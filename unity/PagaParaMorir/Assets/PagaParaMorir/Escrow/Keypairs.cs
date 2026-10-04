using System;
using System.Globalization;
using System.Linq;
using Solana.Unity.Wallet;

namespace PagaParaMorir.Escrow
{
    public static class Keypairs
    {
        /// <summary>
        /// Lee un keypair en el formato de Solana CLI: un JSON con 64 números
        /// (32 bytes de secreto + 32 de llave pública).
        /// </summary>
        public static Account FromSolanaCliJson(string json)
        {
            var body = (json ?? "").Trim();
            if (!body.StartsWith("[") || !body.EndsWith("]"))
                throw new ArgumentException("El archivo no es un keypair de Solana CLI.");
            byte[] bytes;
            try
            {
                bytes = body.Substring(1, body.Length - 2)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => byte.Parse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture))
                    .ToArray();
            }
            catch (FormatException)
            {
                throw new ArgumentException("El archivo no es un keypair de Solana CLI.");
            }
            catch (OverflowException)
            {
                throw new ArgumentException("El archivo no es un keypair de Solana CLI.");
            }
            if (bytes.Length != 64) throw new ArgumentException("El keypair debe tener 64 bytes.");
            return new Account(bytes, bytes.Skip(32).ToArray());
        }
    }
}
