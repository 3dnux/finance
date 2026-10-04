namespace PagaParaMorir.Backend;

/// <summary>Corre el orquestador en un ciclo mientras viva el backend.</summary>
public sealed class OrchestratorService(Orchestrator orchestrator, BackendOptions options, ILogger<OrchestratorService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Backend en marcha: {Tiers} tipos de sala, RPC {Rpc}.", options.Tiers.Count, options.RpcUrl);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.TickSeconds));
        do
        {
            try
            {
                await orchestrator.TickAsync();
            }
            catch (Exception e)
            {
                // Sin red o RPC caído: se reintenta en el siguiente ciclo.
                logger.LogError(e, "Falló un ciclo del orquestador.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
