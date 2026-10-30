using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        public const byte Water = 9;
        public const byte Magma = 10;
        public const byte SuperHard = 11;
        public const byte OreBase = 16;

        public static bool IsOre(byte m) => m >= OreBase;
        public static bool IsLiquid(byte m) => m == Water || m == Magma;
        public static bool Flows(byte m) => m == Sand || IsLiquid(m);
    }

    public sealed class PixelChunk
    {
        public const int Size = 64;
        public readonly byte[] Mats = new byte[Size * Size];
        public readonly float[] Hp = new float[Size * Size];
        public bool Dirty = true;
        public bool SandAwake;
        public bool Dug; // something was removed, so a save has to remember this chunk
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
        private readonly ConcurrentDictionary<long, PixelChunk> ready = new ConcurrentDictionary<long, PixelChunk>();
        private readonly HashSet<long> queued = new HashSet<long>();
        private readonly Dictionary<long, byte[]> dugArchive = new Dictionary<long, byte[]>();
        private readonly object tileLock = new object();
        private long lastKey = long.MinValue;
        private PixelChunk lastChunk;
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
            if (key == lastKey) return lastChunk; // pixel loops stay in one chunk for long stretches
            if (!chunks.TryGetValue(key, out var chunk))
            {
                if (!ready.TryRemove(key, out chunk)) chunk = Generate(cx, cy);
                queued.Remove(key);
                chunks[key] = chunk;
                if (dugArchive.TryGetValue(key, out byte[] holes)) ApplyHoles(chunk, holes);
            }
            lastKey = key;
            lastChunk = chunk;
            return chunk;
        }

        /// <summary>Builds a chunk on a worker thread, so reaching it later costs no frame time.</summary>
        public void Prepare(int cx, int cy)
        {
            if (cy < 0 || cx < 0 || cx * PixelChunk.Size >= Width) return;
            long key = ChunkKey(cx, cy);
            if (chunks.ContainsKey(key) || !queued.Add(key)) return;
            Task.Run(() => ready[key] = Generate(cx, cy));
        }

        /// <summary>Queues the rows of chunks just below a row of tiles.</summary>
        public void PrepareBelow(int row)
        {
            int cy = row * TileSize / PixelChunk.Size;
            for (int ahead = 1; ahead <= 2; ahead++)
                for (int cx = 0; cx * PixelChunk.Size < Width; cx++)
                    Prepare(cx, cy + ahead);
        }

        /// <summary>The holes in every chunk dug so far, as (chunk key, run-length mask) pairs.</summary>
        public IEnumerable<KeyValuePair<long, byte[]>> ExportHoles()
        {
            foreach (var pair in chunks)
                if (pair.Value.Dug) dugArchive[pair.Key] = EncodeHoles(pair.Value);
            return dugArchive;
        }

        public void ImportHoles(long key, byte[] holes) => dugArchive[key] = holes;

        // Alternating run lengths of solid and air, each stored in one or two bytes.
        private static byte[] EncodeHoles(PixelChunk chunk)
        {
            var bytes = new List<byte>();
            bool air = false;
            int run = 0;
            foreach (byte mat in chunk.Mats)
            {
                bool isAir = mat == Mat.Air;
                if (isAir == air)
                {
                    run++;
                    continue;
                }
                WriteRun(bytes, run);
                air = isAir;
                run = 1;
            }
            WriteRun(bytes, run);
            return bytes.ToArray();
        }

        private static void WriteRun(List<byte> bytes, int run)
        {
            if (run < 128)
            {
                bytes.Add((byte)run);
                return;
            }
            bytes.Add((byte)(128 | (run >> 8)));
            bytes.Add((byte)(run & 255));
        }

        private static void ApplyHoles(PixelChunk chunk, byte[] holes)
        {
            int i = 0, at = 0;
            bool air = false;
            while (i < holes.Length && at < chunk.Mats.Length)
            {
                int run = holes[i++];
                if (run >= 128) run = ((run & 127) << 8) | holes[i++];
                if (air)
                {
                    for (int n = 0; n < run; n++)
                    {
                        chunk.Mats[at + n] = Mat.Air;
                        chunk.Hp[at + n] = 0;
                    }
                }
                at += run;
                air = !air;
            }
            chunk.Dug = true;
            chunk.Dirty = true;
            chunk.MinX = chunk.MinY = 0;
            chunk.MaxX = chunk.MaxY = PixelChunk.Size - 1;
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
            ChunkAt(x / PixelChunk.Size, y / PixelChunk.Size).Dug = true;
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
            lastKey = long.MinValue;
            foreach (long key in scratch)
            {
                if (chunks[key].Dug) dugArchive[key] = EncodeHoles(chunks[key]);
                chunks.Remove(key);
            }
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
                    byte mat = chunk.Mats[ly * PixelChunk.Size + lx];
                    if (!Mat.Flows(mat)) continue;
                    int x = cx * PixelChunk.Size + lx, y = cy * PixelChunk.Size + ly;
                    int toY = y + 1;
                    int to = !Solid(x, toY) ? x
                        : !Solid(x - 1, toY) ? x - 1
                        : !Solid(x + 1, toY) ? x + 1 : int.MinValue;
                    if (to == int.MinValue && Mat.IsLiquid(mat))
                    {
                        // liquids also spread sideways along whatever they rest on
                        toY = y;
                        to = !Solid(x + (leftFirst ? -1 : 1), y) ? x + (leftFirst ? -1 : 1)
                            : !Solid(x + (leftFirst ? 1 : -1), y) ? x + (leftFirst ? 1 : -1) : int.MinValue;
                    }
                    if (to == int.MinValue) continue;
                    float hp = chunk.Hp[ly * PixelChunk.Size + lx];
                    Put(x, y, Mat.Air, 0);
                    Put(to, toY, mat, hp);
                    ChunkAt(to / PixelChunk.Size, toY / PixelChunk.Size).SandAwake = true;
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
                    if (Mat.Flows(mat)) chunk.SandAwake = true;
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
            lock (tileLock)
            {
                if (tiles.TryGetValue(key, out var block)) return block;
                block = new Block();
                WorldGen.Fill(block, seed, row, col, tntChance, chestChance);
                if (tiles.Count > 40000) tiles.Clear();
                tiles[key] = block;
                return block;
            }
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
                case BlockType.Water:
                    mat = Mat.Water;
                    break;
                case BlockType.Magma:
                    if (dist < StoneRadius + 1) mat = Mat.Magma;
                    break;
                case BlockType.SuperHard:
                    if (dist < StoneRadius + 1) { mat = Mat.SuperHard; hp *= (float)Balance.SuperHardHpMultiplier; }
                    break;
            }

            if (WorldGen.LayerAt(seed, tileRow, col) == Balance.RuinsLayer && WorldGen.RoomAt(seed, tileRow, col, out bool treasure))
            {
                mat = treasure && Math.Abs(dx) < 6 && Math.Abs(dy) < 5 ? Mat.Chest : Mat.Air;
                if (mat == Mat.Air) hp = 0;
            }
        }

        /// <summary>Slides a band of rows sideways with wrap-around.</summary>
        public void ShiftBand(int firstY, int lastY, int dx)
        {
            var mats = new byte[Width];
            var hps = new float[Width];
            for (int y = firstY; y <= lastY; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    mats[x] = Get(x, y);
                    hps[x] = HpAt(x, y);
                }
                for (int x = 0; x < Width; x++)
                {
                    int from = (x - dx + Width) % Width;
                    if (mats[from] == Mat.Boss) return;
                    Put(x, y, mats[from], hps[from]);
                }
            }
        }

        /// <summary>Lets the ground flow back into a circle that was dug out.</summary>
        public void Refill(int cx, int cy, int radius)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > radius * radius) continue;
                    if (y < 0 || x < 0 || x >= Width || Solid(x, y)) continue;
                    PixelFor(x, y, out byte mat, out float hp);
                    if (mat != Mat.Air && mat != Mat.Tnt) Put(x, y, mat, hp);
                }
            }
        }
    }
}
