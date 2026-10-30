using System.Collections.Generic;
using DeepDrill.Core;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>Draws the ground, the crack overlays and the drill. The camera does the scrolling.</summary>
    public sealed class WorldView : MonoBehaviour
    {
        private const int VisibleRows = 23;
        private const float DrillScreenOffset = 53f / Stage.PixelsPerUnit; // drill sits in the upper third
        private const float WobbleSeconds = 0.08f;
        private const float DrillFramesPerSecond = 16f;

        private sealed class Cell
        {
            public SpriteRenderer Back;
            public SpriteRenderer Tile;
            public SpriteRenderer Crack;
            public SpriteRenderer[] Edges;
        }

        // top, bottom, left, right
        private static readonly string[] EdgeNames = { "edge_top", "edge_bottom", "edge_left", "edge_right" };
        private static readonly int[] EdgeRowOffset = { -1, 1, 0, 0 };
        private static readonly int[] EdgeColOffset = { 0, 0, -1, 1 };

        private readonly Dictionary<long, float> wobble = new Dictionary<long, float>();
        private readonly List<long> wobbleKeys = new List<long>();

        private GameSim sim;
        private Camera cam;
        private Cell[,] cells;
        private Sprite[] dirt;
        private Sprite[] cracks;
        private Sprite[] drillFrames;
        private Sprite[] frenzyFrames;
        private Sprite tunnel;
        private Sprite grass, hardRock, coal, copper, chest, tnt;
        private SpriteRenderer drill;
        private float displayDepth;
        private float drillClock;
        private float shakeTime;
        private float shakeStrength;

        public Vector2 CameraPosition { get; private set; }

        public void Init(GameSim simulation, ArtLibrary art, Camera camera)
        {
            sim = simulation;
            cam = camera;
            dirt = new[] { art.Get("dirt_a"), art.Get("dirt_b"), art.Get("dirt_c") };
            cracks = art.Strip("crack_overlay", 16);
            drillFrames = art.Strip("drill_spin", 32);
            frenzyFrames = art.Strip("drill_frenzy", 32);
            tnt = art.Get("tnt");
            tunnel = art.Get("tunnel_bg");
            grass = art.Get("grass");
            hardRock = art.Get("hard_rock");
            coal = art.Get("ore_coal");
            copper = art.Get("ore_copper");
            chest = art.Get("chest");

            cells = new Cell[VisibleRows, Balance.Columns];
            for (int r = 0; r < VisibleRows; r++)
            {
                for (int c = 0; c < Balance.Columns; c++)
                {
                    var tile = MakeRenderer("tile", 1, transform);
                    var cell = new Cell
                    {
                        Back = MakeRenderer("back", 0, transform),
                        Tile = tile,
                        Crack = MakeRenderer("crack", 3, tile.transform),
                        Edges = new SpriteRenderer[EdgeNames.Length],
                    };
                    for (int e = 0; e < EdgeNames.Length; e++)
                    {
                        cell.Edges[e] = MakeRenderer("edge", 2, tile.transform);
                        cell.Edges[e].sprite = art.Get(EdgeNames[e]);
                    }
                    cells[r, c] = cell;
                }
            }

            drill = MakeRenderer("drill", 5, transform);
            displayDepth = sim.State.depth;
        }

        public void Shake(float seconds, float strength)
        {
            shakeTime = Mathf.Max(shakeTime, seconds);
            shakeStrength = Mathf.Max(shakeStrength, strength);
        }

        public void NoteHit(int row, int col) => wobble[Key(row, col)] = WobbleSeconds;

        public static Vector2 CellCenter(int row, int col) => new Vector2(col - Balance.ShaftCenter, -row);

        public bool TryCellAt(Vector2 world, out int row, out int col)
        {
            row = Mathf.RoundToInt(-world.y);
            col = Mathf.RoundToInt(world.x) + Balance.ShaftCenter;
            return row >= 0 && col >= 0 && col < Balance.Columns;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            float gap = sim.State.depth - displayDepth;
            displayDepth = Mathf.MoveTowards(displayDepth, sim.State.depth, Mathf.Max(5f, gap * 8f) * dt);

            float drillY = -displayDepth + 0.5f + 1f;
            Vector2 target = new Vector2(0, Stage.SnapToPixel(drillY - DrillScreenOffset));
            CameraPosition = target;

            Vector2 shake = Vector2.zero;
            if (shakeTime > 0)
            {
                shakeTime -= dt;
                shake = new Vector2(Random.Range(-1, 2), Random.Range(-1, 2)) * shakeStrength / Stage.PixelsPerUnit;
                if (shakeTime <= 0) shakeStrength = 0;
            }
            cam.transform.position = new Vector3(target.x + shake.x, target.y + shake.y, -10);

            drillClock += dt * DrillFramesPerSecond;
            var frames = sim.InFrenzy ? frenzyFrames : drillFrames;
            drill.sprite = frames[(int)(drillClock * (sim.InFrenzy ? 2f : 1f)) % frames.Length];
            drill.transform.position = new Vector3(0, Stage.SnapToPixel(drillY), 0);

            StepWobble(dt);
            DrawCells(target.y);
        }

        private void DrawCells(float cameraY)
        {
            int topRow = Mathf.FloorToInt(-(cameraY + 10.5f));
            for (int r = 0; r < VisibleRows; r++)
            {
                int row = topRow + r;
                for (int c = 0; c < Balance.Columns; c++)
                    DrawCell(cells[r, c], row, c);
            }
        }

        private void DrawCell(Cell cell, int row, int col)
        {
            Vector3 pos = new Vector3(col - Balance.ShaftCenter, -row, 0);
            cell.Back.transform.position = pos;
            cell.Tile.transform.position = pos;

            bool solid = row >= 0 && sim.Row(row)[col].Alive;
            // solid cells keep ground behind them so a nudged tile never shows the sky through the gap
            cell.Back.enabled = row >= 0;
            cell.Back.sprite = solid ? dirt[0] : tunnel;
            cell.Tile.enabled = solid;
            cell.Crack.enabled = false;
            for (int e = 0; e < cell.Edges.Length; e++)
                cell.Edges[e].enabled = solid && IsHole(row + EdgeRowOffset[e], col + EdgeColOffset[e]);
            if (!solid) return;

            var block = sim.Row(row)[col];
            cell.Tile.sprite = SpriteFor(block.Type, row, col);

            if (wobble.ContainsKey(Key(row, col)))
                cell.Tile.transform.position = pos + new Vector3(0, -1f / Stage.PixelsPerUnit, 0);

            double ratio = block.Hp / block.MaxHp;
            if (ratio > 0.66) return;
            cell.Crack.enabled = true;
            cell.Crack.sprite = cracks[ratio > 0.33 ? 0 : ratio > 0.1 ? 1 : 2];
        }

        // The sky and the map sides do not count as holes, so grass and the screen edge stay borderless.
        private bool IsHole(int row, int col) =>
            row >= 0 && col >= 0 && col < Balance.Columns && !sim.Row(row)[col].Alive;

        private Sprite SpriteFor(BlockType type, int row, int col)
        {
            switch (type)
            {
                case BlockType.Grass: return grass;
                case BlockType.HardRock: return hardRock;
                case BlockType.Coal: return coal;
                case BlockType.Copper: return copper;
                case BlockType.Chest: return chest;
                case BlockType.Tnt: return tnt;
                default: return dirt[WorldGen.Hash(sim.State.seed + 7, row, col) % dirt.Length];
            }
        }

        private void StepWobble(float dt)
        {
            wobbleKeys.Clear();
            wobbleKeys.AddRange(wobble.Keys);
            foreach (long key in wobbleKeys)
            {
                float left = wobble[key] - dt;
                if (left <= 0) wobble.Remove(key);
                else wobble[key] = left;
            }
        }

        private static SpriteRenderer MakeRenderer(string goName, int order, Transform parent)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        private static long Key(int row, int col) => row * 64L + col;
    }
}
