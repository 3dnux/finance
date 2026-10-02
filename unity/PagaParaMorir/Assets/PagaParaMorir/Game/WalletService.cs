using System;
using System.Threading.Tasks;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Models;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using UnityEngine;

namespace PagaParaMorir.Game
{
    /// <summary>
    /// Billetera dentro del juego (InGameWallet del Solana Unity SDK).
    /// En PC no hay conexión directa con Phantom: el jugador tiene una billetera propia,
    /// cifrada con su contraseña en este equipo, y le manda USDC desde la que ya use.
    /// </summary>
    public class WalletService
    {
        // Misma clave que usa InGameWallet para guardar el keystore cifrado.
        private const string KeystoreKey = "EncryptedKeystore";

        public bool HasSavedWallet => PlayerPrefs.HasKey(KeystoreKey);

        public bool IsLoggedIn => Web3.Account != null;

        public PublicKey PublicKey => Web3.Account?.PublicKey;

        /// <summary>Abre la billetera guardada. Devuelve <c>false</c> si la contraseña no es correcta.</summary>
        public async Task<bool> Login(string password)
        {
            var account = await Web3.Instance.LoginInGameWallet(password);
            return account != null;
        }

        /// <summary>Crea una billetera nueva y devuelve sus 12 palabras de respaldo.</summary>
        public async Task<string> Create(string password)
        {
            var previous = PlayerPrefs.GetString(KeystoreKey, "");
            var account = await Web3.Instance.CreateAccount(null, password);
            if (account == null) throw new InvalidOperationException("No se pudo crear la billetera.");
            await PersistKeystore(previous);
            return Web3.Wallet.Mnemonic?.ToString();
        }

        /// <summary>Restaura una billetera desde sus 12 o 24 palabras.</summary>
        public async Task Import(string mnemonic, string password)
        {
            var words = string.Join(" ", mnemonic.Trim().ToLowerInvariant()
                .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            var count = words.Split(' ').Length;
            if (count != 12 && count != 24)
                throw new ArgumentException("La frase debe tener 12 o 24 palabras.");

            var previous = PlayerPrefs.GetString(KeystoreKey, "");
            Account account;
            try
            {
                account = await Web3.Instance.CreateAccount(words, password);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
                throw new ArgumentException("La frase secreta no es válida.");
            }
            if (account == null) throw new ArgumentException("La frase secreta no es válida.");
            await PersistKeystore(previous);
        }

        public void Logout() => Web3.Instance.Logout();

        public Task<RequestResult<string>> SignAndSend(Transaction transaction) =>
            Web3.Wallet.SignAndSendTransaction(transaction);

        public Task<RequestResult<string>> RequestAirdrop(ulong lamports) =>
            Web3.Wallet.RequestAirdrop(lamports);

        /// <summary>
        /// InGameWallet guarda el keystore con un pequeño retraso y en PC no llama a
        /// PlayerPrefs.Save(); lo forzamos para no perder la billetera si el juego se cierra mal.
        /// </summary>
        private static async Task PersistKeystore(string previous)
        {
            for (var i = 0; i < 30 && PlayerPrefs.GetString(KeystoreKey, "") == previous; i++)
                await Task.Delay(100);
            PlayerPrefs.Save();
        }
    }
}
