using PagaParaMorir.Backend;
using PagaParaMorir.Escrow;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection(BackendOptions.Section).Get<BackendOptions>() ?? new BackendOptions();
options.Validate();
builder.Services.AddSingleton(options);
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new StateStore(options.StateFile, options.FirstMatchId));
builder.Services.AddSingleton<IChain>(sp =>
{
    var key = Keypairs.FromSolanaCliJson(File.ReadAllText(options.ServerKeypairPath));
    return new SolanaChain(options.RpcUrl, options.ProgramId, key);
});
builder.Services.AddSingleton<IGameServerLauncher>(sp => options.Launcher.Mode == LauncherMode.Process
    ? ActivatorUtilities.CreateInstance<ProcessLauncher>(sp)
    : ActivatorUtilities.CreateInstance<ManualLauncher>(sp));
builder.Services.AddSingleton<Orchestrator>();
builder.Services.AddHostedService<OrchestratorService>();

var app = builder.Build();

app.MapGet("/api/health", (Orchestrator o) => Results.Ok(new
{
    ok = o.LastTickAt != null,
    lastTickAt = o.LastTickAt,
    servers = o.RunningServers,
    rpc = options.RpcUrl,
    programId = options.ProgramId,
}));

app.MapGet("/api/tiers", () => options.Tiers.Select(t => new
{
    t.Name,
    entryFee = t.EntryFee,
    entry = Usdc.Format(t.EntryFee),
    t.MaxPlayers,
}));

// Envuelto en un objeto: JsonUtility de Unity no lee arreglos en la raíz.
app.MapGet("/api/rooms", (Orchestrator o) => new { rooms = o.Rooms });

app.MapGet("/api/matches/{matchId}/server", (ulong matchId, Orchestrator o) =>
{
    var room = o.Rooms.FirstOrDefault(r => r.MatchId == matchId);
    if (room == null)
        return Results.NotFound(new { error = $"La sala #{matchId} no existe o ya terminó." });
    if (room.Server == null || room.Server.Status != ServerStatus.Running)
        return Results.NotFound(new
        {
            error = $"El servidor de la sala #{matchId} todavía no está listo: se abre cuando hayan pagado {options.LaunchAtPlayers} jugadores.",
        });
    return Results.Ok(room.Server);
});

app.Run();

/// <summary>Visible para las pruebas de la API.</summary>
public partial class Program;
