using System;
using UnityEngine;

namespace PagaParaMorir.Game.UI
{
    /// <summary>Ventana modal de confirmación.</summary>
    public static class Dialog
    {
        public static void Confirm(Transform canvas, string title, string body, string confirmText,
            Action onConfirm, string cancelText = "Cancelar")
        {
            var overlay = Ui.Panel(canvas, Theme.Overlay, "Dialog");
            Ui.Stretch(overlay.rectTransform);
            overlay.transform.SetAsLastSibling();

            var card = Ui.Card(overlay.transform, 24, 40);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(820, 0);
            card.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            Ui.Label(card, title, 40, Theme.Text, FontStyle.Bold);
            Ui.Label(card, body, 26, Theme.Muted);

            var buttons = Ui.HStack(card, 16, 0, TextAnchor.MiddleRight);
            Ui.Spacer(buttons);
            Ui.Button(buttons, cancelText, () => UnityEngine.Object.Destroy(overlay.gameObject), Theme.Secondary);
            Ui.Button(buttons, confirmText, () =>
            {
                UnityEngine.Object.Destroy(overlay.gameObject);
                onConfirm?.Invoke();
            });
        }
    }
}
