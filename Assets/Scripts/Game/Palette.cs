using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// The hue-shifted colour ramps of each layer, matching draw_world_art.lua: shadows lean to
    /// blue-violet, lights to warm yellow, and saturation peaks in the mid tones.
    /// </summary>
    public static class Palette
    {
        private static readonly float[] Hues = { 24, 38, 262, 212, 42, 345, 356, 74, 168, 16, 44, 280 };
        private static readonly float[] Saturations = { 0.75f, 0.7f, 0.65f, 0.85f, 0.45f, 0.22f, 0.7f, 0.7f, 0.75f, 1f, 0.85f, 0.18f };
        private static readonly float[] Values = { 0.14f, 0.30f, 0.46f, 0.62f, 0.78f, 0.92f };
        private static readonly float[] Sats = { 0.60f, 0.68f, 0.80f, 0.70f, 0.52f, 0.34f };
        private static readonly float[] ToShadow = { 0.45f, 0.30f, 0.14f, 0, 0, 0 };
        private static readonly float[] ToLight = { 0, 0, 0, 0, 0.14f, 0.28f };

        private const float ShadowHue = 250;
        private const float LightHue = 55;

        public static Color Shade(int layer, int step)
        {
            float hue = Towards(Towards(Hues[layer], ShadowHue, ToShadow[step]), LightHue, ToLight[step]);
            return Color.HSVToRGB(hue / 360f, Sats[step] * Saturations[layer], Values[step]);
        }

        public static Color Dark(int layer) => Shade(layer, 1);

        public static Color Bright(int layer, float alpha)
        {
            var c = Shade(layer, 5);
            c.a = alpha;
            return c;
        }

        private static float Towards(float hue, float target, float amount)
        {
            float d = Mathf.Repeat(target - hue + 540f, 360f) - 180f;
            return Mathf.Repeat(hue + d * amount, 360f);
        }
    }
}
