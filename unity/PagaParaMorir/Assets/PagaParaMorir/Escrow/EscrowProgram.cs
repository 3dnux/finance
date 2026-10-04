using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;

namespace PagaParaMorir.Escrow
{
    /// <summary>
    /// Direcciones (PDAs) e instrucciones del programa escrow de Paga para Morir.
    /// El orden de las cuentas de cada instrucción sigue al IDL (<c>idl/paga_para_morir.json</c>).
    /// </summary>
    public class EscrowProgram
    {
        /// <summary>Program ID declarado en <c>programs/paga-para-morir/src/lib.rs</c>.</summary>
        public const string DefaultProgramId = "2NB9Xwtj7BRFkZEvDWWBhuqTWK18ASTogRt1SCGKZGhg";

        private static readonly PublicKey BpfLoaderUpgradeable =
            new PublicKey("BPFLoaderUpgradeab1e11111111111111111111111");

        internal static readonly byte[] ConfigDiscriminator = AccountDiscriminator("Config");
        internal static readonly byte[] MatchDiscriminator = AccountDiscriminator("Match");

        public PublicKey ProgramId { get; }

        public EscrowProgram(PublicKey programId = null)
        {
            ProgramId = programId ?? new PublicKey(DefaultProgramId);
        }

        // ---------- PDAs ----------

        public PublicKey ConfigAddress() => FindPda(Encoding.UTF8.GetBytes("config"));

        public PublicKey MatchAddress(ulong matchId) =>
            FindPda(Encoding.UTF8.GetBytes("match"), U64(matchId));

        public PublicKey VaultAddress(PublicKey matchAddress) =>
            FindPda(Encoding.UTF8.GetBytes("vault"), matchAddress.KeyBytes);

        public PublicKey ProgramDataAddress()
        {
            PublicKey.TryFindProgramAddress(new[] { ProgramId.KeyBytes }, BpfLoaderUpgradeable, out var address, out _);
            return address;
        }

        /// <summary>Cuenta de USDC del jugador (Associated Token Account).</summary>
        public static PublicKey UsdcAccountOf(PublicKey owner, PublicKey usdcMint) =>
            AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(owner, usdcMint);

        // ---------- Instrucciones del jugador ----------

        public TransactionInstruction JoinMatch(PublicKey player, ulong matchId, PublicKey usdcMint) =>
            PlayerEscrow("join_match", player, matchId, usdcMint);

        public TransactionInstruction LeaveMatch(PublicKey player, ulong matchId, PublicKey usdcMint) =>
            PlayerEscrow("leave_match", player, matchId, usdcMint);

        public TransactionInstruction ClaimRefund(PublicKey player, ulong matchId, PublicKey usdcMint) =>
            PlayerEscrow("claim_refund", player, matchId, usdcMint);

        private TransactionInstruction PlayerEscrow(string name, PublicKey player, ulong matchId, PublicKey usdcMint)
        {
            var match = MatchAddress(matchId);
            return Instruction(name, new List<AccountMeta>
            {
                AccountMeta.ReadOnly(player, true),
                AccountMeta.ReadOnly(ConfigAddress(), false),
                AccountMeta.Writable(match, false),
                AccountMeta.Writable(VaultAddress(match), false),
                AccountMeta.Writable(UsdcAccountOf(player, usdcMint), false),
                AccountMeta.ReadOnly(usdcMint, false),
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
            });
        }

        // ---------- Instrucciones del servidor / admin ----------
        // El cliente del juego no las usa; sirven para pruebas y herramientas de desarrollo.

        public TransactionInstruction InitializeConfig(
            PublicKey admin, PublicKey usdcMint, PublicKey treasury,
            PublicKey authority, ushort feeBps, long settleTimeoutSecs)
        {
            var args = new byte[32 + 2 + 8];
            authority.KeyBytes.CopyTo(args, 0);
            BinaryPrimitives.WriteUInt16LittleEndian(args.AsSpan(32), feeBps);
            BinaryPrimitives.WriteInt64LittleEndian(args.AsSpan(34), settleTimeoutSecs);
            return Instruction("initialize_config", new List<AccountMeta>
            {
                AccountMeta.Writable(admin, true),
                AccountMeta.Writable(ConfigAddress(), false),
                AccountMeta.ReadOnly(usdcMint, false),
                AccountMeta.ReadOnly(treasury, false),
                AccountMeta.ReadOnly(ProgramId, false),
                AccountMeta.ReadOnly(ProgramDataAddress(), false),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
            }, args);
        }

        public TransactionInstruction CreateMatch(
            PublicKey authority, PublicKey usdcMint, ulong matchId, ulong entryFee, byte maxPlayers)
        {
            var match = MatchAddress(matchId);
            var args = new byte[8 + 8 + 1];
            BinaryPrimitives.WriteUInt64LittleEndian(args.AsSpan(0), matchId);
            BinaryPrimitives.WriteUInt64LittleEndian(args.AsSpan(8), entryFee);
            args[16] = maxPlayers;
            return Instruction("create_match", new List<AccountMeta>
            {
                AccountMeta.Writable(authority, true),
                AccountMeta.ReadOnly(ConfigAddress(), false),
                AccountMeta.Writable(match, false),
                AccountMeta.Writable(VaultAddress(match), false),
                AccountMeta.ReadOnly(usdcMint, false),
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
            }, args);
        }

        public TransactionInstruction StartMatch(PublicKey authority, ulong matchId) =>
            Instruction("start_match", new List<AccountMeta>
            {
                AccountMeta.ReadOnly(authority, true),
                AccountMeta.ReadOnly(ConfigAddress(), false),
                AccountMeta.Writable(MatchAddress(matchId), false),
            });

        public TransactionInstruction SettleMatch(
            PublicKey authority, ulong matchId, PublicKey winner, PublicKey treasury, PublicKey usdcMint)
        {
            var match = MatchAddress(matchId);
            return Instruction("settle_match", new List<AccountMeta>
            {
                AccountMeta.ReadOnly(authority, true),
                AccountMeta.ReadOnly(ConfigAddress(), false),
                AccountMeta.Writable(match, false),
                AccountMeta.Writable(VaultAddress(match), false),
                AccountMeta.Writable(UsdcAccountOf(winner, usdcMint), false),
                AccountMeta.Writable(treasury, false),
                AccountMeta.ReadOnly(usdcMint, false),
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
            }, winner.KeyBytes);
        }

        public TransactionInstruction CancelMatch(PublicKey caller, ulong matchId) =>
            Instruction("cancel_match", new List<AccountMeta>
            {
                AccountMeta.ReadOnly(caller, true),
                AccountMeta.ReadOnly(ConfigAddress(), false),
                AccountMeta.Writable(MatchAddress(matchId), false),
            });

        public TransactionInstruction CloseMatch(
            PublicKey authority, ulong matchId, PublicKey treasury, PublicKey usdcMint)
        {
            var match = MatchAddress(matchId);
            return Instruction("close_match", new List<AccountMeta>
            {
                AccountMeta.Writable(authority, true),
                AccountMeta.ReadOnly(ConfigAddress(), false),
                AccountMeta.Writable(match, false),
                AccountMeta.Writable(VaultAddress(match), false),
                AccountMeta.Writable(treasury, false),
                AccountMeta.ReadOnly(usdcMint, false),
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
            });
        }

        // ---------- Helpers ----------

        /// <summary>Discriminador de instrucción de Anchor: sha256("global:&lt;nombre&gt;")[..8].</summary>
        public static byte[] InstructionDiscriminator(string name) => Sha256Prefix("global:" + name);

        /// <summary>Discriminador de cuenta de Anchor: sha256("account:&lt;Nombre&gt;")[..8].</summary>
        public static byte[] AccountDiscriminator(string name) => Sha256Prefix("account:" + name);

        internal static void CheckDiscriminator(byte[] data, byte[] expected, string accountName)
        {
            if (data == null || data.Length < 8 || !data.AsSpan(0, 8).SequenceEqual(expected))
                throw new ArgumentException($"Los datos no son una cuenta {accountName} del programa.");
        }

        private TransactionInstruction Instruction(string name, List<AccountMeta> keys, byte[] args = null)
        {
            var discriminator = InstructionDiscriminator(name);
            var data = new byte[8 + (args?.Length ?? 0)];
            discriminator.CopyTo(data, 0);
            args?.CopyTo(data, 8);
            return new TransactionInstruction { ProgramId = ProgramId.KeyBytes, Keys = keys, Data = data };
        }

        private PublicKey FindPda(params byte[][] seeds)
        {
            PublicKey.TryFindProgramAddress(seeds, ProgramId, out var address, out _);
            return address;
        }

        private static byte[] U64(ulong value)
        {
            var bytes = new byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
            return bytes;
        }

        private static byte[] Sha256Prefix(string preimage)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(preimage));
                var prefix = new byte[8];
                Array.Copy(hash, prefix, 8);
                return prefix;
            }
        }
    }
}
