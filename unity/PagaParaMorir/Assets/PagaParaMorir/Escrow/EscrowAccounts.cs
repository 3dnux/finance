using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Solana.Unity.Wallet;

namespace PagaParaMorir.Escrow
{
    /// <summary>Estado de una partida, en el mismo orden que el enum del programa.</summary>
    public enum MatchState : byte
    {
        Open = 0,
        InProgress = 1,
        Settled = 2,
        Cancelled = 3,
    }

    /// <summary>Cuenta <c>Match</c> del programa (PDA <c>["match", match_id]</c>).</summary>
    public class MatchAccount
    {
        // Offsets del layout Borsh (8 bytes de discriminador al inicio).
        internal const int StateOffset = 25;
        private const int PlayersOffset = 76;

        public PublicKey Address { get; set; }
        public ulong MatchId { get; set; }
        /// <summary>Entrada por jugador en unidades mínimas de USDC.</summary>
        public ulong EntryFee { get; set; }
        public byte MaxPlayers { get; set; }
        public MatchState State { get; set; }
        public long CreatedAt { get; set; }
        public long StartedAt { get; set; }
        /// <summary><c>null</c> mientras la partida no se liquide.</summary>
        public PublicKey Winner { get; set; }
        public byte Bump { get; set; }
        public byte VaultBump { get; set; }
        public List<PublicKey> Players { get; set; } = new List<PublicKey>();

        /// <summary>Pozo actual: entrada × jugadores.</summary>
        public ulong Pot => EntryFee * (ulong)Players.Count;

        public bool IsFull => Players.Count >= MaxPlayers;

        public bool HasPlayer(PublicKey player) => Players.Exists(p => p.Equals(player));

        public static MatchAccount Deserialize(byte[] data, PublicKey address = null)
        {
            EscrowProgram.CheckDiscriminator(data, EscrowProgram.MatchDiscriminator, "Match");
            var span = data.AsSpan();
            var winner = new PublicKey(span.Slice(42, 32).ToArray());
            var account = new MatchAccount
            {
                Address = address,
                MatchId = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(8)),
                EntryFee = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(16)),
                MaxPlayers = span[24],
                State = (MatchState)span[StateOffset],
                CreatedAt = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(26)),
                StartedAt = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(34)),
                Winner = winner.Equals(PublicKey.DefaultPublicKey) ? null : winner,
                Bump = span[74],
                VaultBump = span[75],
            };
            var count = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(PlayersOffset));
            for (var i = 0; i < count; i++)
            {
                account.Players.Add(new PublicKey(span.Slice(PlayersOffset + 4 + i * 32, 32).ToArray()));
            }
            return account;
        }
    }

    /// <summary>Cuenta <c>Config</c> global del programa (PDA <c>["config"]</c>).</summary>
    public class ConfigAccount
    {
        public PublicKey Admin { get; set; }
        /// <summary>Clave del servidor del juego.</summary>
        public PublicKey Authority { get; set; }
        public PublicKey Treasury { get; set; }
        public PublicKey UsdcMint { get; set; }
        /// <summary>Comisión en puntos básicos (2000 = 20%).</summary>
        public ushort FeeBps { get; set; }
        public long SettleTimeoutSecs { get; set; }
        public bool Paused { get; set; }
        public byte Bump { get; set; }

        /// <summary>Lo que recibiría el ganador de un pozo, igual que <c>split_pot</c> en el programa.</summary>
        public ulong PrizeFor(ulong pot)
        {
            var fee = (ulong)((System.Numerics.BigInteger)pot * FeeBps / 10_000);
            return pot - fee;
        }

        public static ConfigAccount Deserialize(byte[] data)
        {
            EscrowProgram.CheckDiscriminator(data, EscrowProgram.ConfigDiscriminator, "Config");
            var span = data.AsSpan();
            return new ConfigAccount
            {
                Admin = new PublicKey(span.Slice(8, 32).ToArray()),
                Authority = new PublicKey(span.Slice(40, 32).ToArray()),
                Treasury = new PublicKey(span.Slice(72, 32).ToArray()),
                UsdcMint = new PublicKey(span.Slice(104, 32).ToArray()),
                FeeBps = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(136)),
                SettleTimeoutSecs = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(138)),
                Paused = span[146] != 0,
                Bump = span[147],
            };
        }
    }
}
