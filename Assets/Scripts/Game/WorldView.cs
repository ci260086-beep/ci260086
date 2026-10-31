using System.Collections.Generic;
using DeepDrill.Core;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// Draws the pixel ground as one texture per chunk, repainting only what changed, plus the
    /// parallax behind it and the swarm of drills. World x is the column, world y is minus the row.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        private const int ChunkCols = 8;
        private const int ChunkRows = 5;
        private const float DrillFramesPerSecond = 14f;
        private const int FirstGlowTier = 7;
        private const float TierGlowAlpha = 0.28f;
        private const float StarGlowAlpha = 0.42f;
        private const int Motes = 24;
        private const float MoteTurnRate = 5f;
        private const float MoteSpacing = 2.1f;
        private const float MoteRadius = 0.75f;
        private static readonly Color PlainGlow = new Color(1f, 0.8f, 0.45f, 0.14f);
        private const float CameraFollowRate = 6f;
        private const float DrillPivotBelowCenter = 0.33f; // sprites turn about a point near the tip
        private const float DrillPivotY = 1f / 12f;
        private const int DrillPool = 400;
        private const int RepaintsPerFrame = 4;
        private const int AssignsPerFrame = 1; // chunks just off screen are painted one at a time, ahead of need
        private const int VisibleCols = 7;
        private const int VisibleRows = 4;
        private const int GlowCount = 60;
        private const float ParallaxTileUnits = 4f; // 64 pixels
        private const float MaxShakePixels = 6f;
        private const float TraumaDecay = 1.8f;
        private const float TraumaPerStrength = 0.11f;
        private const float NoiseSpeed = 22f;
        private const double SquashSeconds = 0.09;
        private const float SquashWidth = 1.15f;
        private const float SquashHeight = 0.85f;
        private const float ChunkUnits = PixelChunk.Size / Stage.PixelsPerUnit;

        private static readonly Color FrenzyTint = new Color(1f, 0.55f, 0.5f);
        private static readonly Color BoundTint = new Color(0.55f, 0.45f, 0.35f);
        private static readonly Color BossWarnTint = new Color(1f, 0.25f, 0.2f);
        private const float BossWarnRate = 14f;
        private static readonly float[] ParallaxRates = { 0.2f, 0.45f, 0.7f };
        private static readonly string[] ParallaxNames = { "far", "mid", "near" };
        private static readonly Color SkyColor = new Color32(0x8f, 0xc9, 0xe6, 0xff);

        private sealed class ChunkSlot
        {
            public SpriteRenderer Renderer;
            public Texture2D Texture;
            public Color32[] Pixels;
            public long Key = long.MinValue;
        }

        private readonly List<ChunkSlot> slots = new List<ChunkSlot>();
        private readonly Dictionary<long, ChunkSlot> slotByChunk = new Dictionary<long, ChunkSlot>();
        private readonly List<SpriteRenderer> drillPool = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> glows = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> motes = new List<SpriteRenderer>();
        private readonly List<Vector3> stars = new List<Vector3>();
        private readonly Color[] drillGlows = new Color[DrillPool];

        private GameSim sim;
        private Camera cam;
        private Sprite[] bossFaces;
        private Dictionary<int, Sprite[]> drillFrames;
        private SpriteRenderer bossFace;
        private SpriteRenderer sky;
        private SpriteRenderer[] parallax;
        private Sprite[][] parallaxSprites;
        private float followX;
        private int repaintBudget;
        private float drillClock;
        private float trauma;

        public Vector2 CameraPosition { get; private set; }
        public float MaxDrawMs { get; set; }

        public void Init(GameSim simulation, ArtLibrary art, Camera camera)
        {
            sim = simulation;
            cam = camera;
            int layers = GameData.Layers.Count;

            drillFrames = new Dictionary<int, Sprite[]>();
            for (int f = 0; f < GameData.Families.Count; f++)
                for (int t = 0; t < GameData.Tiers; t++)
                    drillFrames[DrillCode.Make(f, t)] = art.Strip($"drill_{f}_{t}", 8, DrillPivotY);

            bossFaces = new Sprite[layers];
            for (int s = 0; s < layers; s++) bossFaces[s] = art.Get($"boss_face_{s}");
            bossFace = MakeRenderer("bossFace", 4, transform);

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

            sky = MakeRenderer("sky", 0, transform);
            sky.sprite = art.Get("plain_tile");
            sky.color = SkyColor;
            sky.drawMode = SpriteDrawMode.Tiled;
            sky.size = new Vector2(60, 30);

            for (int i = 0; i < ChunkCols * ChunkRows; i++) slots.Add(MakeSlot());

            for (int i = 0; i < GlowCount; i++)
            {
                var glow = MakeRenderer("glow", 5, transform);
                glow.sprite = art.Get("fx_circle");
                glow.color = PlainGlow;
                glow.transform.localScale = new Vector3(2.4f, 2.4f, 1);
                glow.enabled = false;
                glows.Add(glow);
            }
            for (int i = 0; i < Motes; i++)
            {
                var mote = MakeRenderer("mote", 7, transform);
                mote.sprite = art.Get("pixel");
                mote.color = new Color(1f, 0.95f, 0.7f);
                mote.enabled = false;
                motes.Add(mote);
            }
            for (int i = 0; i < DrillPool; i++)
            {
                var sr = MakeRenderer("drill", 6, transform);
                sr.enabled = false;
                drillPool.Add(sr);
            }
            followX = (float)sim.Lead.Body.X;
        }

        private ChunkSlot MakeSlot()
        {
            int size = PixelChunk.Size;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var slot = new ChunkSlot { Texture = texture, Pixels = new Color32[size * size] };
            slot.Renderer = MakeRenderer("chunk", 1, transform);
            slot.Renderer.sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0, 1), Stage.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            return slot;
        }

        /// <summary>Adds trauma; the screen moves by its square, so small hits stay subtle.</summary>
        public void Shake(float seconds, float strength)
        {
            trauma = Mathf.Min(1f, trauma + strength * TraumaPerStrength * Mathf.Clamp(seconds * 8f, 0.5f, 1.5f));
        }

        public void NoteHit(int row, int col) { }

        /// <summary>A random spot in view that holds ore, for the odd glint of light.</summary>
        public bool TryFindGem(out Vector2 at)
        {
            at = default;
            int x = Mathf.FloorToInt((CameraPosition.x + 0.5f + Random.Range(-10f, 4f)) * Stage.PixelsPerUnit);
            int y = Mathf.FloorToInt((-CameraPosition.y + 0.5f + Random.Range(-5.5f, 5.5f)) * Stage.PixelsPerUnit);
            if (!Mat.IsOre(sim.World.Get(x, y))) return false;
            at = new Vector2(x / Stage.PixelsPerUnit - 0.5f, -(y / Stage.PixelsPerUnit - 0.5f));
            return true;
        }

        /// <summary>Jumps the camera to the lead drill, for when it moves further than a pan should show.</summary>
        public void SnapToDrill() => followX = (float)sim.Lead.Body.X;

        public static Vector2 CellCenter(int row, int col) => new Vector2(col, -row);

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
            var timer = System.Diagnostics.Stopwatch.StartNew();
            DrawChunks(target);
            MaxDrawMs = Mathf.Max(MaxDrawMs, (float)timer.Elapsed.TotalMilliseconds);

            sky.transform.position = new Vector3(target.x, 15f - 0.5f, 0);

            var boss = sim.CurrentBoss;
            bossFace.enabled = boss != null;
            if (boss != null)
            {
                bossFace.sprite = bossFaces[boss.Index];
                // the guardian flashes as it winds up, the player's cue that a move is coming
                float flash = sim.BossMoveWindingUp ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * BossWarnRate) : 0f;
                bossFace.color = Color.Lerp(Color.white, BossWarnTint, flash);
                bossFace.transform.position = new Vector3(Mathf.Round(leadX), -(boss.RowEnd + 0.5f), 0);
            }
        }

        // Chunks on screen each take a slot; a slot only repaints what changed in its chunk.
        private void DrawChunks(Vector2 camera)
        {
            float left = camera.x + 0.5f - Stage.Width / 2f / Stage.PixelsPerUnit;
            float top = -camera.y + 0.5f - Stage.Height / 2f / Stage.PixelsPerUnit;
            int firstCol = Mathf.FloorToInt(left / ChunkUnits);
            int firstRow = Mathf.FloorToInt(top / ChunkUnits);

            foreach (var slot in slots) slot.Renderer.enabled = false;
            int used = 0;
            repaintBudget = RepaintsPerFrame;
            int assigns = AssignsPerFrame;
            for (int cy = Mathf.Max(0, firstRow); cy < firstRow + ChunkRows; cy++)
            {
                for (int cx = Mathf.Max(0, firstCol); cx < firstCol + ChunkCols; cx++)
                {
                    if (cx * PixelChunk.Size >= PixelWorld.Width) continue;
                    long key = PixelWorld.ChunkKey(cx, cy);
                    if (!slotByChunk.TryGetValue(key, out var slot) || slot.Key != key)
                    {
                        bool onScreen = cy < firstRow + VisibleRows && cx < firstCol + VisibleCols;
                        if (!onScreen && assigns <= 0) continue;
                        assigns--;
                        slot = FreeSlot(firstCol, firstRow);
                        if (slot == null) continue;
                        Assign(slot, key, cx, cy);
                    }
                    used++;
                    Refresh(slot, cx, cy);
                }
            }
        }

        private ChunkSlot FreeSlot(int firstCol, int firstRow)
        {
            foreach (var slot in slots)
            {
                if (slot.Key == long.MinValue) return slot;
                int cy = (int)(slot.Key / 4096), cx = (int)(slot.Key % 4096);
                if (cx < firstCol || cx >= firstCol + ChunkCols || cy < firstRow || cy >= firstRow + ChunkRows)
                {
                    slotByChunk.Remove(slot.Key);
                    return slot;
                }
            }
            return null;
        }

        private void Assign(ChunkSlot slot, long key, int cx, int cy)
        {
            slot.Key = key;
            slotByChunk[key] = slot;
            slot.Renderer.transform.position = new Vector3(cx * ChunkUnits - 0.5f, -(cy * ChunkUnits - 0.5f), 0);
            var chunk = sim.World.ChunkAt(cx, cy);
            PixelPainter.Paint(sim.World, cx, cy, chunk, slot.Pixels, sim.State.seed, 0, 0, PixelChunk.Size - 1, PixelChunk.Size - 1);
            chunk.Dirty = false;
            slot.Texture.SetPixels32(slot.Pixels);
            slot.Texture.Apply(false);
        }

        private void Refresh(ChunkSlot slot, int cx, int cy)
        {
            slot.Renderer.enabled = true;
            var chunk = sim.World.ChunkAt(cx, cy);
            if (!chunk.Dirty || repaintBudget <= 0) return; // the rest wait their turn next frame
            repaintBudget--;
            PixelPainter.Paint(sim.World, cx, cy, chunk, slot.Pixels, sim.State.seed, chunk.MinX, chunk.MinY, chunk.MaxX, chunk.MaxY);
            chunk.Dirty = false;
            slot.Texture.SetPixels32(slot.Pixels);
            slot.Texture.Apply(false);
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
                sr.color = sim.DrillsBound ? BoundTint : (sim.InFrenzy ? FrenzyTint : Color.white);
                int tier = DrillCode.Tier(unit.Code);
                drillGlows[used - 1] = GlowOf(tier);
                // heat haze: a whole-pixel sideways wobble while in frenzy
                float haze = sim.InFrenzy ? Mathf.Round(Mathf.Sin(Time.unscaledTime * 30f + i) * 1.2f) / Stage.PixelsPerUnit : 0f;
                bool squash = unit.LastSwing >= 0 && sim.Now - unit.LastSwing < SquashSeconds;
                sr.transform.localScale = squash ? new Vector3(SquashWidth, SquashHeight, 1) : Vector3.one;
                sr.transform.SetPositionAndRotation(
                    new Vector3(Stage.SnapToPixel(x) + haze, Stage.SnapToPixel(y - DrillPivotBelowCenter), 0),
                    Quaternion.Euler(0, 0, (float)unit.Body.Angle));
            }
            for (int i = used; i < drillPool.Count; i++) drillPool[i].enabled = false;
            stars.Clear();
            for (int i = 0; i < used; i++)
                if (drillGlows[i].a >= StarGlowAlpha) stars.Add(drillPool[i].transform.position + new Vector3(0, 0.33f, 0));

            for (int i = 0; i < glows.Count; i++)
            {
                bool on = i < used;
                glows[i].enabled = on;
                if (!on) continue;
                glows[i].transform.position = drillPool[i].transform.position + new Vector3(0, 0.33f, 0);
                glows[i].color = drillGlows[i];
            }

            // star-core drills keep a few sparks circling them
            int motesUsed = 0;
            for (int i = 0; i < stars.Count && motesUsed < motes.Count; i++)
            {
                var mote = motes[motesUsed];
                float angle = Time.unscaledTime * MoteTurnRate + motesUsed * MoteSpacing;
                mote.enabled = true;
                mote.transform.position = stars[i] + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * MoteRadius;
                motesUsed++;
            }
            for (int i = motesUsed; i < motes.Count; i++) motes[i].enabled = false;
        }

        // Crystal and above throw light in their own colour; the star core burns brightest.
        private static Color GlowOf(int tier)
        {
            if (tier < FirstGlowTier) return PlainGlow;
            var c = Palette.Tier(tier);
            c.a = tier == GameData.Tiers - 1 ? StarGlowAlpha : TierGlowAlpha;
            return c;
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
            float digUnits = Stage.DigWidth / Stage.PixelsPerUnit;
            x = Mathf.Clamp(x, -0.5f + halfWidth, Balance.Columns - 0.5f - digUnits + halfWidth);
            return new Vector2(Stage.SnapToPixel(x), Stage.SnapToPixel(y));
        }

        private static SpriteRenderer MakeRenderer(string goName, int order, Transform parent)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }
    }
}
