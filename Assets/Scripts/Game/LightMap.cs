using System.Collections.Generic;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// The darkness over the dig area, with pools of light cut out of it: the lead drill's headlamp,
    /// every drill in view, explosions and lightning. Worked out on the CPU at half the stage
    /// resolution each frame, quantised into a few steps with a checker dither so it reads as pixel
    /// art, and shown as one texture.
    /// </summary>
    public sealed class LightMap
    {
        public const int Scale = 2; // stage pixels per light-map pixel
        private const int Steps = 6;

        private struct Flash
        {
            public Vector2 Stage;
            public float Radius, Intensity, Left, Duration;
        }

        public readonly Texture2D Texture;
        private readonly int width, height;
        private readonly float[] light;
        private readonly Color32[] pixels;
        private readonly List<Flash> flashes = new List<Flash>();

        public LightMap(int stageWidth, int stageHeight)
        {
            width = stageWidth / Scale;
            height = stageHeight / Scale;
            light = new float[width * height];
            pixels = new Color32[width * height];
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        public void Clear() => System.Array.Clear(light, 0, light.Length);

        /// <summary>Adds a round pool of light. Position and radius are in stage pixels, y down.</summary>
        public void Add(Vector2 stage, float radius, float intensity)
        {
            float cx = stage.x / Scale, cy = stage.y / Scale, r = radius / Scale;
            if (r <= 0 || cx + r < 0 || cy + r < 0 || cx - r >= width || cy - r >= height) return;
            int x0 = Mathf.Max(0, (int)(cx - r)), x1 = Mathf.Min(width - 1, (int)(cx + r));
            int y0 = Mathf.Max(0, (int)(cy - r)), y1 = Mathf.Min(height - 1, (int)(cy + r));
            float inv = 1f / (r * r);
            for (int y = y0; y <= y1; y++)
            {
                float dy = y + 0.5f - cy;
                int row = (height - 1 - y) * width; // textures fill from the bottom up
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float f = 1 - (dx * dx + dy * dy) * inv;
                    if (f > 0) light[row + x] += f * f * intensity;
                }
            }
        }

        /// <summary>A light that fades out over a short time, for blasts and strikes.</summary>
        public void AddFlash(Vector2 stage, float radius, float intensity, float seconds) =>
            flashes.Add(new Flash { Stage = stage, Radius = radius, Intensity = intensity, Left = seconds, Duration = seconds });

        /// <summary>Applies the timed flashes, then writes the darkness texture.</summary>
        public void Build(Color tint, float darkness, float dt)
        {
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i];
                f.Left -= dt;
                if (f.Left <= 0) { flashes.RemoveAt(i); continue; }
                flashes[i] = f;
                Add(f.Stage, f.Radius, f.Intensity * f.Left / f.Duration);
            }

            byte r = (byte)(tint.r * 255), g = (byte)(tint.g * 255), b = (byte)(tint.b * 255);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    float lit = Mathf.Clamp01(light[i]);
                    float a = darkness * (1 - lit);
                    // a few hard steps with a checker between them, like the rest of the art
                    float t = a * Steps + (((x + y) & 1) == 0 ? 0.25f : -0.25f);
                    a = Mathf.Clamp01(Mathf.Floor(t + 0.5f) / Steps);
                    pixels[i] = new Color32(r, g, b, (byte)(a * 255));
                }
            }
            Texture.SetPixels32(pixels);
            Texture.Apply(false);
        }
    }
}
