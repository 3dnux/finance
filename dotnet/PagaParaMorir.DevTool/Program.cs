using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PagaParaMorir.Escrow;
using Solana.Unity.Programs;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;

namespace PagaParaMorir.DevTool
{
    /// <summary>
    /// ppm — maneja el escrow desde la terminal (como servidor/admin) mientras no exista el backend.
    /// </summary>
    public static class Program
    {
        private const string Usage = @"Uso: ppm <comando> [opciones]

Comandos (firma la clave de --keypair):
  config                                  Muestra la configuración del juego.
  init --mint <MINT> --authority <PUBKEY> Configura el juego (solo la upgrade authority).
       [--fee-bps 2000] [--timeout 3600]  La tesorería es la cuenta de USDC de quien firma.
  list                                    Lista todas las partidas.
  create --id <N> --entry <USDC> --max <N>  Abre una sala.
  start --id <N>                          Empieza la partida.
  settle --id <N> --winner <PUBKEY>       Paga al ganador (80%) y a la tesorería (20%).
  cancel --id <N>                         Cancela; los jugadores reclaman su entrada.
  close --id <N>                          Cierra una partida terminada y recupera la renta.

Opciones generales:
  --url <devnet|localnet|mainnet|URL>     Red (por defecto devnet).
  --keypair <ruta>                        Keypair de Solana CLI (por defecto ~/.config/solana/id.json).
  --program <PROGRAM_ID>                  Program ID del escrow (por defecto el del repo).";

        public static async Task<int> Main(string[] argv)
        {
            if (argv.Length == 0 || argv[0] == "--help" || argv[0] == "-h")
            {
                Console.WriteLine(Usage);
                return argv.Length == 0 ? 1 : 0;
            }

            try
            {
                var args = Args.Parse(argv.Skip(1).ToArray());
                var tool = new Tool(args);
                switch (argv[0])
                {
                    case "config": await tool.ShowConfig(); break;
                    case "init": await tool.Init(); break;
                    case "list": await tool.List(); break;
                    case "create": await tool.Create(); break;
                    case "start": await tool.Start(); break;
                    case "settle": await tool.Settle(); break;
                    case "cancel": await tool.Cancel(); break;
                    case "close": await tool.Close(); break;
                    default:
                        Console.Error.WriteLine($"Comando desconocido: {argv[0]}\n\n{Usage}");
                        return 1;
                }
                return 0;
            }
            catch (EscrowException e)
            {
                Console.Error.WriteLine("Error: " + e.Message);
                foreach (var line in e.Logs) Console.Error.WriteLine("  " + line);
                return 2;
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine("Error: " + e.Message);
                return 1;
            }
        }
    }

    internal class Tool
    {
        private readonly Args _args;
        private readonly EscrowClient _client;
        private readonly EscrowProgram _program;
        private readonly Lazy<Account> _signer;

        public Tool(Args args)
        {
            _args = args;
            _program = new EscrowProgram(new PublicKey(args.Get("program", EscrowProgram.DefaultProgramId)));
            _client = new EscrowClient(ClientFactory.GetClient(ResolveUrl(args.Get("url", "devnet"))), _program);
            _signer = new Lazy<Account>(() => LoadKeypair(args.Get("keypair",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/solana/id.json"))));
        }

        private Account Signer => _signer.Value;

        public async Task ShowConfig()
        {
            var c = await _client.GetConfigAsync();
            Console.WriteLine($"Admin:      {c.Admin}");
            Console.WriteLine($"Servidor:   {c.Authority}");
            Console.WriteLine($"Tesorería:  {c.Treasury}");
            Console.WriteLine($"USDC mint:  {c.UsdcMint}");
            Console.WriteLine($"Comisión:   {c.FeeBps / 100m:0.##}%");
            Console.WriteLine($"Timeout:    {c.SettleTimeoutSecs} s");
            Console.WriteLine($"En pausa:   {(c.Paused ? "sí" : "no")}");
        }

        public async Task Init()
        {
            var mint = new PublicKey(_args.Require("mint"));
            var authority = new PublicKey(_args.Require("authority"));
            var feeBps = ushort.Parse(_args.Get("fee-bps", "2000"), CultureInfo.InvariantCulture);
            var timeout = long.Parse(_args.Get("timeout", "3600"), CultureInfo.InvariantCulture);
            var treasury = EscrowProgram.UsdcAccountOf(Signer.PublicKey, mint);

            await Send(
                AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(Signer.PublicKey, Signer.PublicKey, mint, true),
                _program.InitializeConfig(Signer.PublicKey, mint, treasury, authority, feeBps, timeout));
            Console.WriteLine($"Juego configurado. Tesorería: {treasury}");
        }

        public async Task List()
        {
            var config = await _client.GetConfigAsync();
            var matches = await _client.GetMatchesAsync();
            if (matches.Count == 0) Console.WriteLine("No hay partidas.");
            foreach (var m in matches)
            {
                Console.WriteLine($"#{m.MatchId,-6} {m.State,-10} entrada {Usdc.Format(m.EntryFee),-14} " +
                                  $"{m.Players.Count}/{m.MaxPlayers} jugadores  premio {Usdc.Format(config.PrizeFor(m.Pot))}" +
                                  (m.Winner != null ? $"  ganador {m.Winner}" : ""));
                foreach (var p in m.Players) Console.WriteLine($"         - {p}");
            }
        }

        public async Task Create()
        {
            var config = await _client.GetConfigAsync();
            var id = MatchId();
            var entry = Usdc.FromDecimal(decimal.Parse(_args.Require("entry"), CultureInfo.InvariantCulture));
            var max = byte.Parse(_args.Require("max"), CultureInfo.InvariantCulture);
            await Send(_program.CreateMatch(Signer.PublicKey, config.UsdcMint, id, entry, max));
            Console.WriteLine($"Sala #{id} abierta: entrada {Usdc.Format(entry)}, {max} jugadores.");
        }

        public async Task Start()
        {
            var id = MatchId();
            await Send(_program.StartMatch(Signer.PublicKey, id));
            Console.WriteLine($"Sala #{id} en juego.");
        }

        public async Task Settle()
        {
            var config = await _client.GetConfigAsync();
            var id = MatchId();
            var winner = new PublicKey(_args.Require("winner"));
            var match = await _client.GetMatchAsync(id) ?? throw new ArgumentException($"No existe la sala #{id}.");
            await Send(_program.SettleMatch(Signer.PublicKey, id, winner, config.Treasury, config.UsdcMint));
            Console.WriteLine($"Sala #{id} liquidada: {winner} cobró {Usdc.Format(config.PrizeFor(match.Pot))}.");
        }

        public async Task Cancel()
        {
            var id = MatchId();
            await Send(_program.CancelMatch(Signer.PublicKey, id));
            Console.WriteLine($"Sala #{id} cancelada. Los jugadores ya pueden reclamar su entrada.");
        }

        public async Task Close()
        {
            var config = await _client.GetConfigAsync();
            var id = MatchId();
            await Send(_program.CloseMatch(Signer.PublicKey, id, config.Treasury, config.UsdcMint));
            Console.WriteLine($"Sala #{id} cerrada.");
        }

        private ulong MatchId() => ulong.Parse(_args.Require("id"), CultureInfo.InvariantCulture);

        private async Task Send(params Solana.Unity.Rpc.Models.TransactionInstruction[] instructions)
        {
            var signature = await _client.SendAsync(Signer.PublicKey, tx =>
            {
                tx.Sign(Signer);
                return _client.Rpc.SendTransactionAsync(tx.Serialize(), false, Commitment.Confirmed);
            }, instructions);
            Console.WriteLine($"Transacción: {signature}");
        }

        private static string ResolveUrl(string url)
        {
            switch (url)
            {
                case "devnet": return "https://api.devnet.solana.com";
                case "mainnet": return "https://api.mainnet-beta.solana.com";
                case "localnet": return "http://127.0.0.1:8899";
                default: return url;
            }
        }

        private static Account LoadKeypair(string path)
        {
            if (!File.Exists(path)) throw new ArgumentException($"No existe el keypair {path} (usa --keypair).");
            return Keypairs.FromSolanaCliJson(File.ReadAllText(path));
        }
    }

    internal class Args
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        public static Args Parse(string[] argv)
        {
            var args = new Args();
            for (var i = 0; i < argv.Length; i++)
            {
                if (!argv[i].StartsWith("--") || i + 1 >= argv.Length)
                    throw new ArgumentException($"Opción inválida: {argv[i]}");
                args._values[argv[i].Substring(2)] = argv[++i];
            }
            return args;
        }

        public string Get(string name, string fallback) => _values.TryGetValue(name, out var v) ? v : fallback;

        public string Require(string name) =>
            _values.TryGetValue(name, out var v) ? v : throw new ArgumentException($"Falta --{name}.");
    }
}
