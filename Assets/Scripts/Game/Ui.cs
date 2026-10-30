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

        public static Text Label(Transform parent, Font font, string text, float x, float y, float w, float h,
            int size, TextAnchor anchor)
        {
            var go = new GameObject("label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = anchor;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
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
