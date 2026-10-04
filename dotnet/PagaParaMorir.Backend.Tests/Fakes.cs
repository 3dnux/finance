using Microsoft.Extensions.Logging.Abstractions;
using PagaParaMorir.Escrow;
using Solana.Unity.Wallet;

namespace PagaParaMorir.Backend.Tests;

/// <summary>El contrato en memoria, con las mismas reglas de estado que el programa real.</summary>
public sealed class FakeChain(FakeClock clock) : IChain
{
    public List<MatchAccount> Matches { get; } = new();
    public bool Paused { get; set; }
    public bool FailCreates { get; set; }
    public List<string> Calls { get; } = new();

    public Task<ConfigAccount> GetConfigAsync() =>
        Task.FromResult(new ConfigAccount { FeeBps = 2000, Paused = Paused });

    public Task<IReadOnlyList<MatchAccount>> GetMatchesAsync() =>
        Task.FromResult<IReadOnlyList<MatchAccount>>(Matches.Select(Clone).ToList());

    public Task CreateMatchAsync(ulong matchId, ulong entryFee, byte maxPlayers)
    {
        Calls.Add($"create {matchId}");
        if (FailCreates) throw new EscrowException("RPC caído");
        if (Matches.Any(m => m.MatchId == matchId)) throw new EscrowException("ya existe");
        Matches.Add(new MatchAccount
        {
            MatchId = matchId, EntryFee = entryFee, MaxPlayers = maxPlayers,
            State = MatchState.Open, CreatedAt = clock.GetUtcNow().ToUnixTimeSeconds(),
        });
        return Task.CompletedTask;
    }

    public Task CancelMatchAsync(ulong matchId)
    {
        Calls.Add($"cancel {matchId}");
        var m = Get(matchId);
        if (m.State != MatchState.Open && m.State != MatchState.InProgress)
            throw new EscrowException("InvalidState", EscrowErrorCode.InvalidState);
        m.State = MatchState.Cancelled;
        return Task.CompletedTask;
    }

    public Task CloseMatchAsync(ulong matchId)
    {
        Calls.Add($"close {matchId}");
        var m = Get(matchId);
        if (m.State != MatchState.Settled && !(m.State == MatchState.Cancelled && m.Players.Count == 0))
            throw new EscrowException("InvalidState", EscrowErrorCode.InvalidState);
        Matches.Remove(m);
        return Task.CompletedTask;
    }

    public MatchAccount Get(ulong matchId) =>
        Matches.FirstOrDefault(m => m.MatchId == matchId) ?? throw new EscrowException("no existe");

    public void Join(ulong matchId, int players = 1)
    {
        for (var i = 0; i < players; i++) Get(matchId).Players.Add(new Account().PublicKey);
    }

    public void Start(ulong matchId)
    {
        var m = Get(matchId);
        m.State = MatchState.InProgress;
        m.StartedAt = clock.GetUtcNow().ToUnixTimeSeconds();
    }

    public void Settle(ulong matchId) => Get(matchId).State = MatchState.Settled;

    private static MatchAccount Clone(MatchAccount m) => new()
    {
        MatchId = m.MatchId, EntryFee = m.EntryFee, MaxPlayers = m.MaxPlayers, State = m.State,
        CreatedAt = m.CreatedAt, StartedAt = m.StartedAt, Players = m.Players.ToList(),
    };
}

public sealed class FakeServer : IGameServer
{
    public bool HasExited { get; set; }
    public int? ExitCode { get; set; }
    public bool Stopped { get; private set; }

    public void Stop()
    {
        Stopped = true;
        HasExited = true;
    }
}

public sealed class FakeLauncher : IGameServerLauncher
{
    public List<(ulong MatchId, ushort Port, FakeServer Server)> Launched { get; } = new();

    public IGameServer Launch(ulong matchId, ushort port)
    {
        var server = new FakeServer();
        Launched.Add((matchId, port, server));
        return server;
    }

    public FakeServer Last(ulong matchId) => Launched.Last(l => l.MatchId == matchId).Server;
}

public sealed class FakeClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan time) => Now += time;
}

/// <summary>Orquestador con todo falso, listo para probar.</summary>
public sealed class Harness : IDisposable
{
    public FakeClock Clock { get; } = new();
    public FakeChain Chain { get; }
    public FakeLauncher Launcher { get; } = new();
    public BackendOptions Options { get; }
    public Orchestrator Orchestrator { get; }
    private readonly string _stateFile = Path.Combine(Path.GetTempPath(), $"ppm-state-{Guid.NewGuid():N}.json");

    public static readonly RoomTier Cheap = new() { Name = "1 USDC", EntryUsdc = 1, MaxPlayers = 4 };
    public static readonly RoomTier Pricey = new() { Name = "5 USDC", EntryUsdc = 5, MaxPlayers = 2 };

    public Harness(Action<BackendOptions>? configure = null)
    {
        Chain = new FakeChain(Clock);
        Options = new BackendOptions { Tiers = { Cheap, Pricey } };
        configure?.Invoke(Options);
        Orchestrator = new Orchestrator(Chain, Launcher, Options, new StateStore(_stateFile, Options.FirstMatchId),
            Clock, NullLogger<Orchestrator>.Instance);
    }

    public Task Tick() => Orchestrator.TickAsync();

    public MatchAccount OpenOf(RoomTier tier) =>
        Chain.Matches.Single(m => m.State == MatchState.Open && !m.IsFull && tier.Matches(m));

    public void Dispose()
    {
        if (File.Exists(_stateFile)) File.Delete(_stateFile);
    }
}
