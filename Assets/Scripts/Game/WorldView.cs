using System.Collections.Generic;
using DeepDrill.Core;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// Draws the ground, overlays, cracks and the swarm of drills. World x is the column, world y is
    /// minus the row, so a block sits at (col, -row). The camera follows the deepest drill.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        private const int VisibleRows = 14;
        private const int VisibleCols = 23;
        private const float WobbleSeconds = 0.08f;
        private const float DrillFramesPerSecond = 14f;
        private const float CameraFollowRate = 6f;
        private const float DrillPivotBelowCenter = 0.44f; // sprites turn about a point near the tip
        private const float DrillPivotY = 1f / 16f;
        private const int StrataCount = 12;

        private sealed class Cell
        {
            public SpriteRenderer Back;
            public SpriteRenderer Tile;
            public SpriteRenderer Overlay;
            public SpriteRenderer Crack;
            public SpriteRenderer[] Edges;
        }

        // top, bottom, left, right
        private static readonly string[] EdgeNames = { "top", "bottom", "left", "right" };
        private static readonly int[] EdgeRowOffset = { -1, 1, 0, 0 };
        private static readonly int[] EdgeColOffset = { 0, 0, -1, 1 };
        private static readonly string[] GroundVariants = { "a", "b", "c" };
        private static readonly Color FrenzyTint = new Color(1f, 0.55f, 0.5f);

        // the darkest ground tone of each stratum, used for the back wall and guardian shells
        private static readonly Color32[] WallColors =
        {
            new Color32(0x2e, 0x1f, 0x17, 255), new Color32(0x4a, 0x35, 0x20, 255), new Color32(0x4e, 0x4e, 0x44, 255),
            new Color32(0x3a, 0x36, 0x40, 255), new Color32(0x20, 0x24, 0x2e, 255), new Color32(0x3a, 0x2a, 0x22, 255),
            new Color32(0x1e, 0x3a, 0x22, 255), new Color32(0x2a, 0x24, 0x58, 255), new Color32(0x4a, 0x12, 0x26, 255),
            new Color32(0x3a, 0x0e, 0x0e, 255), new Color32(0x6a, 0x3a, 0x08, 255), new Color32(0x5a, 0x54, 0x78, 255),
        };

        private readonly Dictionary<long, float> wobble = new Dictionary<long, float>();
        private readonly List<long> wobbleKeys = new List<long>();
        private readonly List<SpriteRenderer> drillPool = new List<SpriteRenderer>();

        private GameSim sim;
        private Camera cam;
        private Cell[,] cells;
        private Sprite[][] ground;
        private Sprite[][] edgeSprites;
        private Sprite[] loose;
        private Sprite[] oreOverlays;
        private Sprite[] bossFaces;
        private Sprite[] cracks;
        private Sprite[][] tierFrames;
        private Sprite tunnel, grass, tnt, hardOverlay, chestOverlay, bossShell, bossHide;
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

            ground = new Sprite[StrataCount][];
            edgeSprites = new Sprite[StrataCount][];
            loose = new Sprite[StrataCount];
            for (int s = 0; s < StrataCount; s++)
            {
                ground[s] = new Sprite[GroundVariants.Length];
                for (int v = 0; v < GroundVariants.Length; v++) ground[s][v] = art.Get($"ground_{s}_{GroundVariants[v]}");
                edgeSprites[s] = new Sprite[EdgeNames.Length];
                for (int e = 0; e < EdgeNames.Length; e++) edgeSprites[s][e] = art.Get($"edge_{s}_{EdgeNames[e]}");
                loose[s] = art.Get($"loose_{s}");
            }
            oreOverlays = new Sprite[Strata.Ores.Length];
            for (int i = 0; i < oreOverlays.Length; i++) oreOverlays[i] = art.Get($"ore_{i}");

            tierFrames = new Sprite[DrillTiers.Count][];
            for (int t = 0; t < DrillTiers.Count; t++) tierFrames[t] = art.Strip($"tier_{t}", 16, DrillPivotY);

            cracks = art.Strip("crack_overlay", 16, 0.5f);
            tunnel = art.Get("tunnel_neutral");
            grass = art.Get("grass");
            tnt = art.Get("tnt");
            hardOverlay = art.Get("hard_overlay");
            chestOverlay = art.Get("chest_overlay");
            bossShell = art.Get("boss_shell");
            bossHide = art.Get("boss_hide");
            bossFaces = new[] { art.Get("boss_face"), art.Get("beetle_face") };

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
                        Overlay = MakeRenderer("overlay", 2, tile.transform),
                        Crack = MakeRenderer("crack", 4, tile.transform),
                        Edges = new SpriteRenderer[EdgeNames.Length],
                    };
                    for (int e = 0; e < EdgeNames.Length; e++)
                        cell.Edges[e] = MakeRenderer("edge", 3, tile.transform);
                    cells[r, c] = cell;
                }
            }

            for (int i = 0; i < Balance.MaxSimulatedDrills; i++)
            {
                var sr = MakeRenderer("drill", 6, transform);
                sr.enabled = false;
                drillPool.Add(sr);
            }
            followX = (float)sim.Lead.Body.X;
        }

        public void Shake(float seconds, float strength)
        {
            shakeTime = Mathf.Max(shakeTime, seconds);
            shakeStrength = Mathf.Max(shakeStrength, strength);
        }

        public void NoteHit(int row, int col) => wobble[Key(row, col)] = WobbleSeconds;

        /// <summary>Jumps the camera to the lead drill, for when it moves further than a pan should show.</summary>
        public void SnapToDrill() => followX = (float)sim.Lead.Body.X;

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
            var lead = sim.Lead.Body;
            float leadX = (float)lead.X;
            float leadY = -(float)lead.Y;

            followX = Mathf.Lerp(followX, leadX, 1f - Mathf.Exp(-CameraFollowRate * dt));
            Vector2 target = CameraFor(followX, leadY);
            CameraPosition = target;

            Vector2 shake = Vector2.zero;
            if (shakeTime > 0)
            {
                shakeTime -= dt;
                shake = new Vector2(Random.Range(-1, 2), Random.Range(-1, 2)) * shakeStrength / Stage.PixelsPerUnit;
                if (shakeTime <= 0) shakeStrength = 0;
            }
            cam.transform.position = new Vector3(target.x + shake.x, target.y + shake.y, -10);

            drillClock += dt * DrillFramesPerSecond * (sim.InFrenzy ? 2f : 1f);
            DrawDrills();

            int bossStratum = sim.CurrentBossStratum;
            bossFace.enabled = bossStratum >= 0;
            if (bossStratum >= 0)
            {
                bossFace.sprite = bossFaces[bossStratum % bossFaces.Length];
                bossFace.transform.position = new Vector3(Mathf.Round(leadX), -(sim.BossRowNow + 0.5f), 0);
            }

            StepWobble(dt);
            DrawCells(target);
        }

        private void DrawDrills()
        {
            var units = sim.Units;
            for (int i = 0; i < drillPool.Count; i++)
            {
                var sr = drillPool[i];
                sr.enabled = i < units.Count;
                if (!sr.enabled) continue;

                var unit = units[i];
                var frames = tierFrames[unit.Tier];
                sr.sprite = frames[((int)drillClock + i) % frames.Length]; // offset so they do not spin in step
                sr.color = sim.InFrenzy ? FrenzyTint : Color.white;
                // the pivot stays put and the body swings around it; positive core angles tip the nose right
                sr.transform.SetPositionAndRotation(
                    new Vector3(Stage.SnapToPixel((float)unit.Body.X),
                        Stage.SnapToPixel(-(float)unit.Body.Y - DrillPivotBelowCenter), 0),
                    Quaternion.Euler(0, 0, (float)unit.Body.Angle));
            }
        }

        // Puts the lead in the middle of the dig area and a third of the way down it, then keeps
        // the view inside the world so the side walls never show empty space.
        private static Vector2 CameraFor(float drillX, float drillY)
        {
            float digCenter = Stage.DigWidth / 2f;
            float drillStageY = Stage.Height / 3f;
            float x = drillX + (Stage.Width / 2f - digCenter) / Stage.PixelsPerUnit;
            float y = drillY - (Stage.Height / 2f - drillStageY) / Stage.PixelsPerUnit;

            float halfWidth = Stage.Width / 2f / Stage.PixelsPerUnit;
            float digUnits = Stage.PanelX / Stage.PixelsPerUnit; // the rail floats, so ground shows behind it
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
            cell.Overlay.enabled = false;
            for (int e = 0; e < cell.Edges.Length; e++)
            {
                cell.Edges[e].enabled = solid && IsHole(row + EdgeRowOffset[e], col + EdgeColOffset[e]);
                cell.Edges[e].sprite = edgeSprites[stratum][e];
            }
            if (!solid) return;

            var block = sim.Row(row)[col];
            DrawBlock(cell, block, row, col, stratum);

            if (wobble.ContainsKey(Key(row, col)))
                cell.Tile.transform.position = pos + new Vector3(0, -1f / Stage.PixelsPerUnit, 0);

            double ratio = block.Hp / block.MaxHp;
            if (block.Type == BlockType.BossRoot) ratio = sim.BossActive ? sim.BossHp / sim.BossMaxHp : 1;
            if (ratio > 0.66) return;
            cell.Crack.enabled = true;
            cell.Crack.sprite = cracks[ratio > 0.33 ? 0 : ratio > 0.1 ? 1 : 2];
        }

        private void DrawBlock(Cell cell, Block block, int row, int col, int stratum)
        {
            cell.Tile.color = Color.white;
            var plain = ground[stratum];
            cell.Tile.sprite = plain[WorldGen.Hash(sim.State.seed + 7, row, col) % plain.Length];

            switch (block.Type)
            {
                case BlockType.Grass:
                    cell.Tile.sprite = grass;
                    break;
                case BlockType.Tnt:
                    cell.Tile.sprite = tnt;
                    break;
                case BlockType.Sand:
                    cell.Tile.sprite = loose[stratum];
                    break;
                case BlockType.Ore:
                    ShowOverlay(cell, oreOverlays[block.Ore]);
                    break;
                case BlockType.HardRock:
                    ShowOverlay(cell, hardOverlay);
                    break;
                case BlockType.Chest:
                    ShowOverlay(cell, chestOverlay);
                    break;
                case BlockType.BossRoot:
                    bool armored = sim.BossMaxArmor > 0 && (!sim.BossActive || sim.BossArmor > 0);
                    cell.Tile.sprite = armored ? bossShell : bossHide;
                    cell.Tile.color = Color.Lerp(WallColors[stratum], Color.white, 0.45f);
                    break;
            }
        }

        private static void ShowOverlay(Cell cell, Sprite sprite)
        {
            cell.Overlay.enabled = true;
            cell.Overlay.sprite = sprite;
        }

        // The back wall fades between strata across the last few rows of each one.
        private static Color WallTint(int row)
        {
            int stratum = Strata.OfDepth(row);
            if (stratum >= Strata.Last) return WallColors[stratum];
            float into = row % Strata.RowsPerStratum - (Strata.RowsPerStratum - 2f * Strata.TransitionRows);
            return Color.Lerp(WallColors[stratum], WallColors[stratum + 1], into / (2f * Strata.TransitionRows));
        }

        // The sky and the map sides do not count as holes, so grass and the world edge stay borderless.
        private bool IsHole(int row, int col) =>
            row >= 0 && col >= 0 && col < Balance.Columns && !sim.Row(row)[col].Alive;

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
