using System.Linq;
using System.Threading.Tasks;
using PagaParaMorir.Escrow;
using Xunit;

namespace PagaParaMorir.Escrow.Tests
{
    /// <summary>
    /// El cliente C# contra el programa real en <c>solana-test-validator</c>.
    /// Se saltan si no está instalado el validador o el programa compilado.
    /// </summary>
    [Collection("validator")]
    public class EscrowIntegrationTests
    {
        private const ulong Entry = 5 * Usdc.OneUsdc;
        private readonly LocalValidator _v;

        public EscrowIntegrationTests(LocalValidator validator)
        {
            _v = validator;
        }

        private void SkipIfUnavailable() => Skip.If(_v.SkipReason != null, _v.SkipReason);

        private Task CreateMatch(ulong matchId, byte maxPlayers) =>
            _v.Send(_v.Server, _v.Program.CreateMatch(_v.Server.PublicKey, _v.Mint.PublicKey, matchId, Entry, maxPlayers));

        [SkippableFact]
        public async Task Lee_la_configuracion()
        {
            SkipIfUnavailable();
            var config = await _v.Client.GetConfigAsync(refresh: true);
            Assert.Equal(_v.Server.PublicKey, config.Authority);
            Assert.Equal(_v.Mint.PublicKey, config.UsdcMint);
            Assert.Equal(_v.Treasury, config.Treasury);
            Assert.Equal(LocalValidator.FeeBps, config.FeeBps);
            Assert.False(config.Paused);
        }

        [SkippableFact]
        public async Task Partida_completa_desde_el_cliente()
        {
            SkipIfUnavailable();
            var a = await _v.NewPlayer(10 * Usdc.OneUsdc);
            var b = await _v.NewPlayer(10 * Usdc.OneUsdc);
            var c = await _v.NewPlayer(10 * Usdc.OneUsdc);
            var treasuryBefore = await _v.Client.GetUsdcBalanceAsync(_v.Admin.PublicKey);

            await CreateMatch(100, 3);
            var open = await _v.Client.GetMatchesAsync(MatchState.Open);
            Assert.Contains(open, m => m.MatchId == 100);

            await _v.Client.JoinMatchAsync(a.PublicKey, 100, _v.Signer(a));
            await _v.Client.JoinMatchAsync(b.PublicKey, 100, _v.Signer(b));
            await _v.Client.JoinMatchAsync(c.PublicKey, 100, _v.Signer(c));
            await _v.Client.LeaveMatchAsync(c.PublicKey, 100, _v.Signer(c));
            Assert.Equal(10 * Usdc.OneUsdc, await _v.Client.GetUsdcBalanceAsync(c.PublicKey));

            var match = await _v.Client.GetMatchAsync(100);
            Assert.Equal(MatchState.Open, match.State);
            Assert.Equal(new[] { a.PublicKey.Key, b.PublicKey.Key }, match.Players.Select(p => p.Key));
            Assert.Equal(2 * Entry, match.Pot);
            Assert.Single(await _v.Client.GetMatchesOfPlayerAsync(a.PublicKey));

            await _v.Send(_v.Server, _v.Program.StartMatch(_v.Server.PublicKey, 100));
            Assert.Equal(MatchState.InProgress, (await _v.Client.GetMatchAsync(100)).State);
            Assert.DoesNotContain(await _v.Client.GetMatchesAsync(MatchState.Open), m => m.MatchId == 100);

            await _v.Send(_v.Server, _v.Program.SettleMatch(
                _v.Server.PublicKey, 100, b.PublicKey, _v.Treasury, _v.Mint.PublicKey));

            // Pozo 10 USDC → 8 al ganador, 2 a la casa.
            Assert.Equal(13 * Usdc.OneUsdc, await _v.Client.GetUsdcBalanceAsync(b.PublicKey));
            Assert.Equal(5 * Usdc.OneUsdc, await _v.Client.GetUsdcBalanceAsync(a.PublicKey));
            Assert.Equal(treasuryBefore + 2 * Usdc.OneUsdc, await _v.Client.GetUsdcBalanceAsync(_v.Admin.PublicKey));
            match = await _v.Client.GetMatchAsync(100);
            Assert.Equal(MatchState.Settled, match.State);
            Assert.Equal(b.PublicKey, match.Winner);
            Assert.Empty(await _v.Client.GetMatchesOfPlayerAsync(b.PublicKey));

            await _v.Send(_v.Server, _v.Program.CloseMatch(_v.Server.PublicKey, 100, _v.Treasury, _v.Mint.PublicKey));
            Assert.Null(await _v.Client.GetMatchAsync(100));
        }

        [SkippableFact]
        public async Task Reembolso_de_partida_cancelada()
        {
            SkipIfUnavailable();
            var a = await _v.NewPlayer(10 * Usdc.OneUsdc);
            await CreateMatch(200, 4);
            await _v.Client.JoinMatchAsync(a.PublicKey, 200, _v.Signer(a));
            Assert.Equal(5 * Usdc.OneUsdc, await _v.Client.GetUsdcBalanceAsync(a.PublicKey));

            await _v.Send(_v.Server, _v.Program.CancelMatch(_v.Server.PublicKey, 200));
            Assert.Contains(await _v.Client.GetMatchesOfPlayerAsync(a.PublicKey), m => m.State == MatchState.Cancelled);

            await _v.Client.ClaimRefundAsync(a.PublicKey, 200, _v.Signer(a));
            Assert.Equal(10 * Usdc.OneUsdc, await _v.Client.GetUsdcBalanceAsync(a.PublicKey));
            Assert.Empty(await _v.Client.GetMatchesOfPlayerAsync(a.PublicKey));
        }

        [SkippableFact]
        public async Task Errores_del_programa_llegan_en_espanol()
        {
            SkipIfUnavailable();
            var a = await _v.NewPlayer(10 * Usdc.OneUsdc);
            var pobre = await _v.NewPlayer(Usdc.OneUsdc);
            await CreateMatch(300, 2);

            // Validación local antes de gastar comisión de red.
            var sinSaldo = await Assert.ThrowsAsync<EscrowException>(
                () => _v.Client.JoinMatchAsync(pobre.PublicKey, 300, _v.Signer(pobre)));
            Assert.Equal("Necesitas 5.00 USDC para entrar.", sinSaldo.Message);

            await _v.Client.JoinMatchAsync(a.PublicKey, 300, _v.Signer(a));

            // Error que solo detecta el programa: lo saltamos del lado del cliente con la instrucción cruda.
            var config = await _v.Client.GetConfigAsync();
            var repetido = await Assert.ThrowsAsync<EscrowException>(() =>
                _v.Send(a, _v.Program.JoinMatch(a.PublicKey, 300, config.UsdcMint)));
            Assert.Equal(EscrowErrorCode.AlreadyJoined, repetido.Code);
            Assert.Equal("Ya estás en esta sala.", repetido.Message);

            var noJugador = await Assert.ThrowsAsync<EscrowException>(() =>
                _v.Client.ClaimRefundAsync(pobre.PublicKey, 300, _v.Signer(pobre)));
            Assert.Equal(EscrowErrorCode.InvalidState, noJugador.Code);
        }
    }
}
