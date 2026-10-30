using System.Collections.Generic;
using DeepDrill.Core;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// Draws the ground, crack overlays and the drill. World x is the column, world y is minus the
    /// row, so a block sits at (col, -row). The camera follows the drill through the dig area.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        private const int VisibleRows = 14;
        private const int VisibleCols = 23;
        private const float WobbleSeconds = 0.08f;
        private const float DrillFramesPerSecond = 16f;
        private const float CameraFollowRate = 6f;
        private const float DrillPivotBelowCenter = 0.92f; // the sprite turns about a point near its tip
        private const float DrillPivotY = 1.5f / 32f;

        private static readonly Color SoilWall = new Color32(0x2e, 0x1f, 0x17, 0xff);
        private static readonly Color SandWall = new Color32(0x4a, 0x35, 0x20, 0xff);

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
        private Sprite[][] ground;
        private Sprite[][] edgeSprites;
        private Sprite[] bossFaces;
        private Sprite[] cracks;
        private Sprite[] drillFrames;
        private Sprite[] frenzyFrames;
        private Sprite tunnel;
        private Sprite grass, hardRock, coal, copper, chest, tnt, bossRoot;
        private Sprite hardRockSand, coalSand, copperSand, iron, silver, chestSand, sand, beetleShell, beetleBare;
        private SpriteRenderer drill;
        private SpriteRenderer bossFace;
        private float followX;
        private float drillClock;
        private float shakeTime;
        private float shakeStrength;

        public Vector2 CameraPosition { get; private set; }

        public void Init(GameSim simulation, ArtLibrary art, Camera camera)
        {
            sim = simulation;
            cam = camera;
            ground = new[]
            {
                new[] { art.Get("dirt_a"), art.Get("dirt_b"), art.Get("dirt_c") },
                new[] { art.Get("sandstone_a"), art.Get("sandstone_b"), art.Get("sandstone_c") },
            };
            edgeSprites = new Sprite[2][];
            for (int stratum = 0; stratum < edgeSprites.Length; stratum++)
            {
                edgeSprites[stratum] = new Sprite[EdgeNames.Length];
                for (int e = 0; e < EdgeNames.Length; e++)
                    edgeSprites[stratum][e] = art.Get(EdgeNames[e] + (stratum == 0 ? "" : "_sand"));
            }
            hardRockSand = art.Get("hard_rock_sand");
            coalSand = art.Get("ore_coal_sand");
            copperSand = art.Get("ore_copper_sand");
            iron = art.Get("ore_iron_sand");
            silver = art.Get("ore_silver_sand");
            chestSand = art.Get("chest_sand");
            sand = art.Get("sand_block");
            beetleShell = art.Get("beetle_shell");
            beetleBare = art.Get("beetle_bare");
            bossFaces = new[] { art.Get("boss_face"), art.Get("beetle_face") };
            cracks = art.Strip("crack_overlay", 16, 0.5f);
            drillFrames = art.Strip("drill_spin", 32, DrillPivotY);
            frenzyFrames = art.Strip("drill_frenzy", 32, DrillPivotY);
            tnt = art.Get("tnt");
            tunnel = art.Get("tunnel_neutral");
            grass = art.Get("grass");
            hardRock = art.Get("hard_rock");
            coal = art.Get("ore_coal");
            copper = art.Get("ore_copper");
            chest = art.Get("chest");
            bossRoot = art.Get("boss_root");

            bossFace = MakeRenderer("bossFace", 4, transform);

            cells = new Cell[VisibleRows, VisibleCols];
            for (int r = 0; r < VisibleRows; r++)
            {
                for (int c = 0; c < VisibleCols; c++)
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
                        cell.Edges[e] = MakeRenderer("edge", 2, tile.transform);
                    cells[r, c] = cell;
                }
            }

            drill = MakeRenderer("drill", 5, transform);
            followX = (float)sim.Drill.X;
        }

        public void Shake(float seconds, float strength)
        {
            shakeTime = Mathf.Max(shakeTime, seconds);
            shakeStrength = Mathf.Max(shakeStrength, strength);
        }

        public void NoteHit(int row, int col) => wobble[Key(row, col)] = WobbleSeconds;

        /// <summary>Jumps the camera to the drill, for when it moves further than a pan should show.</summary>
        public void SnapToDrill() => followX = (float)sim.Drill.X;

        public static Vector2 CellCenter(int row, int col) => new Vector2(col, -row);

        public bool TryCellAt(Vector2 world, out int row, out int col)
        {
            row = Mathf.RoundToInt(-world.y);
            col = Mathf.RoundToInt(world.x);
            return row >= 0 && col >= 0 && col < Balance.Columns;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            var body = sim.Drill;
            float drillX = (float)body.X;
            float drillY = -(float)body.Y;

            followX = Mathf.Lerp(followX, drillX, 1f - Mathf.Exp(-CameraFollowRate * dt));
            Vector2 target = CameraFor(followX, drillY);
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

            // the pivot stays put and the body swings around it; positive core angles tip the nose right
            float angle = (float)body.Angle;
            drill.transform.SetPositionAndRotation(
                new Vector3(Stage.SnapToPixel(drillX), Stage.SnapToPixel(drillY - DrillPivotBelowCenter), 0),
                Quaternion.Euler(0, 0, angle));

            var boss = sim.CurrentBoss;
            bossFace.enabled = boss != null;
            if (boss != null)
            {
                bossFace.sprite = bossFaces[sim.State.bossesDefeated];
                bossFace.transform.position = new Vector3(Mathf.Round(drillX), -(boss.Row + 0.5f), 0);
            }

            StepWobble(dt);
            DrawCells(target);
        }

        // Puts the drill in the middle of the dig area and a third of the way down it, then keeps
        // the view inside the world so the side walls never show empty space.
        private static Vector2 CameraFor(float drillX, float drillY)
        {
            float digCenter = Stage.DigWidth / 2f;
            float drillStageY = Stage.Height / 3f;
            float x = drillX + (Stage.Width / 2f - digCenter) / Stage.PixelsPerUnit;
            float y = drillY - (Stage.Height / 2f - drillStageY) / Stage.PixelsPerUnit;

            float halfWidth = Stage.Width / 2f / Stage.PixelsPerUnit;
            float digUnits = Stage.DigWidth / Stage.PixelsPerUnit;
            x = Mathf.Clamp(x, -0.5f + halfWidth, Balance.Columns - 0.5f - digUnits + halfWidth);
            return new Vector2(Stage.SnapToPixel(x), Stage.SnapToPixel(y));
        }

        private void DrawCells(Vector2 camera)
        {
            float halfWidth = Stage.Width / 2f / Stage.PixelsPerUnit;
            float halfHeight = Stage.Height / 2f / Stage.PixelsPerUnit;
            int leftCol = Mathf.FloorToInt(camera.x - halfWidth) - 1;
            int topRow = Mathf.FloorToInt(-(camera.y + halfHeight)) - 1;
            for (int r = 0; r < VisibleRows; r++)
                for (int c = 0; c < VisibleCols; c++)
                    DrawCell(cells[r, c], topRow + r, leftCol + c);
        }

        private void DrawCell(Cell cell, int row, int col)
        {
            Vector3 pos = new Vector3(col, -row, 0);
            cell.Back.transform.position = pos;
            cell.Tile.transform.position = pos;

            bool inWorld = row >= 0 && col >= 0 && col < Balance.Columns;
            bool solid = inWorld && sim.Row(row)[col].Alive;
            int stratum = inWorld ? WorldGen.StratumAt(sim.State.seed, row, col) : 0;
            // solid cells keep ground behind them so a nudged tile never shows the sky through the gap
            cell.Back.enabled = inWorld;
            cell.Back.sprite = solid ? ground[stratum][0] : tunnel;
            cell.Back.color = solid ? Color.white : WallTint(row);
            cell.Tile.enabled = solid;
            cell.Crack.enabled = false;
            for (int e = 0; e < cell.Edges.Length; e++)
            {
                cell.Edges[e].enabled = solid && IsHole(row + EdgeRowOffset[e], col + EdgeColOffset[e]);
                cell.Edges[e].sprite = edgeSprites[stratum][e];
            }
            if (!solid) return;

            var block = sim.Row(row)[col];
            cell.Tile.sprite = SpriteFor(block.Type, row, col, stratum);

            if (wobble.ContainsKey(Key(row, col)))
                cell.Tile.transform.position = pos + new Vector3(0, -1f / Stage.PixelsPerUnit, 0);

            double ratio = block.Hp / block.MaxHp;
            if (block.Type == BlockType.BossRoot) ratio = sim.BossActive ? sim.BossHp / sim.BossMaxHp : 1;
            if (ratio > 0.66) return;
            cell.Crack.enabled = true;
            cell.Crack.sprite = cracks[ratio > 0.33 ? 0 : ratio > 0.1 ? 1 : 2];
        }

        // The sky and the map sides do not count as holes, so grass and the world edge stay borderless.
        private bool IsHole(int row, int col) =>
            row >= 0 && col >= 0 && col < Balance.Columns && !sim.Row(row)[col].Alive;

        // The back wall fades across the transition rows and a little past them.
        private static Color WallTint(int row)
        {
            float from = Balance.SandstoneRow - Balance.TransitionRows;
            float to = Balance.SandstoneRow + Balance.TransitionRows;
            return Color.Lerp(SoilWall, SandWall, Mathf.InverseLerp(from, to, row));
        }

        private Sprite SpriteFor(BlockType type, int row, int col, int stratum)
        {
            bool inSand = stratum == 1;
            switch (type)
            {
                case BlockType.Grass: return grass;
                case BlockType.HardRock: return inSand ? hardRockSand : hardRock;
                case BlockType.Coal: return inSand ? coalSand : coal;
                case BlockType.Copper: return inSand ? copperSand : copper;
                case BlockType.Iron: return iron;
                case BlockType.Silver: return silver;
                case BlockType.Sand: return sand;
                case BlockType.Chest: return inSand ? chestSand : chest;
                case BlockType.Tnt: return tnt;
                case BlockType.BossRoot:
                    if (row < Balance.BeetleRow) return bossRoot;
                    return sim.BossActive && sim.BossArmor <= 0 ? beetleBare : beetleShell;
                default:
                    var set = ground[stratum];
                    return set[WorldGen.Hash(sim.State.seed + 7, row, col) % set.Length];
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
