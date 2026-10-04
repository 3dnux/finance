using System.Diagnostics;

namespace PagaParaMorir.Backend;

/// <summary>Un servidor dedicado de una partida.</summary>
public interface IGameServer
{
    bool HasExited { get; }
    int? ExitCode { get; }
    void Stop();
}

/// <summary>Levanta servidores dedicados. Hoy: procesos locales; mañana: Edgegap, GameLift, Kubernetes…</summary>
public interface IGameServerLauncher
{
    IGameServer Launch(ulong matchId, ushort port);
}

/// <summary>Argumentos del servidor dedicado de Unity (ver ServerOptions en el proyecto de Unity).</summary>
public static class ServerCommand
{
    public static List<string> Arguments(BackendOptions options, ulong matchId, ushort port)
    {
        var args = new List<string>
        {
            "-batchmode", "-nographics", "-ppm-server",
            "-match", matchId.ToString(),
            "-port", port.ToString(),
            "-rpc", options.RpcUrl,
            "-program", options.ProgramId,
            "-keypair", Path.GetFullPath(options.ServerKeypairPath),
            "-lobby-seconds", options.Launcher.LobbySeconds.ToString(),
            "-logFile", Path.GetFullPath(Path.Combine(options.Launcher.LogDirectory, $"match-{matchId}.log")),
        };
        args.AddRange(options.Launcher.ExtraArgs);
        return args;
    }
}

public sealed class ProcessLauncher(BackendOptions options, ILogger<ProcessLauncher> logger) : IGameServerLauncher
{
    public IGameServer Launch(ulong matchId, ushort port)
    {
        Directory.CreateDirectory(options.Launcher.LogDirectory);
        var info = new ProcessStartInfo(options.Launcher.Executable) { UseShellExecute = false };
        foreach (var arg in ServerCommand.Arguments(options, matchId, port)) info.ArgumentList.Add(arg);
        var process = Process.Start(info) ?? throw new InvalidOperationException("No se pudo iniciar el servidor.");
        logger.LogInformation("Servidor de la sala #{MatchId} iniciado (pid {Pid}, puerto {Port}).", matchId, process.Id, port);
        return new ProcessServer(process);
    }

    private sealed class ProcessServer(Process process) : IGameServer
    {
        public bool HasExited => process.HasExited;
        public int? ExitCode => process.HasExited ? process.ExitCode : null;

        public void Stop()
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }
}

/// <summary>Para desarrollo: muestra el comando y supone que alguien lo lanza a mano.</summary>
public sealed class ManualLauncher(BackendOptions options, ILogger<ManualLauncher> logger) : IGameServerLauncher
{
    public IGameServer Launch(ulong matchId, ushort port)
    {
        var command = string.Join(" ", ServerCommand.Arguments(options, matchId, port)
            .Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
        logger.LogWarning("Lanza a mano el servidor de la sala #{MatchId}:\n  <ejecutable> {Command}", matchId, command);
        return new ManualServer();
    }

    private sealed class ManualServer : IGameServer
    {
        public bool HasExited { get; private set; }
        public int? ExitCode => null;
        public void Stop() => HasExited = true;
    }
}
