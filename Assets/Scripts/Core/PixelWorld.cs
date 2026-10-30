using System;
using System.Collections.Generic;

namespace DeepDrill.Core
{
    /// <summary>What one pixel of ground is made of. Values from OreBase up are ores by OreDef.Index.</summary>
    public static class Mat
    {
        public const byte Air = 0;
        public const byte Dirt = 1;
        public const byte Grass = 2;
        public const byte Hard = 3;
        public const byte Sand = 4;
        public const byte Tnt = 5;
        public const byte Chest = 6;
        public const byte Boss = 7;
        public const byte Wall = 8;
        public const byte OreBase = 16;

        public static bool IsOre(byte m) => m >= OreBase;
    }

    public sealed class PixelChunk
    {
        public const int Size = 64;
        public readonly byte[] Mats = new byte[Size * Size];
        public readonly float[] Hp = new float[Size * Size];
        public bool Dirty = true;
        public bool SandAwake;
        public int MinX = 0, MinY = 0, MaxX = Size - 1, MaxY = Size - 1;

        /// <summary>Grows the repaint rectangle, with a pixel of margin for the neighbours' outlines.</summary>
        public void Touch(int x, int y)
        {
            if (!Dirty)
            {
                MinX = MaxX = x;
                MinY = MaxY = y;
                Dirty = true;
            }
            MinX = System.Math.Max(0, System.Math.Min(MinX, x - 1));
            MinY = System.Math.Max(0, System.Math.Min(MinY, y - 1));
            MaxX = System.Math.Min(Size - 1, System.Math.Max(MaxX, x + 1));
            MaxY = System.Math.Min(Size - 1, System.Math.Max(MaxY, y + 1));
        }
    }

    /// <summary>
    /// Destructible ground, one cell per art pixel, kept in chunks built on demand. Layout is decided
    /// tile by tile as before, then the shapes are broken up so nothing looks like a block.
    /// World pixels: x grows right, y grows down; tile (row, col) covers 16x16 of them.
    /// </summary>
    public sealed class PixelWorld
    {
        public const int TileSize = 16;
        public const int Width = Balance.Columns * TileSize;
        private const int KeepChunksAbove = 8;
        private const int WarpCell = 8;
        private const float WarpPixels = 4f;
        private const float StoneRadius = 7f;
        private const float OreRadius = 5.5f;
        private const int PixelsPerTile = TileSize * TileSize;

        private readonly Dictionary<long, PixelChunk> chunks = new Dictionary<long, PixelChunk>();
        private readonly Dictionary<long, Block> tiles = new Dictionary<long, Block>();
        private readonly HashSet<long> spentCharges = new HashSet<long>();
        private readonly List<long> scratch = new List<long>();
        private readonly int seed;
        private readonly double tntChance;
        private readonly double chestChance;
        private readonly Func<int, bool> isBossRow;

        public PixelWorld(int seed, double tntChance, double chestChance, Func<int, bool> isBossRow)
        {
            this.seed = seed;
            this.tntChance = tntChance;
            this.chestChance = chestChance;
            this.isBossRow = isBossRow;
        }

        public IEnumerable<KeyValuePair<long, PixelChunk>> Chunks => chunks;

        public static long ChunkKey(int cx, int cy) => cy * 4096L + cx;

        private static int FloorDiv(int v, int d) => v >= 0 ? v / d : -((-v + d - 1) / d);

        public PixelChunk ChunkAt(int cx, int cy)
        {
            long key = ChunkKey(cx, cy);
            if (chunks.TryGetValue(key, out var chunk)) return chunk;
            chunk = Generate(cx, cy);
            chunks[key] = chunk;
            return chunk;
        }

        public byte Get(int x, int y)
        {
            if (y < 0) return Mat.Air;
            if (x < 0 || x >= Width) return Mat.Wall;
            var c = ChunkAt(x / PixelChunk.Size, y / PixelChunk.Size);
            return c.Mats[y % PixelChunk.Size * PixelChunk.Size + x % PixelChunk.Size];
        }

        public bool Solid(int x, int y) => Get(x, y) != Mat.Air;

        public float HpAt(int x, int y) =>
            ChunkAt(x / PixelChunk.Size, y / PixelChunk.Size).Hp[y % PixelChunk.Size * PixelChunk.Size + x % PixelChunk.Size];

        public void SetHp(int x, int y, float hp) =>
            ChunkAt(x / PixelChunk.Size, y / PixelChunk.Size).Hp[y % PixelChunk.Size * PixelChunk.Size + x % PixelChunk.Size] = hp;

        public void Clear(int x, int y)
        {
            if (y < 0 || x < 0 || x >= Width) return;
            Put(x, y, Mat.Air, 0);
            WakeSandAround(x, y);
        }

        private void Put(int x, int y, byte mat, float hp)
        {
            var c = ChunkAt(x / PixelChunk.Size, y / PixelChunk.Size);
            int i = y % PixelChunk.Size * PixelChunk.Size + x % PixelChunk.Size;
            c.Mats[i] = mat;
            c.Hp[i] = hp;
            c.Touch(x % PixelChunk.Size, y % PixelChunk.Size);
        }

        private void WakeSandAround(int x, int y)
        {
            int cx = x / PixelChunk.Size, cy = y / PixelChunk.Size;
            ChunkAt(cx, cy).SandAwake = true;
            if (cy > 0) ChunkAt(cx, cy - 1).SandAwake = true;
        }

        public void CarveCircle(int cx, int cy, int radius)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius) Clear(x, y);
        }

        public void CarveRect(int x0, int y0, int x1, int y1)
        {
            for (int y = Math.Max(0, y0); y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Clear(x, y);
        }

        /// <summary>A charge goes off once, however many of its pixels are dug.</summary>
        public bool ClaimCharge(int row, int col) => spentCharges.Add(row * 64L + col);

        /// <summary>Forgets chunks far above the given row; they rebuild if ever seen again.</summary>
        public void DropAbove(int row)
        {
            int limit = row * TileSize / PixelChunk.Size - KeepChunksAbove;
            scratch.Clear();
            foreach (var pair in chunks)
                if (pair.Key / 4096 < limit) scratch.Add(pair.Key);
            foreach (long key in scratch) chunks.Remove(key);
        }

        /// <summary>Turns a beaten guardian's band back into ordinary ground.</summary>
        public void Rebuild(int firstRow, int rows)
        {
            for (int y = firstRow * TileSize; y < (firstRow + rows) * TileSize; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    PixelFor(x, y, out byte mat, out float hp);
                    Put(x, y, mat, hp);
                }
            }
        }

        /// <summary>One step of falling sand in chunks where something changed; returns moves made.</summary>
        public int StepSand(int firstRow, int lastRow)
        {
            int moved = 0;
            int top = Math.Max(0, firstRow * TileSize / PixelChunk.Size);
            int bottom = lastRow * TileSize / PixelChunk.Size;
            for (int cy = bottom; cy >= top; cy--)
            {
                for (int cx = 0; cx * PixelChunk.Size < Width; cx++)
                {
                    if (!chunks.TryGetValue(ChunkKey(cx, cy), out var chunk) || !chunk.SandAwake) continue;
                    int here = StepSandChunk(cx, cy, chunk);
                    chunk.SandAwake = here > 0;
                    moved += here;
                }
            }
            return moved;
        }

        private int StepSandChunk(int cx, int cy, PixelChunk chunk)
        {
            int moved = 0;
            for (int ly = PixelChunk.Size - 1; ly >= 0; ly--)
            {
                bool leftFirst = (ly & 1) == 0;
                for (int n = 0; n < PixelChunk.Size; n++)
                {
                    int lx = leftFirst ? n : PixelChunk.Size - 1 - n;
                    if (chunk.Mats[ly * PixelChunk.Size + lx] != Mat.Sand) continue;
                    int x = cx * PixelChunk.Size + lx, y = cy * PixelChunk.Size + ly;
                    int to = !Solid(x, y + 1) ? x
                        : !Solid(x - 1, y + 1) ? x - 1
                        : !Solid(x + 1, y + 1) ? x + 1 : int.MinValue;
                    if (to == int.MinValue) continue;
                    float hp = chunk.Hp[ly * PixelChunk.Size + lx];
                    Put(x, y, Mat.Air, 0);
                    Put(to, y + 1, Mat.Sand, hp);
                    ChunkAt(to / PixelChunk.Size, (y + 1) / PixelChunk.Size).SandAwake = true;
                    moved++;
                }
            }
            return moved;
        }

        private PixelChunk Generate(int cx, int cy)
        {
            var chunk = new PixelChunk();
            for (int ly = 0; ly < PixelChunk.Size; ly++)
            {
                for (int lx = 0; lx < PixelChunk.Size; lx++)
                {
                    PixelFor(cx * PixelChunk.Size + lx, cy * PixelChunk.Size + ly, out byte mat, out float hp);
                    int i = ly * PixelChunk.Size + lx;
                    chunk.Mats[i] = mat;
                    chunk.Hp[i] = hp;
                    if (mat == Mat.Sand) chunk.SandAwake = true;
                }
            }
            return chunk;
        }

        // A smooth wobble in pixels, so tile edges and layer boundaries never run straight.
        private float Warp(int x, int y, int salt)
        {
            int gx = FloorDiv(x, WarpCell), gy = FloorDiv(y, WarpCell);
            float fx = (x - gx * WarpCell) / (float)WarpCell, fy = (y - gy * WarpCell) / (float)WarpCell;
            float a = Noise(gx, gy, salt), b = Noise(gx + 1, gy, salt);
            float c = Noise(gx, gy + 1, salt), d = Noise(gx + 1, gy + 1, salt);
            float top = a + (b - a) * fx, bottom = c + (d - c) * fx;
            return (top + (bottom - top) * fy) * WarpPixels;
        }

        private float Noise(int x, int y, int salt) =>
            (float)WorldGen.Unit(seed + salt, y, x) * 2f - 1f;

        private Block TileAt(int row, int col)
        {
            long key = row * 64L + col;
            if (tiles.TryGetValue(key, out var block)) return block;
            block = new Block();
            WorldGen.Fill(block, seed, row, col, tntChance, chestChance);
            if (tiles.Count > 40000) tiles.Clear();
            tiles[key] = block;
            return block;
        }

        private void PixelFor(int x, int y, out byte mat, out float hp)
        {
            int row = y / TileSize;
            if (isBossRow(row))
            {
                mat = Mat.Boss;
                hp = 1f;
                return;
            }

            // the surface stays level; below it everything wobbles
            float wx = row == 0 ? x : x + Warp(x, y, 11);
            float wy = row == 0 ? y : y + Warp(x, y, 23);
            int tileRow = Math.Max(0, (int)Math.Floor(wy / TileSize));
            int col = Math.Max(0, Math.Min(Balance.Columns - 1, (int)Math.Floor(wx / TileSize)));
            var tile = TileAt(tileRow, col);

            float dx = wx - (col + 0.5f) * TileSize, dy = wy - (tileRow + 0.5f) * TileSize;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            hp = (float)(Balance.BlockHp(row) / PixelsPerTile);
            mat = Mat.Dirt;

            switch (tile.Type)
            {
                case BlockType.Grass:
                    mat = Mat.Grass;
                    break;
                case BlockType.HardRock:
                    if (dist < StoneRadius) { mat = Mat.Hard; hp *= (float)Balance.HardRockHpMultiplier; }
                    break;
                case BlockType.Ore:
                    if (dist < OreRadius) mat = (byte)(Mat.OreBase + tile.Ore);
                    break;
                case BlockType.Sand:
                    mat = Mat.Sand;
                    hp *= (float)Balance.LooseHpMultiplier;
                    break;
                case BlockType.Chest:
                    if (Math.Abs(dx) < 6 && Math.Abs(dy) < 5) mat = Mat.Chest;
                    break;
                case BlockType.Tnt:
                    if (Math.Abs(dx) < 7 && Math.Abs(dy) < 7) mat = Mat.Tnt;
                    break;
            }
        }
    }
}
