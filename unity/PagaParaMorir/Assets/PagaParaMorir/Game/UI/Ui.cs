using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PagaParaMorir.Game.UI
{
    /// <summary>Colores del juego.</summary>
    public static class Theme
    {
        public static readonly Color Background = Hex("0E0E12");
        public static readonly Color Panel = Hex("1A1A22");
        public static readonly Color PanelLight = Hex("262631");
        public static readonly Color Input = Hex("101016");
        public static readonly Color Accent = Hex("E5383B");
        public static readonly Color Secondary = Hex("3A3A48");
        public static readonly Color Text = Hex("F2F2F2");
        public static readonly Color Muted = Hex("9A9AA5");
        public static readonly Color Success = Hex("3DDC97");
        public static readonly Color Warning = Hex("FFB627");
        public static readonly Color Overlay = new Color(0, 0, 0, 0.75f);

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }

    /// <summary>
    /// Fábrica de UI (uGUI) en código, para no depender de escenas ni prefabs en esta etapa.
    /// </summary>
    public static class Ui
    {
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Canvas CreateCanvas(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && PPM_HAS_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Panel(Transform parent, Color color, string name = "Panel")
        {
            var rt = Rect(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static RectTransform VStack(Transform parent, float spacing = 16, int padding = 0,
            TextAnchor align = TextAnchor.UpperLeft, string name = "VStack")
        {
            var rt = Rect(parent, name);
            Configure(rt.gameObject.AddComponent<VerticalLayoutGroup>(), spacing, padding, align);
            return rt;
        }

        public static RectTransform HStack(Transform parent, float spacing = 16, int padding = 0,
            TextAnchor align = TextAnchor.MiddleLeft, string name = "HStack")
        {
            var rt = Rect(parent, name);
            Configure(rt.gameObject.AddComponent<HorizontalLayoutGroup>(), spacing, padding, align);
            return rt;
        }

        /// <summary>Convierte un panel en contenedor vertical con relleno.</summary>
        public static RectTransform Card(Transform parent, float spacing = 12, int padding = 24, string name = "Card")
        {
            var image = Panel(parent, Theme.Panel, name);
            Configure(image.gameObject.AddComponent<VerticalLayoutGroup>(), spacing, padding, TextAnchor.UpperLeft);
            return image.rectTransform;
        }

        public static Text Label(Transform parent, string text, int size = 28, Color? color = null,
            FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = Rect(parent, "Label");
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color ?? Theme.Text;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(Transform parent, string text, Action onClick, Color? background = null,
            float minWidth = 220, float height = 64, int fontSize = 26)
        {
            var image = Panel(parent, background ?? Theme.Accent, "Button");
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            var label = Label(image.transform, text, fontSize, Theme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(16, 0);
            label.rectTransform.offsetMax = new Vector2(-16, 0);

            Size(image, minWidth: minWidth, preferredWidth: Math.Max(minWidth, label.preferredWidth + 48), height: height);
            return button;
        }

        public static InputField Input(Transform parent, string placeholder, bool password = false,
            bool multiline = false, float height = 64)
        {
            var image = Panel(parent, Theme.Input, "Input");
            Size(image, height: height, flexibleWidth: 1);

            var text = Label(image.transform, "", 26, Theme.Text, FontStyle.Normal,
                multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            text.supportRichText = false;
            Inset(Stretch(text.rectTransform), 18, 10);

            var hint = Label(image.transform, placeholder, 26, Theme.Muted, FontStyle.Italic,
                multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            Inset(Stretch(hint.rectTransform), 18, 10);

            var input = image.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = hint;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            return input;
        }

        /// <summary>Lista con scroll vertical; los hijos se agregan a <paramref name="content"/>.</summary>
        public static RectTransform ScrollList(Transform parent, out RectTransform content, float spacing = 12)
        {
            var root = Rect(parent, "ScrollList");
            Size(root, flexibleHeight: 1, flexibleWidth: 1);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;

            var viewport = Stretch(Rect(root, "Viewport"));
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Rect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            Configure(content.gameObject.AddComponent<VerticalLayoutGroup>(), spacing, 0, TextAnchor.UpperLeft);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return root;
        }

        /// <summary>Elemento vacío que empuja a los demás (en un HStack/VStack).</summary>
        public static void Spacer(Transform parent)
        {
            Size(Rect(parent, "Spacer"), flexibleWidth: 1, flexibleHeight: 1);
        }

        public static LayoutElement Size(Component target, float? minWidth = null, float? preferredWidth = null,
            float? height = null, float? flexibleWidth = null, float? flexibleHeight = null)
        {
            // Sin `??`: en el editor GetComponent devuelve un "null falso" de Unity.
            var element = target.GetComponent<LayoutElement>();
            if (element == null) element = target.gameObject.AddComponent<LayoutElement>();
            if (minWidth.HasValue) element.minWidth = minWidth.Value;
            if (preferredWidth.HasValue) element.preferredWidth = preferredWidth.Value;
            if (height.HasValue) { element.minHeight = height.Value; element.preferredHeight = height.Value; }
            if (flexibleWidth.HasValue) element.flexibleWidth = flexibleWidth.Value;
            if (flexibleHeight.HasValue) element.flexibleHeight = flexibleHeight.Value;
            return element;
        }

        public static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }

        private static RectTransform Inset(RectTransform rt, float x, float y)
        {
            rt.offsetMin = new Vector2(x, y);
            rt.offsetMax = new Vector2(-x, -y);
            return rt;
        }

        private static void Configure(HorizontalOrVerticalLayoutGroup group, float spacing, int padding, TextAnchor align)
        {
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = group is VerticalLayoutGroup;
            group.childForceExpandHeight = false;
        }
    }
}
