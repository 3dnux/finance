using UnityEngine;
using UnityEngine.UI;

namespace PagaParaMorir.Game.UI
{
    /// <summary>Línea de estado al pie de la pantalla.</summary>
    public class StatusBar
    {
        private readonly Text _label;

        public StatusBar(Transform parent)
        {
            var panel = Ui.Panel(parent, Theme.Panel, "StatusBar");
            Ui.Size(panel, height: 56);
            _label = Ui.Label(panel.transform, "", 24, Theme.Muted);
            Ui.Stretch(_label.rectTransform);
            _label.rectTransform.offsetMin = new Vector2(24, 0);
            _label.rectTransform.offsetMax = new Vector2(-24, 0);
        }

        public void Info(string message) => Show(message, Theme.Muted);
        public void Success(string message) => Show(message, Theme.Success);
        public void Error(string message) => Show(message, Theme.Accent);
        public void Clear() => Show("", Theme.Muted);

        private void Show(string message, Color color)
        {
            _label.text = message;
            _label.color = color;
        }
    }
}
