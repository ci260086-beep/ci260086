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

        // The mid colour of each drill material, matching MATERIALS in draw_world_art.lua.
        private static readonly Color32[] Tiers =
        {
            new Color32(0x8f, 0x5a, 0x30, 255), new Color32(0x64, 0x62, 0x60, 255), new Color32(0xdc, 0x70, 0x30, 255),
            new Color32(0x5c, 0x76, 0x96, 255), new Color32(0x86, 0xb8, 0xea, 255), new Color32(0xe4, 0xe9, 0xf2, 255),
            new Color32(0xff, 0xc4, 0x20, 255), new Color32(0xd4, 0x84, 0xff, 255), new Color32(0x70, 0xf0, 0xff, 255),
            new Color32(0x50, 0xf0, 0xa0, 255), new Color32(0xc0, 0x60, 0xff, 255), new Color32(0xff, 0xa0, 0x20, 255),
        };

        private const float ShadowHue = 250;
        private const float LightHue = 55;

        public static Color Shade(int layer, int step)
        {
            float hue = Towards(Towards(Hues[layer], ShadowHue, ToShadow[step]), LightHue, ToLight[step]);
            return Color.HSVToRGB(hue / 360f, Sats[step] * Saturations[layer], Values[step]);
        }

        public static Color Dark(int layer) => Shade(layer, 1);

        public static Color Tier(int tier) => Tiers[tier];

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
