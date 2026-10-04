using PagaParaMorir.Escrow;

namespace PagaParaMorir.Backend;

public enum ServerStatus
{
    Running,
    Exited,
}

/// <summary>Dónde conectarse para jugar una sala.</summary>
public sealed record ServerEndpoint(ulong MatchId, string Host, ushort Port, ServerStatus Status, DateTimeOffset StartedAt);

/// <summary>Una sala tal como la ve el lobby: datos on-chain + estado de su servidor.</summary>
public sealed record RoomStatus(
    ulong MatchId,
    string? Tier,
    ulong EntryFee,
    string Entry,
    byte MaxPlayers,
    int Players,
    string State,
    ServerEndpoint? Server);

/// <summary>
/// Cada <see cref="BackendOptions.TickSeconds"/>: mantiene una sala abierta por tipo, levanta un
/// servidor dedicado cuando una sala junta jugadores y limpia lo que quedó a medias para que
/// ningún jugador se quede sin su dinero.
/// </summary>
public sealed class Orchestrator(
    IChain chain,
    IGameServerLauncher launcher,
    BackendOptions options,
    StateStore state,
    TimeProvider clock,
    ILogger<Orchestrator> logger)
{
    private sealed class ServerEntry(IGameServer server, ushort port, DateTimeOffset startedAt)
    {
        public IGameServer Server { get; } = server;
        public ushort Port { get; } = port;
        public DateTimeOffset StartedAt { get; } = startedAt;
        /// <summary>Cuándo vimos por primera vez que su sala ya no existe on-chain.</summary>
        public DateTimeOffset? MatchGoneAt { get; set; }
    }

    private static readonly TimeSpan LingerAfterClose = TimeSpan.FromMinutes(2);

    private readonly Dictionary<ulong, ServerEntry> _servers = new();
    private readonly Dictionary<ulong, int> _launchAttempts = new();
    private readonly SemaphoreSlim _tickLock = new(1, 1);
    private volatile IReadOnlyList<RoomStatus> _rooms = Array.Empty<RoomStatus>();

    /// <summary>Foto de las salas en el último ciclo (para la API).</summary>
    public IReadOnlyList<RoomStatus> Rooms => _rooms;

    public DateTimeOffset? LastTickAt { get; private set; }
    public int RunningServers => _servers.Count;

    public ServerEndpoint? ServerFor(ulong matchId) => _rooms.FirstOrDefault(r => r.MatchId == matchId)?.Server;

    public async Task TickAsync()
    {
        await _tickLock.WaitAsync();
        try
        {
            var config = await chain.GetConfigAsync();
            var matches = await chain.GetMatchesAsync();
            var now = clock.GetUtcNow();
            var handled = new HashSet<ulong>();

            await ReapServers(matches, handled);
            await Housekeeping(matches, now, handled);
            LaunchServers(matches, handled);
            var created = config.Paused ? new List<MatchAccount>() : await EnsureTiers(matches, handled);

            _rooms = matches.Concat(created).Select(ToStatus).ToList();
            LastTickAt = now;
        }
        finally
        {
            _tickLock.Release();
        }
    }

    /// <summary>Servidores que terminaron: si la partida quedó a medias, se cancela para reembolsar.</summary>
    private async Task ReapServers(IReadOnlyList<MatchAccount> matches, HashSet<ulong> handled)
    {
        // La sala ya se cerró y su servidor sigue vivo mucho después (debería salir solo en ~30 s).
        var now = clock.GetUtcNow();
        foreach (var (matchId, entry) in _servers.ToList())
        {
            if (entry.Server.HasExited || matches.Any(m => m.MatchId == matchId)) continue;
            entry.MatchGoneAt ??= now;
            if (now - entry.MatchGoneAt.Value > LingerAfterClose) StopServer(matchId);
        }

        foreach (var (matchId, entry) in _servers.Where(s => s.Value.Server.HasExited).ToList())
        {
            _servers.Remove(matchId);
            var match = matches.FirstOrDefault(m => m.MatchId == matchId);
            logger.LogInformation("El servidor de la sala #{MatchId} terminó (código {Code}); sala en {State}.",
                matchId, entry.Server.ExitCode, match?.State);
            if (match == null) continue;

            if (match.State == MatchState.InProgress)
                await Act(handled, matchId, "cancelar (el servidor se cayó en juego)", () => chain.CancelMatchAsync(matchId));
            else if (match.State == MatchState.Open && match.Players.Count > 0 &&
                     _launchAttempts.GetValueOrDefault(matchId) >= options.MaxLaunchAttempts)
                await Act(handled, matchId, "cancelar (el servidor no logró arrancar)", () => chain.CancelMatchAsync(matchId));
        }
    }

    private async Task Housekeeping(IReadOnlyList<MatchAccount> matches, DateTimeOffset now, HashSet<ulong> handled)
    {
        var nowUnix = now.ToUnixTimeSeconds();
        foreach (var m in matches)
        {
            if (handled.Contains(m.MatchId)) continue;
            switch (m.State)
            {
                case MatchState.Settled:
                    await Act(handled, m.MatchId, "cerrar (ya pagada)", () => chain.CloseMatchAsync(m.MatchId));
                    break;

                case MatchState.Cancelled when m.Players.Count == 0:
                    await Act(handled, m.MatchId, "cerrar (cancelada y reembolsada)", () => chain.CloseMatchAsync(m.MatchId));
                    break;

                case MatchState.InProgress when nowUnix - m.StartedAt > options.StuckInProgressMinutes * 60:
                    StopServer(m.MatchId);
                    await Act(handled, m.MatchId, "cancelar (partida atascada)", () => chain.CancelMatchAsync(m.MatchId));
                    break;

                case MatchState.Open when m.Players.Count > 0 && m.Players.Count < options.LaunchAtPlayers &&
                                          !_servers.ContainsKey(m.MatchId) &&
                                          nowUnix - m.CreatedAt > options.OpenMatchMaxMinutes * 60:
                    await Act(handled, m.MatchId, "cancelar (no llegaron más jugadores)", () => chain.CancelMatchAsync(m.MatchId));
                    break;
            }
        }
    }

    private void LaunchServers(IReadOnlyList<MatchAccount> matches, HashSet<ulong> handled)
    {
        var ready = matches.Where(m => m.State == MatchState.Open &&
                                       m.Players.Count >= options.LaunchAtPlayers &&
                                       !_servers.ContainsKey(m.MatchId) &&
                                       !handled.Contains(m.MatchId) &&
                                       _launchAttempts.GetValueOrDefault(m.MatchId) < options.MaxLaunchAttempts);
        foreach (var m in ready.OrderBy(m => m.CreatedAt))
        {
            if (_servers.Count >= options.Launcher.MaxServers)
            {
                logger.LogWarning("Se alcanzó el máximo de {Max} servidores; la sala #{MatchId} espera.", options.Launcher.MaxServers, m.MatchId);
                return;
            }
            var port = FreePort();
            if (port == null)
            {
                logger.LogWarning("No hay puertos libres para la sala #{MatchId}.", m.MatchId);
                return;
            }
            _launchAttempts[m.MatchId] = _launchAttempts.GetValueOrDefault(m.MatchId) + 1;
            try
            {
                _servers[m.MatchId] = new ServerEntry(launcher.Launch(m.MatchId, port.Value), port.Value, clock.GetUtcNow());
            }
            catch (Exception e)
            {
                logger.LogError(e, "No se pudo lanzar el servidor de la sala #{MatchId}.", m.MatchId);
            }
        }
    }

    /// <summary>Cada tipo de sala debe tener una abierta con lugar.</summary>
    private async Task<List<MatchAccount>> EnsureTiers(IReadOnlyList<MatchAccount> matches, HashSet<ulong> handled)
    {
        var created = new List<MatchAccount>();
        foreach (var tier in options.Tiers)
        {
            // Las salas que este ciclo canceló o cerró ya no cuentan como abiertas.
            if (matches.Any(m => m.State == MatchState.Open && !m.IsFull && tier.Matches(m) && !handled.Contains(m.MatchId)))
                continue;
            var id = state.AllocateMatchId(matches.Select(m => m.MatchId));
            try
            {
                await chain.CreateMatchAsync(id, tier.EntryFee, tier.MaxPlayers);
                logger.LogInformation("Sala #{MatchId} abierta: {Tier}.", id, tier.Name);
                created.Add(new MatchAccount
                {
                    MatchId = id,
                    EntryFee = tier.EntryFee,
                    MaxPlayers = tier.MaxPlayers,
                    State = MatchState.Open,
                    CreatedAt = clock.GetUtcNow().ToUnixTimeSeconds(),
                });
            }
            catch (Exception e)
            {
                logger.LogError(e, "No se pudo abrir la sala {Tier}.", tier.Name);
            }
        }
        return created;
    }

    private async Task Act(HashSet<ulong> handled, ulong matchId, string what, Func<Task> action)
    {
        handled.Add(matchId);
        try
        {
            await action();
            logger.LogInformation("Sala #{MatchId}: {What}.", matchId, what);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Sala #{MatchId}: no se pudo {What}.", matchId, what);
        }
    }

    private void StopServer(ulong matchId)
    {
        if (!_servers.Remove(matchId, out var entry)) return;
        try
        {
            entry.Server.Stop();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "No se pudo detener el servidor de la sala #{MatchId}.", matchId);
        }
    }

    private ushort? FreePort()
    {
        var used = _servers.Values.Select(s => s.Port).ToHashSet();
        for (var p = options.Launcher.PortRangeStart; p <= options.Launcher.PortRangeEnd; p++)
        {
            if (!used.Contains(p)) return p;
            if (p == ushort.MaxValue) break;
        }
        return null;
    }

    private RoomStatus ToStatus(MatchAccount m)
    {
        ServerEndpoint? server = null;
        if (_servers.TryGetValue(m.MatchId, out var entry))
            server = new ServerEndpoint(m.MatchId, options.Launcher.PublicHost, entry.Port,
                entry.Server.HasExited ? ServerStatus.Exited : ServerStatus.Running, entry.StartedAt);
        return new RoomStatus(
            m.MatchId,
            options.Tiers.FirstOrDefault(t => t.Matches(m))?.Name,
            m.EntryFee,
            Usdc.Format(m.EntryFee),
            m.MaxPlayers,
            m.Players.Count,
            m.State.ToString(),
            server);
    }
}
