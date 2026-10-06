using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace PagaParaMorir.Game.UI
{
    /// <summary>Abrir, crear o restaurar la billetera del juego.</summary>
    public class WalletScreen
    {
        private const int MinPasswordLength = 8;

        private readonly PagaParaMorirApp _app;
        private readonly RectTransform _card;
        private readonly CanvasGroup _group;

        public WalletScreen(PagaParaMorirApp app, RectTransform parent)
        {
            _app = app;
            var root = Ui.Stretch(Ui.Rect(parent, "WalletScreen"));
            _group = root.gameObject.AddComponent<CanvasGroup>();

            _card = Ui.Card(root, 20, 48);
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(900, 0);
            _card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (_app.Wallet.HasSavedWallet) ShowLogin();
            else ShowChoose();
        }

        private void ShowLogin()
        {
            Ui.Clear(_card);
            Ui.Label(_card, "Abre tu billetera", 44, Theme.Text, FontStyle.Bold);
            Ui.Label(_card, "Está guardada y cifrada en este equipo.", 26, Theme.Muted);
            var password = Ui.Input(_card, "Contraseña", password: true);
            Ui.Button(_card, "Entrar", () => Run(async () =>
            {
                if (await _app.Wallet.Login(password.text))
                {
                    _app.Status.Success("Billetera abierta.");
                    _app.OnLoggedIn();
                }
                else _app.Status.Error("Contraseña incorrecta.");
            }));
            Ui.Button(_card, "Usar otra billetera", ShowChoose, Theme.Secondary);
        }

        private void AddWalletAppButton()
        {
            if (!WalletService.CanUseWalletApp) return;
            Ui.Button(_card, "Conectar Phantom / Solflare", () => Run(async () =>
            {
                if (await _app.Wallet.ConnectWalletApp())
                {
                    _app.Status.Success("Billetera conectada.");
                    _app.OnLoggedIn();
                }
                else _app.Status.Error("No se conectó la billetera. ¿Tienes Phantom o Solflare instalada?");
            }));
        }

        private void ShowChoose()
        {
            Ui.Clear(_card);
            Ui.Label(_card, "Tu billetera del juego", 44, Theme.Text, FontStyle.Bold);
            Ui.Label(_card,
                "Aquí guardas el USDC con el que pagas tus entradas y recibes tus premios. " +
                "Puedes mandarle USDC desde Phantom o cualquier billetera de Solana.", 26, Theme.Muted);
            if (_app.Wallet.HasSavedWallet)
                Ui.Label(_card, "Ojo: crear o restaurar otra reemplaza la billetera guardada en este equipo. " +
                                "Asegúrate de tener sus 12 palabras.", 24, Theme.Warning);
            AddWalletAppButton();
            Ui.Button(_card, "Crear billetera nueva", ShowCreate);
            Ui.Button(_card, "Restaurar con mis 12 palabras", ShowImport, Theme.Secondary);
            if (_app.Wallet.HasSavedWallet) Ui.Button(_card, "Volver", ShowLogin, Theme.Secondary);
        }

        private void ShowCreate()
        {
            Ui.Clear(_card);
            Ui.Label(_card, "Crea una contraseña", 44, Theme.Text, FontStyle.Bold);
            Ui.Label(_card, $"Protege la billetera en este equipo. Mínimo {MinPasswordLength} caracteres.", 26, Theme.Muted);
            var password = Ui.Input(_card, "Contraseña", password: true);
            var repeat = Ui.Input(_card, "Repite la contraseña", password: true);
            Ui.Button(_card, "Crear billetera", () =>
            {
                if (!ValidPassword(password.text, repeat.text)) return;
                Run(async () => ShowMnemonic(await _app.Wallet.Create(password.text)));
            });
            Ui.Button(_card, "Volver", ShowChoose, Theme.Secondary);
        }

        private void ShowMnemonic(string mnemonic)
        {
            Ui.Clear(_card);
            Ui.Label(_card, "Anota estas 12 palabras", 44, Theme.Text, FontStyle.Bold);
            Ui.Label(_card,
                "Son la única forma de recuperar tu dinero si pierdes este equipo o la contraseña. " +
                "Escríbelas en papel, en orden. Nunca se las des a nadie: quien las tenga se queda con tu USDC.",
                26, Theme.Warning);

            var grid = Ui.Panel(_card, Theme.Input, "Words");
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(260, 52);
            layout.spacing = new Vector2(12, 12);
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            var words = (mnemonic ?? "").Split(' ');
            for (var i = 0; i < words.Length; i++)
                Ui.Label(grid.transform, $"{i + 1}. {words[i]}", 28, Theme.Text, FontStyle.Bold);
            Ui.Size(grid, height: 20 * 2 + Mathf.CeilToInt(words.Length / 3f) * 64);

            Ui.Button(_card, "Ya las anoté en papel", () =>
            {
                _app.Status.Success("Billetera creada.");
                _app.OnLoggedIn();
            });
        }

        private void ShowImport()
        {
            Ui.Clear(_card);
            Ui.Label(_card, "Restaura tu billetera", 44, Theme.Text, FontStyle.Bold);
            Ui.Label(_card, "Escribe tus 12 o 24 palabras separadas por espacios.", 26, Theme.Muted);
            var phrase = Ui.Input(_card, "palabra1 palabra2 palabra3 …", multiline: true, height: 140);
            var password = Ui.Input(_card, "Nueva contraseña para este equipo", password: true);
            var repeat = Ui.Input(_card, "Repite la contraseña", password: true);
            Ui.Button(_card, "Restaurar", () =>
            {
                if (!ValidPassword(password.text, repeat.text)) return;
                Run(async () =>
                {
                    await _app.Wallet.Import(phrase.text, password.text);
                    _app.Status.Success("Billetera restaurada.");
                    _app.OnLoggedIn();
                });
            });
            Ui.Button(_card, "Volver", ShowChoose, Theme.Secondary);
        }

        private bool ValidPassword(string password, string repeat)
        {
            if (password.Length < MinPasswordLength)
            {
                _app.Status.Error($"La contraseña debe tener al menos {MinPasswordLength} caracteres.");
                return false;
            }
            if (password != repeat)
            {
                _app.Status.Error("Las contraseñas no coinciden.");
                return false;
            }
            return true;
        }

        private async void Run(Func<Task> action)
        {
            if (!_group.interactable) return;
            _group.interactable = false;
            _app.Status.Info("Un momento…");
            try
            {
                await action();
            }
            catch (ArgumentException e)
            {
                _app.Status.Error(e.Message);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _app.Status.Error("Algo salió mal: " + e.Message);
            }
            finally
            {
                // La pantalla puede haberse destruido al pasar al lobby.
                if (_group != null) _group.interactable = true;
            }
        }
    }
}
