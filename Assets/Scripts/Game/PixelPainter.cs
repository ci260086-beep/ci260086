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
        private const int DecalsPerLayer = 4;
        private const int RocksPerLayer = 5;
        private const int DecalEveryTiles = 9;
        private const int RockPatchTiles = 4;
        private const int WobbleStretch = 3;
        private const int WobbleOffset = 41;
        private const int WobbleReach = 3;
        private const int WobbleSteps = 128;

        // layer boundaries: the band where the next layer's ground creeps in, in pixels, and how far
        // the noisy contour may wander from the straight line through the band
        private const int BlendLeadPixels = 16;
        private const float BlendNoise = 0.32f;
        private const int BlendCellLarge = 11;
        private const int BlendCellSmall = 4;

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static Color32[][] ramps;
        private static Color32[] ores;
        private static Color32[][][] decals;
        private static Color32[][][] rocks;
        private static readonly int[] wobble = BuildWobble();

        private static int[] BuildWobble()
        {
            var table = new int[WobbleSteps];
            for (int i = 0; i < WobbleSteps; i++)
                table[i] = Mathf.RoundToInt(Mathf.Sin(i * Mathf.PI * 2 / WobbleSteps) * WobbleReach);
            return table;
        }

        private static int Wobble(int step) => wobble[step & (WobbleSteps - 1)];

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

        /// <summary>Reads the layer art once at start-up; the painter samples it for every ground pixel.</summary>
        public static void UseLayerArt(ArtLibrary art)
        {
            int layers = GameData.Layers.Count;
            decals = new Color32[layers][][];
            rocks = new Color32[layers][][];
            for (int l = 0; l < layers; l++)
            {
                decals[l] = new Color32[DecalsPerLayer][];
                for (int i = 0; i < DecalsPerLayer; i++) decals[l][i] = art.Get($"decal_layer_{l}_{i}").texture.GetPixels32();
                rocks[l] = new Color32[RocksPerLayer][];
                for (int i = 0; i < RocksPerLayer; i++) rocks[l][i] = art.Get($"rock_layer_{l}_{i}").texture.GetPixels32();
            }
        }

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
                case Mat.Water:
                    return (x + y / 2) % 7 == 0 ? new Color32(0x70, 0xb8, 0xe8, 255) : new Color32(0x2a, 0x6a, 0xb0, 255);
                case Mat.Magma:
                    return n % 3 == 0 ? new Color32(0xff, 0xc0, 0x50, 255) : new Color32(0xe0, 0x50, 0x20, 255);
                case Mat.SuperHard:
                    return n % 4 == 0 ? new Color32(0xa8, 0xa0, 0xc0, 255) : new Color32(0x4a, 0x46, 0x5a, 255);
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
            return GroundAt(layer, x, y);
        }

        // Ground is drawn from the layer's rock textures, one texture to a patch of tiles so its
        // seams stay out of sight, and one tile in nine carries a small buried thing.
        private static Color32 GroundAt(int layer, int x, int y)
        {
            int size = PixelWorld.TileSize;
            int tileX = x / size, tileY = y / size;
            int lx = x - tileX * size, ly = size - 1 - (y - tileY * size);
            uint roll = Mix(tileX + 91, tileY + 37);
            if (roll % DecalEveryTiles == 0)
            {
                var decal = decals[layer][(roll >> 8) % DecalsPerLayer][ly * size + lx];
                if (decal.a > 0) return decal;
            }
            // a slow wobble and a shift per patch keep the seamless textures from lining up in rows
            uint patch = Mix(tileX / RockPatchTiles, tileY / RockPatchTiles);
            int sx = x + (int)(patch >> 4) + Wobble(y / WobbleStretch);
            int sy = y + (int)(patch >> 12) + Wobble(x / WobbleStretch + WobbleOffset);
            int rx = (sx & (size - 1)), ry = size - 1 - (sy & (size - 1));
            return rocks[layer][patch % RocksPerLayer][ry * size + rx];
        }

        // Smooth noise in [0, 1): bilinear between hashed corners of a grid of `cell` pixels.
        private static float ValueNoise(int x, int y, int cell, int salt)
        {
            int gx = Mathf.FloorToInt(x / (float)cell), gy = Mathf.FloorToInt(y / (float)cell);
            float fx = (x - gx * cell) / (float)cell, fy = (y - gy * cell) / (float)cell;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float a = Corner(gx, gy, salt), b = Corner(gx + 1, gy, salt);
            float c = Corner(gx, gy + 1, salt), d = Corner(gx + 1, gy + 1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Corner(int x, int y, int salt) => (Mix(x + salt, y - salt * 3) & 0xffff) / 65536f;

        /// <summary>
        /// Which layer's look a ground pixel takes. Gameplay mixes layers tile by tile near the end
        /// of a layer; the paint follows a noisy contour through the same band instead, so the
        /// ground changes in organic tongues and pockets rather than in square tiles. Paint only:
        /// what a tile is made of still comes from WorldGen.
        /// </summary>
        public static int PaintLayerAt(int seed, int x, int y, int tileLayer)
        {
            int row = y / PixelWorld.TileSize;
            int layer = GameData.LayerOfRow(row);
            int upper = layer, lower = layer + 1;
            int size = PixelWorld.TileSize;
            // just past a boundary the band continues a little into the new layer
            if (layer > 0 && y < GameData.Layers[layer - 1].RowEnd * size + BlendLeadPixels)
            {
                upper = layer - 1;
                lower = layer;
            }
            if (lower >= GameData.Layers.Count) return tileLayer;
            int end = GameData.Layers[upper].RowEnd * size;
            int start = end - Balance.TransitionRows * size;
            if (y < start) return tileLayer;
            float t = (y - start + 0.5f) / (end + BlendLeadPixels - start);
            float n = ValueNoise(x, y, BlendCellLarge, seed) * 0.7f + ValueNoise(x, y, BlendCellSmall, seed + 17) * 0.3f;
            float v = t + (n - 0.5f) * 2 * BlendNoise;
            // a one pixel checker along the contour softens the step between the two textures
            if (Mathf.Abs(v - 0.5f) < 0.025f) return ((x + y) & 1) == 0 ? lower : upper;
            return v > 0.5f ? lower : upper;
        }

        /// <summary>Repaints part of a chunk into its colour buffer.</summary>
        public static void Paint(PixelWorld world, int cx, int cy, PixelChunk chunk, Color32[] buffer, int seed,
            int minX, int minY, int maxX, int maxY)
        {
            Prepare();
            int size = PixelChunk.Size;
            int last = size - 1;
            var mats = chunk.Mats;
            int layer, tileLayer = 0, tileRow = -1, tileCol = -1;
            for (int ly = minY; ly <= maxY; ly++)
            {
                int y = cy * size + ly;
                for (int lx = minX; lx <= maxX; lx++)
                {
                    int x = cx * size + lx;
                    int at = ly * size + lx;
                    byte mat = mats[at];
                    int to = (last - ly) * size + lx;
                    if (mat == Mat.Air)
                    {
                        buffer[to] = Clear;
                        continue;
                    }
                    int row = y / PixelWorld.TileSize, col = x / PixelWorld.TileSize;
                    if (row != tileRow || col != tileCol)
                    {
                        tileRow = row;
                        tileCol = col;
                        tileLayer = WorldGen.LayerAt(seed, row, col);
                    }
                    layer = PaintLayerAt(seed, x, y, tileLayer);
                    // neighbours inside the chunk are read directly; only its rim asks the world
                    bool up = ly > 0 ? mats[at - size] == Mat.Air : y > 0 && !world.Solid(x, y - 1);
                    bool down = ly < last ? mats[at + size] == Mat.Air : !world.Solid(x, y + 1);
                    bool side = (lx > 0 ? mats[at - 1] == Mat.Air : !world.Solid(x - 1, y))
                        || (lx < last ? mats[at + 1] == Mat.Air : !world.Solid(x + 1, y));
                    if (up) buffer[to] = ramps[layer][5];
                    else if (down || side) buffer[to] = ramps[layer][0];
                    else buffer[to] = ColorOf(mat, layer, x, y);
                }
            }
        }
    }
}
