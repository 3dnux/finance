using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text.Json;
using PagaParaMorir.Escrow;
using Solana.Unity.Wallet;
using Xunit;

namespace PagaParaMorir.Escrow.Tests
{
    /// <summary>Pruebas sin red: el cliente C# debe coincidir con el IDL del programa.</summary>
    public class EncodingTests
    {
        private static readonly JsonElement Idl = LoadIdl();

        private static JsonElement LoadIdl()
        {
            var path = Path.Combine(RepoRoot.Path, "idl", "paga_para_morir.json");
            return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        }

        private static byte[] Discriminator(string section, string name) =>
            Idl.GetProperty(section).EnumerateArray()
                .First(e => e.GetProperty("name").GetString() == name)
                .GetProperty("discriminator").EnumerateArray()
                .Select(b => (byte)b.GetInt32()).ToArray();

        [Theory]
        [InlineData("join_match")]
        [InlineData("leave_match")]
        [InlineData("claim_refund")]
        [InlineData("initialize_config")]
        [InlineData("create_match")]
        [InlineData("start_match")]
        [InlineData("settle_match")]
        [InlineData("cancel_match")]
        [InlineData("close_match")]
        public void Discriminador_de_instruccion_coincide_con_el_IDL(string name)
        {
            Assert.Equal(Discriminator("instructions", name), EscrowProgram.InstructionDiscriminator(name));
        }

        [Theory]
        [InlineData("Config")]
        [InlineData("Match")]
        public void Discriminador_de_cuenta_coincide_con_el_IDL(string name)
        {
            Assert.Equal(Discriminator("accounts", name), EscrowProgram.AccountDiscriminator(name));
        }

        [Fact]
        public void Program_id_coincide_con_el_IDL()
        {
            Assert.Equal(EscrowProgram.DefaultProgramId, Idl.GetProperty("address").GetString());
        }

        [Fact]
        public void Cuentas_de_join_match_en_el_orden_del_IDL()
        {
            var program = new EscrowProgram();
            var player = new Account().PublicKey;
            var mint = new Account().PublicKey;
            var ix = program.JoinMatch(player, 42, mint);

            var expected = Idl.GetProperty("instructions").EnumerateArray()
                .First(e => e.GetProperty("name").GetString() == "join_match")
                .GetProperty("accounts").EnumerateArray().ToList();
            Assert.Equal(expected.Count, ix.Keys.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                var writable = expected[i].TryGetProperty("writable", out var w) && w.GetBoolean();
                var signer = expected[i].TryGetProperty("signer", out var s) && s.GetBoolean();
                Assert.Equal(writable, ix.Keys[i].IsWritable);
                Assert.Equal(signer, ix.Keys[i].IsSigner);
            }
            Assert.Equal(player.Key, ix.Keys[0].PublicKey);
            Assert.Equal(program.MatchAddress(42).Key, ix.Keys[2].PublicKey);
            Assert.Equal(EscrowProgram.UsdcAccountOf(player, mint).Key, ix.Keys[4].PublicKey);
        }

        [Fact]
        public void Decodifica_una_cuenta_Match()
        {
            var p1 = new Account().PublicKey;
            var p2 = new Account().PublicKey;
            var data = new byte[80 + 32 * 2];
            EscrowProgram.AccountDiscriminator("Match").CopyTo(data, 0);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(8), 7);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(16), 5 * Usdc.OneUsdc);
            data[24] = 10;
            data[25] = (byte)MatchState.InProgress;
            BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(26), 1_800_000_000);
            BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(34), 1_800_000_060);
            data[74] = 254;
            data[75] = 253;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(76), 2);
            p1.KeyBytes.CopyTo(data, 80);
            p2.KeyBytes.CopyTo(data, 112);

            var m = MatchAccount.Deserialize(data);
            Assert.Equal(7UL, m.MatchId);
            Assert.Equal(5 * Usdc.OneUsdc, m.EntryFee);
            Assert.Equal(10, m.MaxPlayers);
            Assert.Equal(MatchState.InProgress, m.State);
            Assert.Equal(1_800_000_060, m.StartedAt);
            Assert.Null(m.Winner);
            Assert.Equal(new[] { p1.Key, p2.Key }, m.Players.Select(p => p.Key));
            Assert.Equal(10 * Usdc.OneUsdc, m.Pot);
            Assert.True(m.HasPlayer(p2));
        }

        [Fact]
        public void Rechaza_datos_de_otra_cuenta()
        {
            var data = new byte[200];
            EscrowProgram.AccountDiscriminator("Config").CopyTo(data, 0);
            Assert.Throws<ArgumentException>(() => MatchAccount.Deserialize(data));
        }

        [Fact]
        public void Premio_es_80_por_ciento_con_comision_de_20()
        {
            var config = new ConfigAccount { FeeBps = 2000 };
            Assert.Equal(40 * Usdc.OneUsdc, config.PrizeFor(50 * Usdc.OneUsdc));
            Assert.Equal(6UL, config.PrizeFor(7)); // la comisión se redondea hacia abajo
        }

        [Theory]
        [InlineData("Program log: AnchorError occurred. Error Code: AlreadyJoined. Error Number: 6007. Error Message: x.", EscrowErrorCode.AlreadyJoined)]
        [InlineData("Program 2NB9 failed: custom program error: 0x1776", EscrowErrorCode.MatchFull)]
        public void Encuentra_el_codigo_de_error_en_los_logs(string log, EscrowErrorCode expected)
        {
            Assert.Equal(expected, EscrowErrors.FindCode(new[] { "otra línea", log }));
        }

        [Fact]
        public void Ignora_errores_que_no_son_del_programa()
        {
            // 0x1 es "insufficient funds" del programa de tokens, no un error del escrow.
            Assert.Null(EscrowErrors.FindCode(new[] { "custom program error: 0x1" }));
            var e = EscrowErrors.FromFailure("x", new[] { "Program log: Error: insufficient funds" });
            Assert.Equal("No tienes suficiente USDC.", e.Message);
        }

        [Theory]
        [InlineData(5_000_000UL, "5.00 USDC")]
        [InlineData(1_234_567UL, "1.234567 USDC")]
        [InlineData(0UL, "0.00 USDC")]
        public void Formatea_USDC(ulong units, string expected)
        {
            Assert.Equal(expected, Usdc.Format(units));
        }
    }
}
