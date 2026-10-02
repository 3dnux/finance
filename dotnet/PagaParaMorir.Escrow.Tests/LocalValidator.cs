using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PagaParaMorir.Escrow;
using Solana.Unity.Programs;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using Xunit;

namespace PagaParaMorir.Escrow.Tests
{
    /// <summary>
    /// Levanta <c>solana-test-validator</c> con el programa compilado y deja el juego configurado:
    /// USDC de prueba, tesorería y <c>initialize_config</c> con 20% de comisión.
    /// </summary>
    public class LocalValidator : IAsyncLifetime
    {
        public const ushort FeeBps = 2000;
        public const long SettleTimeoutSecs = 3600;
        private const int RpcPort = 18899;

        public string SkipReason { get; private set; }
        public IRpcClient Rpc { get; private set; }
        public EscrowClient Client { get; private set; }
        public EscrowProgram Program { get; } = new EscrowProgram();
        public Account Admin { get; } = new Account();
        public Account Server { get; } = new Account();
        public Account Mint { get; } = new Account();
        public PublicKey Treasury => EscrowProgram.UsdcAccountOf(Admin.PublicKey, Mint.PublicKey);

        private Process _process;
        private string _ledger;

        public async Task InitializeAsync()
        {
            var validator = FindValidator();
            var so = Path.Combine(RepoRoot.Path, "target", "deploy", "paga_para_morir.so");
            if (validator == null) { SkipReason = "solana-test-validator no está instalado"; return; }
            if (!File.Exists(so)) { SkipReason = "falta target/deploy/paga_para_morir.so (corre `anchor build`)"; return; }

            _ledger = Path.Combine(Path.GetTempPath(), "ppm-ledger-" + Guid.NewGuid().ToString("N"));
            _process = Process.Start(new ProcessStartInfo
            {
                FileName = validator,
                Arguments = string.Join(" ", new[]
                {
                    "--reset", "--quiet", "--ledger", _ledger,
                    "--rpc-port", RpcPort.ToString(), "--faucet-port", "19900",
                    "--gossip-port", "18001", "--dynamic-port-range", "18002-18200",
                    "--upgradeable-program", EscrowProgram.DefaultProgramId, so, Admin.PublicKey.Key,
                }),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            Rpc = ClientFactory.GetClient($"http://127.0.0.1:{RpcPort}");
            Client = new EscrowClient(Rpc, Program);
            await WaitUntilReady();

            await Airdrop(Admin.PublicKey);
            await Airdrop(Server.PublicKey);
            await CreateMint();
            await Send(Admin, Program.InitializeConfig(
                Admin.PublicKey, Mint.PublicKey, Treasury, Server.PublicKey, FeeBps, SettleTimeoutSecs));
        }

        public Task DisposeAsync()
        {
            try { if (_process != null && !_process.HasExited) _process.Kill(true); } catch { }
            try { if (_ledger != null) Directory.Delete(_ledger, true); } catch { }
            return Task.CompletedTask;
        }

        /// <summary>Jugador nuevo con SOL para comisiones y <paramref name="usdc"/> unidades de USDC.</summary>
        public async Task<Account> NewPlayer(ulong usdc)
        {
            var player = new Account();
            await Airdrop(player.PublicKey);
            var ata = EscrowProgram.UsdcAccountOf(player.PublicKey, Mint.PublicKey);
            await Send(Admin,
                AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(Admin.PublicKey, player.PublicKey, Mint.PublicKey),
                TokenProgram.MintTo(Mint.PublicKey, ata, usdc, Admin.PublicKey));
            return player;
        }

        /// <summary>Firma con las cuentas dadas; la primera paga la comisión de red.</summary>
        public SignAndSend Signer(params Account[] signers) => tx =>
        {
            tx.Sign(signers.ToList());
            return Rpc.SendTransactionAsync(tx.Serialize(), false, Commitment.Confirmed);
        };

        public Task<string> Send(Account payer, params Solana.Unity.Rpc.Models.TransactionInstruction[] ixs) =>
            Client.SendAsync(payer.PublicKey, Signer(payer), ixs);

        public Task<string> SendWith(Account[] signers, params Solana.Unity.Rpc.Models.TransactionInstruction[] ixs) =>
            Client.SendAsync(signers[0].PublicKey, Signer(signers), ixs);

        private async Task CreateMint()
        {
            var rent = await Rpc.GetMinimumBalanceForRentExemptionAsync(TokenProgram.MintAccountDataSize);
            await SendWith(new[] { Admin, Mint },
                SystemProgram.CreateAccount(Admin.PublicKey, Mint.PublicKey, rent.Result,
                    TokenProgram.MintAccountDataSize, TokenProgram.ProgramIdKey),
                TokenProgram.InitializeMint(Mint.PublicKey, Usdc.Decimals, Admin.PublicKey),
                AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(Admin.PublicKey, Admin.PublicKey, Mint.PublicKey));
        }

        private async Task Airdrop(PublicKey to)
        {
            var sig = await Rpc.RequestAirdropAsync(to.Key, 2_000_000_000, Commitment.Confirmed);
            Assert.True(sig.WasSuccessful, "airdrop: " + sig.Reason);
            await Client.ConfirmAsync(sig.Result);
        }

        private async Task WaitUntilReady()
        {
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (DateTime.UtcNow < deadline)
            {
                if (_process.HasExited)
                    throw new InvalidOperationException("solana-test-validator terminó: " + _process.StandardError.ReadToEnd());
                try
                {
                    var programData = await Rpc.GetAccountInfoAsync(Program.ProgramDataAddress().Key);
                    if (programData.WasSuccessful && programData.Result?.Value != null) return;
                }
                catch { }
                await Task.Delay(500);
            }
            throw new TimeoutException("solana-test-validator no arrancó a tiempo");
        }

        private static string FindValidator()
        {
            var candidates = new List<string>();
            var fromEnv = Environment.GetEnvironmentVariable("SOLANA_BIN");
            if (!string.IsNullOrEmpty(fromEnv)) candidates.Add(Path.Combine(fromEnv, "solana-test-validator"));
            candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator).Select(p => Path.Combine(p, "solana-test-validator")));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local/share/solana/install/active_release/bin/solana-test-validator"));
            return candidates.FirstOrDefault(File.Exists);
        }
    }

    [CollectionDefinition("validator")]
    public class ValidatorCollection : ICollectionFixture<LocalValidator> { }
}
