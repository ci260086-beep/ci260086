using UnityEngine;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>
    /// Finds text that does not fit its box: wider than the box where it cannot wrap, or taller than
    /// the box once wrapped. The screenshot driver runs it after every shot and logs each case.
    /// </summary>
    public static class TextAudit
    {
        private const float Slack = 1.5f; // stage pixels

        public static int Check(string shot)
        {
            int found = 0;
            foreach (var text in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                if (!text.isActiveAndEnabled || string.IsNullOrEmpty(text.text) || text.color.a <= 0.01f) continue;
                var rect = text.rectTransform.rect;
                if (rect.width <= 0 || rect.height <= 0) continue;
                float wide = text.horizontalOverflow == HorizontalWrapMode.Overflow ? text.preferredWidth - rect.width : 0;
                float tall = text.preferredHeight - rect.height;
                if (wide <= Slack && tall <= Slack) continue;
                found++;
                string what = text.text.Replace("\n", " / ");
                if (what.Length > 40) what = what.Substring(0, 40) + "…";
                Debug.Log($"[overflow] {shot} {PathOf(text.transform)} size={text.fontSize} box={rect.width:0}x{rect.height:0} need={text.preferredWidth:0}x{text.preferredHeight:0} \"{what}\"");
            }
            return found;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
