using DeepDrill.Core;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// Turns ground pixels into colour. Solid ground is one continuous surface; only pixels touching
    /// a hole get an edge: the layer's darkest tone on the shadow side, a light rim where a surface
    /// faces up. Nothing here is pure black.
    /// </summary>
    public static class PixelPainter
    {
        private const int SpeckSize = 3;

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static Color32[][] ramps;
        private static Color32[] ores;

        private static readonly Color32[] OreTones =
        {
            new Color32(0x4a, 0x4a, 0x58, 255), new Color32(0xe0, 0x8a, 0x4a, 255), new Color32(0x9a, 0xa0, 0xb0, 255),
            new Color32(0xe8, 0xe8, 0xf0, 255), new Color32(0xb0, 0x90, 0xf0, 255), new Color32(0xe8, 0xb0, 0x2a, 255),
            new Color32(0xe0, 0xd8, 0xe0, 255), new Color32(0x30, 0x60, 0xe8, 255), new Color32(0xb0, 0x8c, 0x38, 255),
            new Color32(0xe8, 0x30, 0x5a, 255), new Color32(0xc8, 0xc8, 0xd8, 255), new Color32(0x6a, 0xa8, 0xc0, 255),
            new Color32(0x60, 0x68, 0x80, 255), new Color32(0xc8, 0xd0, 0xe4, 255), new Color32(0x90, 0xc0, 0x40, 255),
            new Color32(0x90, 0xe8, 0xf8, 255), new Color32(0x50, 0x60, 0xe0, 255), new Color32(0xe8, 0x40, 0x80, 255),
            new Color32(0x80, 0xb0, 0x50, 255), new Color32(0xc0, 0x50, 0xb0, 255), new Color32(0xe8, 0xc8, 0x70, 255),
            new Color32(0x60, 0xb0, 0xf0, 255), new Color32(0xff, 0xe2, 0x7a, 255), new Color32(0xc0, 0x5c, 0xff, 255),
        };

        private static void Prepare()
        {
            if (ramps != null) return;
            int layers = GameData.Layers.Count;
            ramps = new Color32[layers][];
            for (int l = 0; l < layers; l++)
            {
                ramps[l] = new Color32[6];
                for (int s = 0; s < 6; s++) ramps[l][s] = Palette.Shade(l, s);
            }
            ores = OreTones;
        }

        private static uint Mix(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 73856093u ^ (uint)y * 19349663u;
                h ^= h >> 13;
                h *= 1274126177u;
                return h ^ (h >> 16);
            }
        }

        /// <summary>The body colour of a material at a spot, before any edge.</summary>
        public static Color32 ColorOf(byte mat, int layer, int x, int y)
        {
            Prepare();
            var ramp = ramps[layer];
            uint n = Mix(x, y);
            switch (mat)
            {
                case Mat.Grass:
                    return y % 16 < 2 ? new Color32(0xa8, 0xd4, 0x5a, 255)
                        : y % 16 < 5 ? new Color32(0x76, 0xa8, 0x3a, 255) : ramp[3];
                case Mat.Hard:
                    return n % 5 == 0 ? new Color32(0x5c, 0x5c, 0x6a, 255)
                        : n % 7 == 0 ? new Color32(0xb4, 0xb4, 0xc2, 255) : new Color32(0x8a, 0x8a, 0x98, 255);
                case Mat.Sand:
                    return n % 4 == 0 ? ramp[5] : ramp[4];
                case Mat.Tnt:
                    return (y / 3) % 2 == 0 ? new Color32(0xb8, 0x2c, 0x2c, 255) : new Color32(0x7c, 0x1a, 0x22, 255);
                case Mat.Chest:
                    return (y % 16) == 8 ? new Color32(0xe8, 0xb0, 0x2a, 255) : new Color32(0xb8, 0x69, 0x3a, 255);
                case Mat.Boss:
                    return n % 5 == 0 ? ramp[2] : ramp[1];
            }
            if (Mat.IsOre(mat))
            {
                var ore = ores[mat - Mat.OreBase];
                return Mix(x / 2, y / 2) % 9 == 0 ? new Color32(255, 255, 255, 255) : ore;
            }
            // specks come in small clumps: single-pixel noise reads as static rather than earth
            int speck = (int)(Mix(x / SpeckSize, y / SpeckSize) % 14);
            return speck == 0 ? ramp[4] : speck <= 2 ? ramp[2] : ramp[3];
        }

        /// <summary>Repaints part of a chunk into its colour buffer.</summary>
        public static void Paint(PixelWorld world, int cx, int cy, PixelChunk chunk, Color32[] buffer, int seed,
            int minX, int minY, int maxX, int maxY)
        {
            Prepare();
            int size = PixelChunk.Size;
            for (int ly = minY; ly <= maxY; ly++)
            {
                int y = cy * size + ly;
                for (int lx = minX; lx <= maxX; lx++)
                {
                    int x = cx * size + lx;
                    byte mat = chunk.Mats[ly * size + lx];
                    int to = (size - 1 - ly) * size + lx;
                    if (mat == Mat.Air)
                    {
                        buffer[to] = Clear;
                        continue;
                    }
                    int layer = WorldGen.LayerAt(seed, y / PixelWorld.TileSize, x / PixelWorld.TileSize);
                    bool up = !world.Solid(x, y - 1) && y > 0;
                    bool down = !world.Solid(x, y + 1);
                    bool side = !world.Solid(x - 1, y) || !world.Solid(x + 1, y);
                    if (up) buffer[to] = ramps[layer][5];
                    else if (down || side) buffer[to] = ramps[layer][0];
                    else buffer[to] = ColorOf(mat, layer, x, y);
                }
            }
        }
    }
}
