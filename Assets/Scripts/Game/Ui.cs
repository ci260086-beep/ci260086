using UnityEngine;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>Small uGUI builders. All positions are stage pixels from the parent's top left.</summary>
    public static class Ui
    {
        public static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static Image Panel(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            TopLeft(image.rectTransform, x, y, w, h);
            return image;
        }

        public static Image Icon(Transform parent, Sprite sprite, float x, float y, float size)
        {
            var image = Panel(parent, "icon", x, y, size, size, Color.white);
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        public const int PixelFontSize = 8;
        public const float LineSpacing = 0.75f; // the font's own line is 12 pixels for 8-pixel glyphs; 9 keeps lists compact

        /// <summary>Height of one line of text at a font size, in stage pixels.</summary>
        public static float LineHeight(int size) => size * 1.5f * LineSpacing;

        /// <summary>The pixel font only stays crisp at whole multiples of its 8-pixel size.</summary>
        public static int PixelSize(int requested) => requested <= 11 ? PixelFontSize : requested <= 19 ? PixelFontSize * 2 : PixelFontSize * 3;

        /// <summary>Fusion Pixel 8px, shipped with the game, with the OS font as a fallback for anything missing.</summary>
        public static Font LoadPixelFont()
        {
            var font = Resources.Load<Font>("Fonts/FusionPixel8");
            if (font == null)
            {
                Debug.LogWarning("Pixel font missing, using the system font");
                return Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 16);
            }
            font.fontNames = new[] { "Fusion Pixel 8px Proportional SC", "Microsoft YaHei UI", "Microsoft YaHei" };
            Font.textureRebuilt += f => { if (f == font && f.material.mainTexture != null) f.material.mainTexture.filterMode = FilterMode.Point; };
            if (font.material.mainTexture != null) font.material.mainTexture.filterMode = FilterMode.Point;
            return font;
        }

        public static Text Label(Transform parent, Font font, string text, float x, float y, float w, float h,
            int size, TextAnchor anchor)
        {
            var go = new GameObject("label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = PixelSize(size);
            label.alignment = anchor;
            label.color = Color.white;
            label.raycastTarget = false;
            // long text breaks onto the next line inside its box instead of running past it
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.lineSpacing = LineSpacing;
            TopLeft(label.rectTransform, x, y, w, h);
            return label;
        }

        public static Button Button(Transform parent, Font font, string text, float x, float y, float w, float h,
            int size, Color color, out Text label)
        {
            var image = Panel(parent, "button", x, y, w, h, color);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            label = Label(image.transform, font, text, 0, 0, w, h, size, TextAnchor.MiddleCenter);
            return button;
        }
    }
}
