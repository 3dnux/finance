using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PagaParaMorir.Escrow;
using UnityEngine;
using UnityEngine.UI;

namespace PagaParaMorir.Game.UI
{
    /// <summary>Saldo del jugador, sus partidas y las salas abiertas.</summary>
    public class LobbyScreen
    {
        private const ulong LamportsPerSol = 1_000_000_000;
        /// <summary>SOL mínimo recomendado para pagar comisiones de red.</summary>
        private const ulong MinSolForFees = 5_000_000;

        private readonly PagaParaMorirApp _app;
        private readonly CanvasGroup _group;
        private readonly Text _address;
        private readonly Text _usdc;
        private readonly Text _sol;
        private readonly Text _solHint;
        private readonly RectTransform _myMatches;
        private readonly RectTransform _openMatches;
        private readonly Text _openTitle;

        private bool _busy;
        private bool _refreshing;

        public LobbyScreen(PagaParaMorirApp app, RectTransform parent)
        {
            _app = app;
            var root = Ui.Stretch(Ui.Rect(parent, "LobbyScreen"));
            _group = root.gameObject.AddComponent<CanvasGroup>();
            var columns = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            columns.spacing = 32;
            columns.childControlWidth = columns.childControlHeight = true;
            columns.childForceExpandHeight = true;
            columns.childForceExpandWidth = false;

            // ---- Columna izquierda: billetera ----
            var wallet = Ui.Card(root, 16, 32, "Wallet");
            Ui.Size(wallet, minWidth: 560, preferredWidth: 560);
            Ui.Label(wallet, "Tu billetera", 36, Theme.Text, FontStyle.Bold);
            _address = Ui.Label(wallet, "", 24, Theme.Muted);
            Ui.Button(wallet, "Copiar dirección", CopyAddress, Theme.Secondary, height: 52, fontSize: 22);
            Ui.Label(wallet, "USDC", 22, Theme.Muted);
            _usdc = Ui.Label(wallet, "…", 48, Theme.Success, FontStyle.Bold);
            _sol = Ui.Label(wallet, "SOL: …", 24, Theme.Muted);
            _solHint = Ui.Label(wallet, "", 22, Theme.Warning);
            Ui.Label(wallet,
                "Para jugar, envía USDC (red Solana) a tu dirección desde Phantom u otra billetera, " +
                "y un poco de SOL (≈0.01) para las comisiones de red.", 22, Theme.Muted);
            Ui.Spacer(wallet);
            Ui.Button(wallet, "Actualizar", () => RefreshInBackground(), Theme.Secondary);
            if (_app.IsDevNetwork)
                Ui.Button(wallet, "Pedir 1 SOL de prueba", RequestAirdrop, Theme.Secondary);
            Ui.Button(wallet, "Cerrar sesión", _app.Logout, Theme.Secondary);

            // ---- Columna derecha: partidas ----
            var right = Ui.VStack(root, 16, 0, TextAnchor.UpperLeft, "Matches");
            Ui.Size(right, flexibleWidth: 1);
            _myMatches = Ui.VStack(right, 12, 0, TextAnchor.UpperLeft, "MyMatches");
            _openTitle = Ui.Label(right, "Salas abiertas", 36, Theme.Text, FontStyle.Bold);
            Ui.ScrollList(right, out _openMatches);

            _address.text = _app.Wallet.PublicKey?.Key ?? "";
        }

        /// <summary>Actualiza saldo y salas sin bloquear la pantalla.</summary>
        public async void RefreshInBackground()
        {
            try
            {
                await Refresh();
            }
            catch (EscrowException e)
            {
                _app.Status.Error(e.Message);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _app.Status.Error("No se pudo actualizar: " + e.Message);
            }
        }

        private async Task Refresh()
        {
            if (_refreshing || _busy || _app.Escrow == null) return;
            _refreshing = true;
            try
            {
                var escrow = _app.Escrow;
                var me = _app.Wallet.PublicKey;
                var config = await escrow.GetConfigAsync();
                var usdc = await escrow.GetUsdcBalanceAsync(me);
                var sol = await escrow.GetSolBalanceAsync(me);
                var matches = await escrow.GetMatchesAsync();
                if (_group == null || _app.Escrow != escrow) return; // la pantalla cambió mientras cargaba

                _usdc.text = Usdc.Format(usdc);
                _sol.text = $"SOL: {(decimal)sol / LamportsPerSol:0.####}";
                _solHint.text = sol < MinSolForFees ? "Necesitas un poco de SOL para pagar la red." : "";

                var mine = matches.Where(m => m.HasPlayer(me) && m.State != MatchState.Settled).ToList();
                var open = matches.Where(m => m.State == MatchState.Open && !m.HasPlayer(me) && !m.IsFull).ToList();
                RenderMyMatches(mine, config);
                RenderOpenMatches(open, config, usdc);
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void RenderMyMatches(List<MatchAccount> mine, ConfigAccount config)
        {
            Ui.Clear(_myMatches);
            if (mine.Count == 0) return;
            Ui.Label(_myMatches, "Mis partidas", 36, Theme.Text, FontStyle.Bold);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            foreach (var m in mine)
            {
                var row = Row(_myMatches, $"Sala #{m.MatchId}");
                switch (m.State)
                {
                    case MatchState.Open:
                        Detail(row.info, $"Esperando jugadores · {m.Players.Count}/{m.MaxPlayers} · Pozo {Usdc.Format(m.Pot)}");
                        Ui.Button(row.actions, $"Salir y recuperar {Usdc.Format(m.EntryFee)}",
                            () => Leave(m), Theme.Secondary);
                        break;
                    case MatchState.InProgress when EscrowClient.IsSettleExpired(m, config, now):
                        Detail(row.info, "El servidor no reportó ganador a tiempo. Puedes cancelar y recuperar tu entrada.", Theme.Warning);
                        Ui.Button(row.actions, "Cancelar partida", () => CancelExpired(m), Theme.Secondary);
                        break;
                    case MatchState.InProgress:
                        Detail(row.info, $"En juego · Premio {Usdc.Format(config.PrizeFor(m.Pot))}", Theme.Warning);
                        break;
                    case MatchState.Cancelled:
                        Detail(row.info, "Partida cancelada. Tu entrada está lista para reclamar.", Theme.Warning);
                        Ui.Button(row.actions, $"Reclamar {Usdc.Format(m.EntryFee)}", () => ClaimRefund(m));
                        break;
                }
            }
        }

        private void RenderOpenMatches(List<MatchAccount> open, ConfigAccount config, ulong usdcBalance)
        {
            Ui.Clear(_openMatches);
            _openTitle.text = open.Count == 0 ? "Salas abiertas" : $"Salas abiertas ({open.Count})";
            if (config.Paused)
            {
                Ui.Label(_openMatches, "El juego está en pausa.", 26, Theme.Warning);
                return;
            }
            if (open.Count == 0)
            {
                Ui.Label(_openMatches, "No hay salas abiertas ahora. Se actualiza solo.", 26, Theme.Muted);
                return;
            }

            foreach (var m in open.OrderBy(m => m.EntryFee).ThenByDescending(m => m.Players.Count))
            {
                var row = Row(_openMatches, $"Sala #{m.MatchId} · Entrada {Usdc.Format(m.EntryFee)}");
                var prizeIfJoin = config.PrizeFor(m.EntryFee * (ulong)(m.Players.Count + 1));
                var prizeFull = config.PrizeFor(m.EntryFee * m.MaxPlayers);
                Detail(row.info, $"{m.Players.Count}/{m.MaxPlayers} jugadores · " +
                                 $"Premio si entras: {Usdc.Format(prizeIfJoin)} · Con sala llena: {Usdc.Format(prizeFull)}");
                var button = Ui.Button(row.actions, "Pagar y entrar", () => ConfirmJoin(m, config));
                button.interactable = usdcBalance >= m.EntryFee;
            }
        }

        // ---------- Acciones ----------

        private void ConfirmJoin(MatchAccount m, ConfigAccount config)
        {
            var feePercent = config.FeeBps / 100m;
            Dialog.Confirm(_app.CanvasRoot,
                $"Entrar a la Sala #{m.MatchId}",
                $"Vas a pagar {Usdc.Format(m.EntryFee)}. Si no ganas, no se devuelve.\n\n" +
                $"El ganador se lleva el pozo menos {feePercent:0.#}% de comisión. " +
                "Mientras la sala no empiece puedes salirte y recuperar tu entrada.",
                $"Pagar {Usdc.Format(m.EntryFee)}",
                () => Run($"Pagando entrada de la Sala #{m.MatchId}…",
                    () => _app.Escrow.JoinMatchAsync(_app.Wallet.PublicKey, m.MatchId, _app.Wallet.SignAndSend),
                    $"¡Estás dentro de la Sala #{m.MatchId}! Espera a que se llene."));
        }

        private void Leave(MatchAccount m) =>
            Run($"Saliendo de la Sala #{m.MatchId}…",
                () => _app.Escrow.LeaveMatchAsync(_app.Wallet.PublicKey, m.MatchId, _app.Wallet.SignAndSend),
                $"Saliste de la Sala #{m.MatchId}. Te devolvimos {Usdc.Format(m.EntryFee)}.");

        private void ClaimRefund(MatchAccount m) =>
            Run($"Reclamando reembolso de la Sala #{m.MatchId}…",
                () => _app.Escrow.ClaimRefundAsync(_app.Wallet.PublicKey, m.MatchId, _app.Wallet.SignAndSend),
                $"Recuperaste {Usdc.Format(m.EntryFee)}.");

        private void CancelExpired(MatchAccount m) =>
            Run($"Cancelando la Sala #{m.MatchId}…",
                () => _app.Escrow.CancelExpiredMatchAsync(_app.Wallet.PublicKey, m.MatchId, _app.Wallet.SignAndSend),
                "Partida cancelada. Ya puedes reclamar tu entrada.");

        private void RequestAirdrop() =>
            Run("Pidiendo SOL de prueba…", async () =>
            {
                var result = await _app.Wallet.RequestAirdrop(LamportsPerSol);
                if (!result.WasSuccessful)
                    throw new EscrowException("La red de prueba no dio SOL ahora (límite de uso). Intenta más tarde.");
                await _app.Escrow.ConfirmAsync(result.Result);
                return result.Result;
            }, "Recibiste 1 SOL de prueba.");

        private void CopyAddress()
        {
            GUIUtility.systemCopyBuffer = _app.Wallet.PublicKey?.Key ?? "";
            _app.Status.Success("Dirección copiada.");
        }

        private async void Run(string pending, Func<Task<string>> action, string success)
        {
            if (_busy) return;
            _busy = true;
            _group.interactable = false;
            _app.Status.Info(pending);
            try
            {
                await action();
                _app.Status.Success(success);
            }
            catch (EscrowException e)
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
                _busy = false;
                if (_group != null) _group.interactable = true;
            }
            RefreshInBackground();
        }

        // ---------- Filas ----------

        private static (RectTransform info, RectTransform actions) Row(Transform parent, string title)
        {
            var card = Ui.Panel(parent, Theme.PanelLight, "Row");
            var layout = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 18, 18);
            layout.spacing = 24;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var info = Ui.VStack(card.transform, 6, 0, TextAnchor.MiddleLeft, "Info");
            Ui.Size(info, flexibleWidth: 1);
            Ui.Label(info, title, 30, Theme.Text, FontStyle.Bold);
            var actions = Ui.HStack(card.transform, 12, 0, TextAnchor.MiddleRight, "Actions");
            return (info, actions);
        }

        private static void Detail(Transform info, string text, Color? color = null) =>
            Ui.Label(info, text, 22, color ?? Theme.Muted);
    }
}
