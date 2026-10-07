using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MiniGames
{
    public sealed class ArcadeUi : MonoBehaviour
    {
        public static readonly Color Background = Hex("101720"), Panel = Hex("18222E"), Line = Hex("2A3645");
        public static readonly Color White = Hex("F1F3E9"), Muted = Hex("93A3AF"), Mint = Hex("B6F36B");
        public static readonly Color[] BrickColors = { Hex("EF8B79"), Hex("EABD68"), Hex("B6DDA2"), Hex("8CC9D6"), Hex("ABA8D7") };
        public RectTransform Root { get; private set; }
        Sprite rounded;
        Font font;

        public static ArcadeUi Create(Transform owner)
        {
            var go = new GameObject("Arcade UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(ArcadeUi));
            go.transform.SetParent(owner, false);
            var ui = go.GetComponent<ArcadeUi>();
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            ui.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ui.rounded = RoundedSprite();
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var br = bg.GetComponent<RectTransform>();
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = Background;
            bg.GetComponent<Image>().raycastTarget = false;
            ui.Root = new GameObject("1440 x 900 layout", typeof(RectTransform)).GetComponent<RectTransform>();
            ui.Root.SetParent(go.transform, false);
            ui.Root.anchorMin = ui.Root.anchorMax = ui.Root.pivot = new Vector2(.5f, .5f);
            ui.Root.sizeDelta = new Vector2(1440, 900);
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(owner, false);
            }
            ui.Update();
            return ui;
        }

        void Update() => Root.localScale = Vector3.one * Mathf.Min(Screen.width / 1440f, Screen.height / 900f);

        public RectTransform Group(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, x, y, width, height);
            return rect;
        }

        public Image Box(Transform parent, string name, float x, float y, float w, float h, Color color, bool round = true)
        {
            var rect = Group(parent, name, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            if (round) { image.sprite = rounded; image.type = Image.Type.Sliced; }
            return image;
        }

        public Text Label(Transform parent, string content, float x, float y, float w, float h, int size, Color color, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            var rect = Group(parent, content, x, y, w, h);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.text = content; text.fontSize = size; text.color = color;
            text.supportRichText = false;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = align; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        public Button Button(Transform parent, string text, float x, float y, float w, float h, Action action, bool primary = false)
        {
            var box = Box(parent, text, x, y, w, h, primary ? Mint : Line);
            box.raycastTarget = true;
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box;
            var colors = button.colors;
            colors.highlightedColor = new Color(.87f, .95f, 1);
            colors.selectedColor = new Color(.87f, .95f, 1);
            colors.pressedColor = new Color(.7f, .8f, .85f);
            button.colors = colors;
            button.onClick.AddListener(() => action());
            Label(box.transform, text, 8, 0, w - 16, h, 17, primary ? Background : White, true, TextAnchor.MiddleCenter);
            return button;
        }

        public void Header(string section)
        {
            Box(Root, "Mark 1", 64, 43, 10, 26, Mint, false);
            Box(Root, "Mark 2", 78, 43, 10, 17, Mint, false);
            Box(Root, "Mark 3", 92, 43, 10, 26, Mint, false);
            Label(Root, "mini / games", 120, 37, 260, 40, 27, White, true);
            Label(Root, section, 540, 47, 370, 24, 12, Muted, false, TextAnchor.MiddleCenter);
            Box(Root, "Header line", 64, 106, 1312, 1, Line, false);
            Label(Root, "A LITTLE PLAY GOES A LONG WAY.", 64, 848, 520, 20, 11, Muted);
            Label(Root, "JUNCO  /  ARCADE COLLECTION", 1040, 848, 336, 20, 11, Muted, false, TextAnchor.UpperRight);
        }

        public static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        public static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }

        static Sprite RoundedSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UI rounded rectangle", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(16 - (x + .5f), x + .5f - 48, 0);
                float dy = Mathf.Max(16 - (y + .5f), y + .5f - 48, 0);
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(16 - Mathf.Sqrt(dx * dx + dy * dy)));
            }
            texture.SetPixels(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
        }

        void OnDestroy()
        {
            if (rounded == null) return;
            Destroy(rounded.texture);
            Destroy(rounded);
        }
    }
}
