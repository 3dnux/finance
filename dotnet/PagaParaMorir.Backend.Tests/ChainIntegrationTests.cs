using Microsoft.Extensions.Logging.Abstractions;
using PagaParaMorir.Escrow;
using PagaParaMorir.Escrow.Tests;
using Xunit;

namespace PagaParaMorir.Backend.Tests;

/// <summary>El orquestador contra el contrato real en <c>solana-test-validator</c>.</summary>
public class ChainIntegrationTests : IClassFixture<LocalValidator>, IDisposable
{
    private readonly LocalValidator _v;
    private readonly string _stateFile = Path.Combine(Path.GetTempPath(), $"ppm-chain-{Guid.NewGuid():N}.json");

    public ChainIntegrationTests(LocalValidator validator)
    {
        _v = validator;
    }

    public void Dispose()
    {
        if (File.Exists(_stateFile)) File.Delete(_stateFile);
    }

    [SkippableFact]
    public async Task Abre_salas_levanta_servidor_y_reembolsa_si_el_servidor_muere()
    {
        Skip.If(_v.SkipReason != null, _v.SkipReason);
        var tier = new RoomTier { Name = "1 USDC", EntryUsdc = 1, MaxPlayers = 3 };
        var options = new BackendOptions { Tiers = { tier } };
        var launcher = new FakeLauncher();
        var chain = new SolanaChain(new EscrowClient(_v.Rpc, _v.Program), _v.Server);
        var orchestrator = new Orchestrator(chain, launcher, options, new StateStore(_stateFile, 5000),
            TimeProvider.System, NullLogger<Orchestrator>.Instance);

        // 1. Abre la sala del tipo configurado on-chain.
        await orchestrator.TickAsync();
        var match = await _v.Client.GetMatchAsync(5000);
        Assert.NotNull(match);
        Assert.Equal(Usdc.OneUsdc, match.EntryFee);
        Assert.Equal(3, match.MaxPlayers);

        // 2. Dos jugadores pagan: se levanta el servidor.
        var a = await _v.NewPlayer(10 * Usdc.OneUsdc);
        var b = await _v.NewPlayer(10 * Usdc.OneUsdc);
        await _v.Client.JoinMatchAsync(a.PublicKey, 5000, _v.Signer(a));
        await _v.Client.JoinMatchAsync(b.PublicKey, 5000, _v.Signer(b));
        await orchestrator.TickAsync();
        Assert.Equal(5000UL, Assert.Single(launcher.Launched).MatchId);
        Assert.Equal(ServerStatus.Running, orchestrator.ServerFor(5000)!.Status);

        // 3. El servidor empieza la partida y se cae: el backend cancela y los jugadores recuperan su dinero.
        await _v.Send(_v.Server, _v.Program.StartMatch(_v.Server.PublicKey, 5000));
        launcher.Last(5000).HasExited = true;
        await orchestrator.TickAsync();
        Assert.Equal(MatchState.Cancelled, (await _v.Client.GetMatchAsync(5000))!.State);

        await _v.Client.ClaimRefundAsync(a.PublicKey, 5000, _v.Signer(a));
        await _v.Client.ClaimRefundAsync(b.PublicKey, 5000, _v.Signer(b));
        Assert.Equal(10 * Usdc.OneUsdc, await _v.Client.GetUsdcBalanceAsync(a.PublicKey));

        // 4. Ya reembolsada, el backend la cierra; y siempre hay una sala abierta del tipo.
        await orchestrator.TickAsync();
        Assert.Null(await _v.Client.GetMatchAsync(5000));
        var open = await _v.Client.GetMatchesAsync(MatchState.Open);
        Assert.Contains(open, m => m.MatchId > 5000 && tier.Matches(m));
    }
}
