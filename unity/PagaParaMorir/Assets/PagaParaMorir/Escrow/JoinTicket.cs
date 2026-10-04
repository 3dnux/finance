using System;
using System.Buffers.Binary;
using System.Text;
using System.Threading.Tasks;
using Solana.Unity.Wallet;

namespace PagaParaMorir.Escrow
{
    /// <summary>
    /// Boleto para entrar al servidor de una partida: prueba que quien se conecta controla
    /// la billetera que pagó la entrada. El jugador firma un mensaje con su llave; el
    /// servidor verifica la firma y que esa llave esté en la lista de la partida on-chain.
    /// </summary>
    public sealed class JoinTicket
    {
        private const byte Version = 1;
        public const int EncodedLength = 1 + 8 + 8 + 32 + 64;

        public ulong MatchId { get; }
        /// <summary>Hora de emisión (unix, segundos). El servidor rechaza boletos viejos.</summary>
        public long IssuedAt { get; }
        public PublicKey Player { get; }
        public byte[] Signature { get; }

        private JoinTicket(ulong matchId, long issuedAt, PublicKey player, byte[] signature)
        {
            MatchId = matchId;
            IssuedAt = issuedAt;
            Player = player;
            Signature = signature;
        }

        /// <summary>Lo que firma el jugador. Texto legible para que la billetera pueda mostrarlo.</summary>
        public static byte[] Message(ulong matchId, long issuedAt, PublicKey player) =>
            Encoding.UTF8.GetBytes($"PagaParaMorir:join:{matchId}:{issuedAt}:{player.Key}");

        public static async Task<JoinTicket> CreateAsync(
            ulong matchId, long issuedAt, PublicKey player, Func<byte[], Task<byte[]>> signMessage)
        {
            var signature = await signMessage(Message(matchId, issuedAt, player));
            if (signature == null || signature.Length != 64)
                throw new EscrowException("La billetera no firmó el boleto de entrada.");
            return new JoinTicket(matchId, issuedAt, player, signature);
        }

        public static JoinTicket Create(ulong matchId, long issuedAt, Account player) =>
            new JoinTicket(matchId, issuedAt, player.PublicKey, player.Sign(Message(matchId, issuedAt, player.PublicKey)));

        public byte[] Encode()
        {
            var data = new byte[EncodedLength];
            data[0] = Version;
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(1), MatchId);
            BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(9), IssuedAt);
            Player.KeyBytes.CopyTo(data, 17);
            Signature.CopyTo(data, 49);
            return data;
        }

        /// <summary>Lee un boleto. Devuelve <c>null</c> si los bytes no tienen el formato correcto.</summary>
        public static JoinTicket Decode(byte[] data)
        {
            if (data == null || data.Length != EncodedLength || data[0] != Version) return null;
            var span = data.AsSpan();
            return new JoinTicket(
                BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(1)),
                BinaryPrimitives.ReadInt64LittleEndian(span.Slice(9)),
                new PublicKey(span.Slice(17, 32).ToArray()),
                span.Slice(49, 64).ToArray());
        }

        /// <summary>
        /// Verifica firma, partida y antigüedad. No revisa la lista on-chain: eso lo hace el servidor.
        /// </summary>
        public bool Verify(ulong expectedMatchId, long nowUnix, long maxAgeSeconds, out string error)
        {
            error = null;
            if (MatchId != expectedMatchId) error = "El boleto es de otra partida.";
            else if (Math.Abs(nowUnix - IssuedAt) > maxAgeSeconds) error = "El boleto expiró. Vuelve a intentarlo.";
            else if (!Player.Verify(Message(MatchId, IssuedAt, Player), Signature)) error = "Firma inválida.";
            return error == null;
        }
    }
}
