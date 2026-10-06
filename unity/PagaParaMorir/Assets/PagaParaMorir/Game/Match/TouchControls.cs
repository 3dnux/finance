using PagaParaMorir.Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PagaParaMorir.Game.Match
{
    /// <summary>
    /// Estado de los controles táctiles. Lo escriben los botones en pantalla y lo lee
    /// <see cref="PlayerInputReader"/> junto con teclado y ratón.
    /// </summary>
    public static class TouchInput
    {
        /// <summary>Hay controles táctiles en pantalla.</summary>
        public static bool Active;
        public static Vector2 Move;
        public static bool FireHeld;

        private static Vector2 _look;
        private static bool _jump;
        private static bool _reload;
        private static bool _nextWeapon;
        private static bool _menu;

        public static void AddLook(Vector2 pixels) => _look += pixels;
        public static void PressJump() => _jump = true;
        public static void PressReload() => _reload = true;
        public static void PressNextWeapon() => _nextWeapon = true;
        public static void PressMenu() => _menu = true;

        /// <summary>Lee y vacía lo acumulado desde la última lectura.</summary>
        public static void Consume(out Vector2 look, out bool jump, out bool reload, out bool nextWeapon, out bool menu)
        {
            look = _look;
            jump = _jump;
            reload = _reload;
            nextWeapon = _nextWeapon;
            menu = _menu;
            _look = Vector2.zero;
            _jump = _reload = _nextWeapon = _menu = false;
        }

        public static void Reset()
        {
            Move = Vector2.zero;
            FireHeld = false;
            Consume(out _, out _, out _, out _, out _);
        }
    }

    /// <summary>
    /// Controles para teléfono: palanca a la izquierda, arrastrar a la derecha para mirar,
    /// y botones de disparar (arrastrando también se apunta), saltar, recargar, cambiar de arma y menú.
    /// Cada dedo es un puntero distinto de uGUI, así que se pueden usar a la vez.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        private static readonly Color Faint = new Color(1, 1, 1, 0.12f);
        private static readonly Color Knob = new Color(1, 1, 1, 0.35f);

        public static TouchControls Create(Transform canvas)
        {
            var root = Ui.Stretch(Ui.Rect(canvas, "TouchControls"));
            root.SetAsFirstSibling(); // debajo del resto del HUD
            var controls = root.gameObject.AddComponent<TouchControls>();

            // Mitad derecha: arrastrar para mirar (detrás de los botones).
            var look = Ui.Panel(root, new Color(0, 0, 0, 0), "LookArea");
            look.rectTransform.anchorMin = new Vector2(0.4f, 0);
            look.rectTransform.anchorMax = Vector2.one;
            look.rectTransform.offsetMin = look.rectTransform.offsetMax = Vector2.zero;
            look.gameObject.AddComponent<TouchLook>();

            // Palanca de movimiento.
            var stickBase = Ui.Panel(root, Faint, "Stick");
            Place(stickBase.rectTransform, new Vector2(0, 0), new Vector2(340, 340), new Vector2(220, 220));
            var knob = Ui.Panel(stickBase.transform, Knob, "Knob");
            knob.raycastTarget = false;
            knob.rectTransform.sizeDelta = new Vector2(140, 140);
            stickBase.gameObject.AddComponent<TouchStick>().Knob = knob.rectTransform;

            // Botones (esquina inferior derecha).
            Button(root, "DISPARAR", new Vector2(1, 0), new Vector2(240, 240), new Vector2(-190, 200), TouchButton.Kind.Fire, Theme.Accent);
            Button(root, "SALTAR", new Vector2(1, 0), new Vector2(150, 150), new Vector2(-420, 120), TouchButton.Kind.Jump, Theme.Secondary);
            Button(root, "RECARGAR", new Vector2(1, 0), new Vector2(150, 110), new Vector2(-190, 420), TouchButton.Kind.Reload, Theme.Secondary);
            Button(root, "ARMA", new Vector2(1, 0), new Vector2(150, 110), new Vector2(-420, 300), TouchButton.Kind.NextWeapon, Theme.Secondary);
            Button(root, "MENÚ", new Vector2(1, 1), new Vector2(150, 80), new Vector2(-110, -150), TouchButton.Kind.Menu, Theme.Secondary);

            TouchInput.Active = true;
            return controls;
        }

        private void OnDestroy()
        {
            TouchInput.Active = false;
            TouchInput.Reset();
        }

        private static void Button(Transform parent, string label, Vector2 anchor, Vector2 size, Vector2 position,
            TouchButton.Kind kind, Color color)
        {
            color.a = 0.55f;
            var image = Ui.Panel(parent, color, label);
            Place(image.rectTransform, anchor, size, position);
            var text = Ui.Label(image.transform, label, 26, Theme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            Ui.Stretch(text.rectTransform);
            image.gameObject.AddComponent<TouchButton>().Type = kind;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }
    }

    /// <summary>Palanca virtual: el dedo dentro del círculo da la dirección (largo máximo 1).</summary>
    public class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform Knob;
        private RectTransform _rect;

        private void Awake() => _rect = (RectTransform)transform;

        public void OnPointerDown(PointerEventData e) => OnDrag(e);

        public void OnDrag(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, e.position, e.pressEventCamera, out var local))
                return;
            var radius = _rect.rect.width * 0.5f;
            var value = Vector2.ClampMagnitude(local / radius, 1f);
            TouchInput.Move = value;
            if (Knob != null) Knob.anchoredPosition = value * radius;
        }

        public void OnPointerUp(PointerEventData e)
        {
            TouchInput.Move = Vector2.zero;
            if (Knob != null) Knob.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>Zona para mirar: arrastrar el dedo gira la cámara.</summary>
    public class TouchLook : MonoBehaviour, IDragHandler
    {
        public void OnDrag(PointerEventData e) => TouchInput.AddLook(e.delta);
    }

    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public enum Kind { Fire, Jump, Reload, NextWeapon, Menu }

        public Kind Type;

        public void OnPointerDown(PointerEventData e)
        {
            switch (Type)
            {
                case Kind.Fire: TouchInput.FireHeld = true; break;
                case Kind.Jump: TouchInput.PressJump(); break;
                case Kind.Reload: TouchInput.PressReload(); break;
                case Kind.NextWeapon: TouchInput.PressNextWeapon(); break;
                case Kind.Menu: TouchInput.PressMenu(); break;
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (Type == Kind.Fire) TouchInput.FireHeld = false;
        }

        /// <summary>Con el dedo en "disparar" también se apunta, como en los shooters de celular.</summary>
        public void OnDrag(PointerEventData e)
        {
            if (Type == Kind.Fire) TouchInput.AddLook(e.delta);
        }
    }
}
