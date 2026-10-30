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
        private const float DrillPivotBelowCenter = 0.33f; // sprites turn about a point near the tip
        private const float DrillPivotY = 1f / 12f;
        private const int DrillPool = 400;
        private const int GlowCount = 60;
        private const float ParallaxTileUnits = 4f; // 64 pixels
        private const float TwinkleSeconds = 0.5f;
        private const uint DecalOdds = 9;
        private const float MaxShakePixels = 6f;
        private const float TraumaDecay = 1.8f;
        private const float TraumaPerStrength = 0.11f;
        private const float NoiseSpeed = 22f;
        private const double SquashSeconds = 0.09;
        private const float SquashWidth = 1.15f;
        private const float SquashHeight = 0.85f;

        private sealed class Cell
        {
            public SpriteRenderer Back;
            public SpriteRenderer Tile;
            public SpriteRenderer Overlay;
            public SpriteRenderer Crack;
            public SpriteRenderer Flash;
            public SpriteRenderer[] Edges;
        }

        // top, bottom, left, right
        private static readonly string[] EdgeNames = { "top", "bottom", "left", "right" };
        private static readonly int[] EdgeRowOffset = { -1, 1, 0, 0 };
        private static readonly int[] EdgeColOffset = { 0, 0, -1, 1 };
        private static readonly string[] GroundVariants = { "a", "b", "c" };
        private static readonly Color FrenzyTint = new Color(1f, 0.55f, 0.5f);

        private static readonly float[] ParallaxRates = { 0.2f, 0.45f, 0.7f };
        private static readonly string[] ParallaxNames = { "far", "mid", "near" };
        private static readonly Color SkyColor = new Color32(0x8f, 0xc9, 0xe6, 0xff);

        private readonly Dictionary<long, float> wobble = new Dictionary<long, float>();
        private readonly List<long> wobbleKeys = new List<long>();
        private readonly HashSet<long> flashing = new HashSet<long>();
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
        private Dictionary<int, Sprite[]> drillFrames;
        private Sprite tunnel, grass, tnt, hardOverlay, superHardOverlay, chestOverlay, bossShell, bossHide;
        private Sprite waterBlock, magmaBlock;
        private SpriteRenderer bossFace;
        private SpriteRenderer[] parallax;
        private Sprite[][] parallaxSprites;
        private Sprite[] decals;
        private Sprite plainTile;
        private readonly List<SpriteRenderer> glows = new List<SpriteRenderer>();
        private readonly List<Vector2> visibleOres = new List<Vector2>();
        private float twinkleClock;
        private float followX;
        private float drillClock;
        private float trauma;

        public Vector2 CameraPosition { get; private set; }

        public void Init(GameSim simulation, ArtLibrary art, Camera camera)
        {
            sim = simulation;
            cam = camera;

            int layers = GameData.Layers.Count;
            ground = new Sprite[layers][];
            edgeSprites = new Sprite[layers][];
            loose = new Sprite[layers];
            for (int s = 0; s < layers; s++)
            {
                ground[s] = new Sprite[GroundVariants.Length];
                for (int v = 0; v < GroundVariants.Length; v++) ground[s][v] = art.Get($"ground_{s}_{GroundVariants[v]}");
                edgeSprites[s] = new Sprite[EdgeNames.Length];
                for (int e = 0; e < EdgeNames.Length; e++) edgeSprites[s][e] = art.Get($"edge_{s}_{EdgeNames[e]}");
                loose[s] = art.Get($"loose_{s}");
            }
            oreOverlays = new Sprite[GameData.OreCount];
            for (int i = 0; i < oreOverlays.Length; i++) oreOverlays[i] = art.Get($"ore_{i}");

            drillFrames = new Dictionary<int, Sprite[]>();
            for (int f = 0; f < GameData.Families.Count; f++)
                for (int t = 0; t < GameData.Tiers; t++)
                    drillFrames[DrillCode.Make(f, t)] = art.Strip($"drill_{f}_{t}", 8, DrillPivotY);

            cracks = art.Strip("crack_overlay", 16, 0.5f);
            tunnel = art.Get("tunnel_neutral");
            grass = art.Get("grass");
            tnt = art.Get("tnt");
            hardOverlay = art.Get("hard_overlay");
            superHardOverlay = art.Get("superhard_overlay");
            waterBlock = art.Get("water_block");
            magmaBlock = art.Get("magma_block");
            chestOverlay = art.Get("chest_overlay");
            bossShell = art.Get("boss_shell");
            bossHide = art.Get("boss_hide");
            bossFaces = new Sprite[layers];
            for (int s = 0; s < layers; s++) bossFaces[s] = art.Get($"boss_face_{s}");

            bossFace = MakeRenderer("bossFace", 4, transform);

            plainTile = art.Get("plain_tile");
            decals = new[] { art.Get("decal_0"), art.Get("decal_1"), art.Get("decal_2"), art.Get("decal_3") };
            parallaxSprites = new Sprite[layers][];
            for (int s = 0; s < layers; s++)
            {
                parallaxSprites[s] = new Sprite[ParallaxNames.Length];
                for (int p = 0; p < ParallaxNames.Length; p++)
                    parallaxSprites[s][p] = art.Get($"parallax_{s}_{ParallaxNames[p]}");
            }
            parallax = new SpriteRenderer[ParallaxNames.Length];
            for (int p = 0; p < parallax.Length; p++)
            {
                var sr = MakeRenderer("parallax", -3 + p, transform);
                sr.drawMode = SpriteDrawMode.Tiled;
                parallax[p] = sr;
            }
            for (int i = 0; i < GlowCount; i++)
            {
                var glow = MakeRenderer("glow", 5, transform);
                glow.sprite = art.Get("fx_circle");
                glow.color = new Color(1f, 0.8f, 0.45f, 0.14f);
                glow.transform.localScale = new Vector3(2.4f, 2.4f, 1);
                glow.enabled = false;
                glows.Add(glow);
            }

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
                        Flash = MakeRenderer("flash", 5, tile.transform),
                        Edges = new SpriteRenderer[EdgeNames.Length],
                    };
                    for (int e = 0; e < EdgeNames.Length; e++)
                        cell.Edges[e] = MakeRenderer("edge", 3, tile.transform);
                    cell.Flash.sprite = art.Get("pixel");
                    cell.Flash.transform.localScale = new Vector3(Stage.PixelsPerUnit, Stage.PixelsPerUnit, 1);
                    cell.Flash.color = new Color(1, 1, 1, 0.8f);
                    cells[r, c] = cell;
                }
            }

            for (int i = 0; i < DrillPool; i++)
            {
                var sr = MakeRenderer("drill", 6, transform);
                sr.enabled = false;
                drillPool.Add(sr);
            }
            followX = (float)sim.Lead.Body.X;
        }

        /// <summary>Adds trauma; the screen moves by its square, so small hits stay subtle.</summary>
        public void Shake(float seconds, float strength)
        {
            trauma = Mathf.Min(1f, trauma + strength * TraumaPerStrength * Mathf.Clamp(seconds * 8f, 0.5f, 1.5f));
        }

        public void NoteHit(int row, int col)
        {
            long key = Key(row, col);
            wobble[key] = WobbleSeconds;
            flashing.Add(key);
        }

        /// <summary>Jumps the camera to the lead drill, for when it moves further than a pan should show.</summary>
        public void SnapToDrill() => followX = (float)sim.Lead.Body.X;

        public static Vector2 CellCenter(int row, int col) => new Vector2(col, -row);

        public bool TryCellAt(Vector2 world, out int row, out int col)
        {
            row = Mathf.RoundToInt(-world.y);
            col = Mathf.RoundToInt(world.x);
            return col >= 0 && col < Balance.Columns; // rows above ground stay valid: drills start up there
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
            if (trauma > 0)
            {
                trauma = Mathf.Max(0, trauma - TraumaDecay * dt);
                float amount = trauma * trauma * MaxShakePixels * GameSettings.ShakeScale / Stage.PixelsPerUnit;
                float t = Time.unscaledTime * NoiseSpeed;
                shake = new Vector2(Mathf.PerlinNoise(t, 0.3f) * 2 - 1, Mathf.PerlinNoise(0.7f, t) * 2 - 1) * amount;
            }
            cam.transform.position = new Vector3(target.x + shake.x, target.y + shake.y, -10);

            drillClock += dt * DrillFramesPerSecond * (sim.InFrenzy ? 2f : 1f);
            DrawDrills(target);
            DrawParallax(target);

            var boss = sim.CurrentBoss;
            bossFace.enabled = boss != null;
            if (boss != null)
            {
                bossFace.sprite = bossFaces[boss.Index];
                bossFace.transform.position = new Vector3(Mathf.Round(leadX), -(boss.RowEnd + 0.5f), 0);
            }

            StepWobble(dt);
            DrawCells(target);
            flashing.Clear();
        }

        // Three backdrops slide at different rates and show wherever the ground has been dug away.
        private void DrawParallax(Vector2 camera)
        {
            int layer = GameData.LayerOfRow(Mathf.Max(0, Mathf.RoundToInt(-camera.y)));
            for (int p = 0; p < parallax.Length; p++)
            {
                var sr = parallax[p];
                sr.sprite = parallaxSprites[layer][p];
                sr.size = new Vector2(ParallaxTileUnits * 8, ParallaxTileUnits * 6);
                float rate = ParallaxRates[p];
                float x = camera.x - Mathf.Repeat(camera.x * rate, ParallaxTileUnits);
                float y = camera.y - Mathf.Repeat(camera.y * rate, ParallaxTileUnits);
                sr.transform.position = new Vector3(Stage.SnapToPixel(x), Stage.SnapToPixel(y), 0);
            }
        }

        // Only drills in view take a sprite, so a huge swarm costs no more to draw than a screenful.
        private void DrawDrills(Vector2 camera)
        {
            float halfWidth = Stage.Width / 2f / Stage.PixelsPerUnit + 1;
            float halfHeight = Stage.Height / 2f / Stage.PixelsPerUnit + 1;
            int used = 0;
            var units = sim.Units;
            for (int i = 0; i < units.Count && used < drillPool.Count; i++)
            {
                var unit = units[i];
                float x = (float)unit.Body.X, y = -(float)unit.Body.Y;
                if (Mathf.Abs(x - camera.x) > halfWidth || Mathf.Abs(y - camera.y) > halfHeight) continue;

                var sr = drillPool[used++];
                sr.enabled = true;
                var frames = drillFrames[unit.Code];
                sr.sprite = frames[((int)drillClock + i) % frames.Length]; // offset so they do not spin in step
                sr.color = sim.InFrenzy ? FrenzyTint : Color.white;
                // heat haze: a whole-pixel sideways wobble while in frenzy
                float haze = sim.InFrenzy ? Mathf.Round(Mathf.Sin(Time.unscaledTime * 30f + i) * 1.2f) / Stage.PixelsPerUnit : 0f;
                double age = sim.Now - unit.LastSwing;
                bool squash = unit.LastSwing >= 0 && age < SquashSeconds;
                sr.transform.localScale = squash ? new Vector3(SquashWidth, SquashHeight, 1) : Vector3.one;
                // the pivot stays put and the body swings around it; positive core angles tip the nose right
                sr.transform.SetPositionAndRotation(
                    new Vector3(Stage.SnapToPixel(x) + haze, Stage.SnapToPixel(y - DrillPivotBelowCenter), 0),
                    Quaternion.Euler(0, 0, (float)unit.Body.Angle));
            }
            for (int i = used; i < drillPool.Count; i++) drillPool[i].enabled = false;

            for (int i = 0; i < glows.Count; i++)
            {
                bool on = i < used;
                glows[i].enabled = on;
                if (on) glows[i].transform.position = drillPool[i].transform.position + new Vector3(0, 0.33f, 0);
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

        /// <summary>A random ore on screen, for the odd glint of light.</summary>
        public bool TryTwinkle(float dt, out Vector2 at)
        {
            at = default;
            twinkleClock += dt;
            if (twinkleClock < TwinkleSeconds || visibleOres.Count == 0) return false;
            twinkleClock = 0;
            at = visibleOres[Random.Range(0, visibleOres.Count)] + Random.insideUnitCircle * 0.4f;
            return true;
        }

        private void DrawCells(Vector2 camera)
        {
            visibleOres.Clear();
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
            int layer = inWorld ? WorldGen.LayerAt(sim.State.seed, row, col) : 0;
            // solid cells keep ground behind them so a nudged tile never shows the sky through the gap;
            // dug cells show the parallax, and open air above ground shows sky
            cell.Back.enabled = solid || row < 0;
            cell.Back.sprite = solid ? ground[layer][0] : plainTile;
            cell.Back.color = solid ? Color.white : SkyColor;
            cell.Tile.enabled = solid;
            cell.Crack.enabled = false;
            cell.Flash.enabled = false;
            cell.Overlay.enabled = false;
            for (int e = 0; e < cell.Edges.Length; e++)
            {
                cell.Edges[e].enabled = solid && IsHole(row + EdgeRowOffset[e], col + EdgeColOffset[e]);
                cell.Edges[e].sprite = edgeSprites[layer][e];
            }
            if (!solid) return;

            var block = sim.Row(row)[col];
            DrawBlock(cell, block, row, col, layer);

            cell.Flash.enabled = flashing.Contains(Key(row, col));
            if (wobble.ContainsKey(Key(row, col)))
                cell.Tile.transform.position = pos + new Vector3(0, -1f / Stage.PixelsPerUnit, 0);

            double ratio = block.Hp / block.MaxHp;
            if (block.Type == BlockType.BossRoot) ratio = sim.BossActive ? sim.BossHp / sim.BossMaxHp : 1;
            if (ratio > 0.66) return;
            cell.Crack.enabled = true;
            cell.Crack.sprite = cracks[ratio > 0.33 ? 0 : ratio > 0.1 ? 1 : 2];
        }

        private void DrawBlock(Cell cell, Block block, int row, int col, int layer)
        {
            cell.Tile.color = Color.white;
            var plain = ground[layer];
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
                    cell.Tile.sprite = loose[layer];
                    break;
                case BlockType.Ore:
                    ShowOverlay(cell, oreOverlays[block.Ore]);
                    visibleOres.Add(new Vector2(col, -row));
                    break;
                case BlockType.Dirt:
                    uint roll = WorldGen.Hash(sim.State.seed + 11, row, col);
                    if (roll % DecalOdds == 0) ShowOverlay(cell, decals[(roll >> 8) % (uint)decals.Length]);
                    break;
                case BlockType.HardRock:
                    ShowOverlay(cell, hardOverlay);
                    break;
                case BlockType.SuperHard:
                    ShowOverlay(cell, superHardOverlay);
                    break;
                case BlockType.Water:
                    cell.Tile.sprite = waterBlock;
                    break;
                case BlockType.Magma:
                    cell.Tile.sprite = magmaBlock;
                    break;
                case BlockType.Chest:
                    ShowOverlay(cell, chestOverlay);
                    break;
                case BlockType.BossRoot:
                    bool armored = sim.BossMaxArmor > 0 && (!sim.BossActive || sim.BossArmor > 0);
                    cell.Tile.sprite = armored ? bossShell : bossHide;
                    cell.Tile.color = Color.Lerp(Palette.Dark(layer), Color.white, 0.45f);
                    break;
            }
        }

        private static void ShowOverlay(Cell cell, Sprite sprite)
        {
            cell.Overlay.enabled = true;
            cell.Overlay.sprite = sprite;
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
