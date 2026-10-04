using PagaParaMorir.Escrow;

namespace PagaParaMorir.Backend;

/// <summary>Un tipo de sala que el backend mantiene siempre abierta (ej. "5 USDC, 8 jugadores").</summary>
public sealed class RoomTier
{
    public string Name { get; set; } = "";
    public decimal EntryUsdc { get; set; }
    public byte MaxPlayers { get; set; }

    public ulong EntryFee => Usdc.FromDecimal(EntryUsdc);

    public bool Matches(MatchAccount match) => match.EntryFee == EntryFee && match.MaxPlayers == MaxPlayers;
}

public enum LauncherMode
{
    /// <summary>Lanza el build de Unity como proceso en esta máquina (un VPS).</summary>
    Process,
    /// <summary>No lanza nada: anota el comando para lanzarlo a mano (desarrollo).</summary>
    Manual,
}

public sealed class LauncherOptions
{
    public LauncherMode Mode { get; set; } = LauncherMode.Manual;
    /// <summary>Ejecutable del servidor dedicado (build de Unity).</summary>
    public string Executable { get; set; } = "";
    /// <summary>Dirección pública que se le da a los jugadores.</summary>
    public string PublicHost { get; set; } = "127.0.0.1";
    public ushort PortRangeStart { get; set; } = 7777;
    public ushort PortRangeEnd { get; set; } = 7877;
    public int MaxServers { get; set; } = 20;
    /// <summary>Segundos de sala de espera del servidor (empieza antes si se llena).</summary>
    public int LobbySeconds { get; set; } = 180;
    public string LogDirectory { get; set; } = "logs";
    public List<string> ExtraArgs { get; set; } = new();
}

public sealed class BackendOptions
{
    public const string Section = "PagaParaMorir";

    public string RpcUrl { get; set; } = "https://api.devnet.solana.com";
    public string ProgramId { get; set; } = EscrowProgram.DefaultProgramId;
    /// <summary>Keypair de Solana CLI del servidor del juego (config.authority).</summary>
    public string ServerKeypairPath { get; set; } = "server.json";
    public string StateFile { get; set; } = "backend-state.json";
    public ulong FirstMatchId { get; set; } = 1000;

    public List<RoomTier> Tiers { get; set; } = new();
    /// <summary>Con cuántos jugadores pagados se levanta el servidor de una sala.</summary>
    public int LaunchAtPlayers { get; set; } = 2;
    /// <summary>Intentos de levantar el servidor de una sala antes de cancelarla.</summary>
    public int MaxLaunchAttempts { get; set; } = 3;
    public double TickSeconds { get; set; } = 5;
    /// <summary>Una sala con jugadores que no llega a <see cref="LaunchAtPlayers"/> en este tiempo se cancela (reembolso).</summary>
    public double OpenMatchMaxMinutes { get; set; } = 30;
    /// <summary>Una partida en juego más vieja que esto quedó atascada: se cancela (reembolso).</summary>
    public double StuckInProgressMinutes { get; set; } = 15;

    public LauncherOptions Launcher { get; set; } = new();

    public void Validate()
    {
        if (Tiers.Count == 0) throw new InvalidOperationException("Configura al menos un tipo de sala (Tiers).");
        foreach (var t in Tiers)
        {
            if (t.EntryUsdc <= 0) throw new InvalidOperationException($"La sala '{t.Name}' necesita una entrada mayor a 0.");
            if (t.MaxPlayers < 2 || t.MaxPlayers > 100) throw new InvalidOperationException($"La sala '{t.Name}' debe tener entre 2 y 100 jugadores.");
        }
        if (LaunchAtPlayers < 2) throw new InvalidOperationException("LaunchAtPlayers debe ser al menos 2.");
        if (Launcher.PortRangeEnd < Launcher.PortRangeStart) throw new InvalidOperationException("Rango de puertos inválido.");
        if (Launcher.Mode == LauncherMode.Process && !File.Exists(Launcher.Executable))
            throw new InvalidOperationException($"No existe el ejecutable del servidor: '{Launcher.Executable}'.");
    }
}
