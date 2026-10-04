using System.Collections.Generic;
using System.Linq;
using PagaParaMorir.Game.UI;
using PagaParaMorir.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace PagaParaMorir.Game.Match
{
    /// <summary>Interfaz durante la partida: vida, arma, zona, bajas y resultado.</summary>
    public class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }

        private const int KillFeedLines = 5;
        private const float KillFeedSeconds = 8f;

        private GameSession _session;
        private Text _banner;
        private Text _timer;
        private Text _alive;
        private Text _center;
        private Text _warning;
        private Text _health;
        private RectTransform _healthFill;
        private Text _weapon;
        private Text _slots;
        private Text _feed;
        private Text _hitMarker;
        private Text _crosshair;
        private GameObject _pausePanel;
        private GameObject _endPanel;
        private Text _endTitle;
        private Text _endBody;
        private float _hitMarkerUntil;
        private readonly List<(string text, float until)> _feedLines = new List<(string, float)>();

        public static Hud Create(GameSession session)
        {
            var canvas = Ui.CreateCanvas(null, "Hud");
            canvas.sortingOrder = 10;
            var hud = canvas.gameObject.AddComponent<Hud>();
            hud._session = session;
            hud.Build(canvas.transform);
            Instance = hud;
            MatchController.KillFeed += hud.OnKill;
            return hud;
        }

        public void Close()
        {
            MatchController.KillFeed -= OnKill;
            if (Instance == this) Instance = null;
            Destroy(gameObject);
        }

        public void ShowHitMarker(bool eliminated)
        {
            _hitMarker.color = eliminated ? Theme.Accent : Color.white;
            _hitMarkerUntil = Time.unscaledTime + (eliminated ? 0.5f : 0.15f);
        }

        private void OnKill(string text) => _feedLines.Add((text, Time.unscaledTime + KillFeedSeconds));

        private void Build(Transform root)
        {
            _crosshair = Anchored(root, "+", 40, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, Color.white, new Vector2(80, 80));
            _hitMarker = Anchored(root, "×", 56, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, Color.white, new Vector2(80, 80));

            _banner = Anchored(root, "", 28, new Vector2(0.5f, 1f), TextAnchor.UpperCenter, Theme.Text, new Vector2(1400, 80), new Vector2(0, -24));
            _timer = Anchored(root, "", 34, new Vector2(0.5f, 1f), TextAnchor.UpperCenter, Theme.Warning, new Vector2(800, 50), new Vector2(0, -100));
            _alive = Anchored(root, "", 34, new Vector2(1f, 1f), TextAnchor.UpperRight, Theme.Text, new Vector2(400, 50), new Vector2(-32, -24));
            _center = Anchored(root, "", 140, new Vector2(0.5f, 0.62f), TextAnchor.MiddleCenter, Theme.Text, new Vector2(1200, 200));
            _warning = Anchored(root, "", 36, new Vector2(0.5f, 0.38f), TextAnchor.MiddleCenter, Theme.Accent, new Vector2(1200, 60));
            _feed = Anchored(root, "", 24, new Vector2(0f, 0.75f), TextAnchor.UpperLeft, Theme.Text, new Vector2(700, 220), new Vector2(32, 0));

            // Vida
            _health = Anchored(root, "", 40, new Vector2(0f, 0f), TextAnchor.LowerLeft, Theme.Text, new Vector2(500, 60), new Vector2(32, 80));
            var barBack = Ui.Panel(root, new Color(0, 0, 0, 0.5f), "HealthBar");
            SetAnchored(barBack.rectTransform, new Vector2(0f, 0f), new Vector2(420, 22), new Vector2(32, 44), new Vector2(0, 0));
            var fill = Ui.Panel(barBack.transform, Theme.Success, "Fill");
            _healthFill = fill.rectTransform;
            _healthFill.anchorMin = Vector2.zero;
            _healthFill.anchorMax = new Vector2(1, 1);
            _healthFill.pivot = new Vector2(0, 0.5f);
            _healthFill.offsetMin = _healthFill.offsetMax = Vector2.zero;

            // Arma
            _weapon = Anchored(root, "", 40, new Vector2(1f, 0f), TextAnchor.LowerRight, Theme.Text, new Vector2(700, 60), new Vector2(-32, 80));
            _slots = Anchored(root, "", 22, new Vector2(1f, 0f), TextAnchor.LowerRight, Theme.Muted, new Vector2(900, 40), new Vector2(-32, 40));

            Anchored(root, "WASD moverse · Ratón apuntar · Clic disparar · Espacio saltar · R recargar · 1-4 armas · Esc menú",
                18, new Vector2(0.5f, 0f), TextAnchor.LowerCenter, Theme.Muted, new Vector2(1400, 30), new Vector2(0, 12));

            _pausePanel = BuildPanel(root, "Pausa", out _, out _,
                ("Volver a jugar", () => PlayerInputReader.LockCursor(true)),
                ("Salir de la partida", () => _session.Leave()));
            _endPanel = BuildPanel(root, "", out _endTitle, out _endBody,
                ("Volver al lobby", () => _session.Leave()));
        }

        private void Update()
        {
            var match = MatchController.Instance;
            var me = NetworkPlayer.Local;
            var phase = match != null ? match.Phase.Value : MatchPhase.Waiting;
            var ended = phase == MatchPhase.Finished || phase == MatchPhase.Cancelled;

            _banner.text = match != null ? match.Banner.Value.ToString() : "Conectando con el servidor…";
            _alive.text = match == null ? "" : phase == MatchPhase.Waiting
                ? $"En sala: {match.AliveCount.Value}"
                : $"Vivos: {match.AliveCount.Value}/{match.PlayerCount.Value}";

            var secondsLeft = match != null ? Mathf.CeilToInt(match.SecondsLeft.Value) : 0;
            _timer.text = phase == MatchPhase.Waiting ? $"Empieza en {Clock(secondsLeft)} o al llenarse"
                : phase == MatchPhase.Playing ? $"Zona: {match.ZoneRadius.Value:0} m · Tiempo {Clock(secondsLeft)}"
                : "";
            _center.text = phase == MatchPhase.Countdown ? Mathf.Max(1, secondsLeft).ToString()
                : me != null && !me.Alive.Value && !ended ? "ELIMINADO"
                : "";

            var outside = me != null && me.Alive.Value && phase == MatchPhase.Playing && match != null &&
                          new Vector2(me.transform.position.x, me.transform.position.z).magnitude > match.ZoneRadius.Value;
            _warning.text = outside ? "¡Fuera de la zona! Vuelve al círculo rojo" : "";

            if (me != null)
            {
                var health = Mathf.Max(0, me.Health.Value);
                _health.text = $"Vida {health}";
                _healthFill.anchorMax = new Vector2(health / 100f, 1);
                var def = me.CurrentWeapon;
                _weapon.text = me.Reloading.Value ? $"{def.Name} · Recargando…" : $"{def.Name} · {me.Ammo.Value}/{def.MagazineSize}";
                _slots.text = string.Join("   ", Weapons.All.Select((w, i) =>
                    i == me.Weapon.Value ? $"[{i + 1} {w.Name}]" : $"{i + 1} {w.Name}"));
            }

            _hitMarker.enabled = Time.unscaledTime < _hitMarkerUntil;
            _feedLines.RemoveAll(l => Time.unscaledTime > l.until);
            _feed.text = string.Join("\n", _feedLines.Skip(System.Math.Max(0, _feedLines.Count - KillFeedLines)).Select(l => l.text));

            _crosshair.enabled = !ended && me != null && me.Alive.Value;
            _endPanel.SetActive(ended);
            if (ended)
            {
                if (PlayerInputReader.CursorLocked) PlayerInputReader.LockCursor(false);
                var winner = match.WinnerId.Value.ToString();
                _endTitle.text = phase == MatchPhase.Cancelled ? "Partida cancelada"
                    : me != null && winner == me.Id ? "¡GANASTE!"
                    : $"Ganó {Visuals.ShortId(winner)}";
                _endTitle.color = phase == MatchPhase.Finished && me != null && winner == me.Id ? Theme.Success : Theme.Text;
                _endBody.text = match.Banner.Value.ToString();
            }
            _pausePanel.SetActive(!ended && me != null && !PlayerInputReader.CursorLocked);
        }

        private static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        private static Text Anchored(Transform parent, string text, int size, Vector2 anchor, TextAnchor align,
            Color color, Vector2 boxSize, Vector2? offset = null)
        {
            var label = Ui.Label(parent, text, size, color, FontStyle.Bold, align);
            label.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
            SetAnchored(label.rectTransform, anchor, boxSize, offset ?? Vector2.zero, anchor);
            return label;
        }

        private static void SetAnchored(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset, Vector2 pivot)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        private static GameObject BuildPanel(Transform root, string title, out Text titleLabel, out Text body,
            params (string label, System.Action action)[] buttons)
        {
            var overlay = Ui.Panel(root, Theme.Overlay, "Panel");
            Ui.Stretch(overlay.rectTransform);
            var card = Ui.Card(overlay.transform, 20, 40);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(760, 0);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            titleLabel = Ui.Label(card, title, 56, Theme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            body = Ui.Label(card, "", 26, Theme.Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            foreach (var (label, action) in buttons) Ui.Button(card, label, action);
            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }
    }
}
