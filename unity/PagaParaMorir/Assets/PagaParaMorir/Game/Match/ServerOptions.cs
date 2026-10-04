using System;
using System.Globalization;

namespace PagaParaMorir.Game.Match
{
    /// <summary>
    /// Opciones del servidor dedicado, desde la línea de comandos:
    /// <c>-ppm-server -match 12 -port 7777 -rpc URL -program ID -keypair server.json [-lobby-seconds 180]</c>
    /// (o <c>-ppm-server -practice</c> para una partida sin dinero).
    /// </summary>
    public sealed class ServerOptions
    {
        public bool IsServer { get; private set; }
        public bool Practice { get; private set; }
        public ulong MatchId { get; private set; }
        public ushort Port { get; private set; } = 7777;
        public string RpcUrl { get; private set; } = "https://api.devnet.solana.com";
        public string ProgramId { get; private set; } = Escrow.EscrowProgram.DefaultProgramId;
        public string KeypairPath { get; private set; }
        /// <summary>Segundos máximos de sala de espera antes de empezar o cancelar.</summary>
        public double LobbySeconds { get; private set; } = 120;

        public static ServerOptions ForPractice(ushort port) => new ServerOptions { IsServer = true, Practice = true, Port = port };

        public static ServerOptions FromCommandLine(string[] args)
        {
            var o = new ServerOptions();
            for (var i = 0; i < args.Length; i++)
            {
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Falta el valor de {args[i]}");
                switch (args[i])
                {
                    case "-ppm-server": o.IsServer = true; break;
                    case "-practice": o.Practice = true; break;
                    case "-match": o.MatchId = ulong.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "-port": o.Port = ushort.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "-rpc": o.RpcUrl = Next(); break;
                    case "-program": o.ProgramId = Next(); break;
                    case "-keypair": o.KeypairPath = Next(); break;
                    case "-lobby-seconds": o.LobbySeconds = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                }
            }
            if (o.IsServer && !o.Practice && string.IsNullOrEmpty(o.KeypairPath))
                throw new ArgumentException("El servidor de una partida con dinero necesita -keypair (la clave del servidor).");
            return o;
        }
    }
}
