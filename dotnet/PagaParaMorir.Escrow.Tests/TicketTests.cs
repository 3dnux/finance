using System.Linq;
using PagaParaMorir.Escrow;
using Solana.Unity.Wallet;
using Xunit;

namespace PagaParaMorir.Escrow.Tests
{
    public class TicketTests
    {
        private const long Now = 1_800_000_000;

        [Fact]
        public void Boleto_valido_ida_y_vuelta()
        {
            var player = new Account();
            var bytes = JoinTicket.Create(7, Now, player).Encode();
            Assert.Equal(JoinTicket.EncodedLength, bytes.Length);

            var ticket = JoinTicket.Decode(bytes);
            Assert.Equal(7UL, ticket.MatchId);
            Assert.Equal(player.PublicKey, ticket.Player);
            Assert.True(ticket.Verify(7, Now + 10, 120, out var error), error);
        }

        [Fact]
        public void Rechaza_otra_partida_boletos_viejos_y_firmas_falsas()
        {
            var player = new Account();
            var ticket = JoinTicket.Create(7, Now, player);
            Assert.False(ticket.Verify(8, Now, 120, out var error));
            Assert.Equal("El boleto es de otra partida.", error);
            Assert.False(ticket.Verify(7, Now + 121, 120, out error));
            Assert.Equal("El boleto expiró. Vuelve a intentarlo.", error);

            // Alguien copia el boleto y pone su propia llave: la firma ya no coincide.
            var forged = ticket.Encode();
            new Account().PublicKey.KeyBytes.CopyTo(forged, 17);
            Assert.False(JoinTicket.Decode(forged).Verify(7, Now, 120, out error));
            Assert.Equal("Firma inválida.", error);
        }

        [Fact]
        public void Ignora_bytes_basura()
        {
            Assert.Null(JoinTicket.Decode(null));
            Assert.Null(JoinTicket.Decode(new byte[10]));
            Assert.Null(JoinTicket.Decode(new byte[JoinTicket.EncodedLength]));
        }

        [Fact]
        public void Lee_keypair_de_solana_cli()
        {
            var original = new Account();
            var json = "[" + string.Join(",", original.PrivateKey.KeyBytes.Take(32)
                .Concat(original.PublicKey.KeyBytes)) + "]";
            var loaded = Keypairs.FromSolanaCliJson(json);
            Assert.Equal(original.PublicKey, loaded.PublicKey);

            var message = new byte[] { 1, 2, 3 };
            Assert.True(loaded.PublicKey.Verify(message, loaded.Sign(message)));
        }

        [Fact]
        public void Rechaza_keypairs_invalidos()
        {
            Assert.Throws<System.ArgumentException>(() => Keypairs.FromSolanaCliJson("hola"));
            Assert.Throws<System.ArgumentException>(() => Keypairs.FromSolanaCliJson("[1,2,3]"));
            Assert.Throws<System.ArgumentException>(() => Keypairs.FromSolanaCliJson("[" + string.Join(",", Enumerable.Repeat("300", 64)) + "]"));
        }
    }
}
