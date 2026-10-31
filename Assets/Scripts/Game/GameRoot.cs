using System;
using System.Collections.Generic;
using System.IO;
using DeepDrill.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DeepDrill.Game
{
    /// <summary>Boots the game and feeds simulation events to the views.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        private const float AutosaveSeconds = 30f;
        private const int HitstopFrames = 2;
        private const float HitstopCooldown = 0.5f;
        private const int DepthMilestone = 50;
        private const int MaxBitsPerSwing = 4;
        private const int GemProbes = 1;
        private const float SlowMotionSeconds = 1f;
        private const float SlowMotionRate = 0.2f;
        private const int LootFountain = 24;
        private const int LootOreEvery = 3;
        private const float LootFlashSize = 6f;
        private const int BusySwarm = 50; // past this, only a third of the swings get full effects
        private const int QuietCritDrills = 20; // past this, drill crits would only be noise
        private const double HardLandingSpeed = 14;
        private const double SoftLandingSpeed = 10;

        private static readonly Color DirtBits = new Color32(0x8c, 0x5f, 0x3f, 0xff);
        private static readonly Color RockBits = new Color32(0x8a, 0x8a, 0x98, 0xff);
        private static readonly Color OreBits = new Color32(0xe0, 0x8a, 0x4a, 0xff);
        private static readonly Color GrassBits = new Color32(0x76, 0xa8, 0x3a, 0xff);
        private static readonly Color ChestBits = new Color32(0xff, 0xe2, 0x7a, 0xff);
        private static readonly Color TntBits = new Color32(0xb8, 0x2c, 0x2c, 0xff);
        private static readonly Color SandBits = new Color32(0xe8, 0xcc, 0x84, 0xff);
        private static readonly Color HitText = Color.white;
        private static readonly Color BossText = new Color32(0xff, 0x50, 0x48, 0xff);
        private static readonly Color CoinText = new Color32(0xff, 0xe2, 0x7a, 0xff);

        private GameSim sim;
        private SaveService saves;
        private WorldView world;
        private FxView fx;
        private SoundBank sound;
        private HudView hud;
        private ArtLibrary art;
        private float autosave;
        private int hitstop;
        private float slowMotion;
        private float hitstopCooldown;

        public GameSim Sim => sim;
        public float MaxTickMs { get; set; }
        public float MaxDrawMs => world.MaxDrawMs;
        public void ResetTimers()
        {
            MaxTickMs = 0;
            world.MaxDrawMs = 0;
        }
        public SaveService Saves => saves;
        public HudView Hud => hud;

        public void SaveNow()
        {
            sim.PackHoles();
            saves.Save(sim.State);
        }

        public void SnapCamera() => world.SnapToDrill();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("GameRoot");
            go.AddComponent<GameRoot>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            art = Resources.Load<ArtLibrary>("ArtLibrary");
            GameData.Load(name => Resources.Load<TextAsset>("Data/" + name).text);

            string savePath = CommandLine.Value("--save") ?? Path.Combine(Application.persistentDataPath, "save.json");
            saves = new SaveService(savePath);
            sim = new GameSim(saves.Load());
            double offline = sim.ApplyOffline(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var stageTexture = new RenderTexture(Stage.Width, Stage.Height, 16) { filterMode = FilterMode.Point };
            var camera = BuildCamera(stageTexture);
            BuildScreenCamera();

            hud = gameObject.AddComponent<HudView>();
            hud.Init(sim, art, stageTexture);

            world = new GameObject("World").AddComponent<WorldView>();
            world.Init(sim, art, camera);

            fx = new GameObject("Fx").AddComponent<FxView>();
            fx.Init(art, hud.FxLayer, hud.Font);

            sound = gameObject.AddComponent<SoundBank>();
            sound.Init();

            hud.SaveRequested += SaveNow;
            if (offline > 0) hud.ShowOffline(sim.LastOffline);

            string shots = CommandLine.Value("--shots");
            if (shots != null) gameObject.AddComponent<DemoDriver>().Init(this, shots);
        }

        private static Camera BuildCamera(RenderTexture target)
        {
            var go = new GameObject("Camera");
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Stage.Height / 2f / Stage.PixelsPerUnit;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x8f, 0xc9, 0xe6, 0xff);
            camera.targetTexture = target;
            return camera;
        }

        // The stage camera draws into a texture, so nothing would draw to the display itself and the
        // editor would print "No cameras rendering" over the overlay UI. This one only clears it.
        private static void BuildScreenCamera()
        {
            var camera = new GameObject("ScreenCamera").AddComponent<Camera>();
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.depth = -100;
            camera.useOcclusionCulling = false;
        }

        private void Update()
        {
            hitstopCooldown -= Time.deltaTime;
            slowMotion -= Time.unscaledDeltaTime;
            float simStep = Time.deltaTime * (slowMotion > 0 ? SlowMotionRate : 1f);
            fx.Density = GameSettings.ParticleScale * (sim.Units.Count > BusySwarm ? 1f / 3f : 1f);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            if (hitstop > 0) hitstop--;
            else sim.Tick(simStep);
            MaxTickMs = Mathf.Max(MaxTickMs, (float)timer.Elapsed.TotalMilliseconds);

            if (Input.GetMouseButtonDown(0)) HandleClick(Input.mousePosition);
            if (Input.GetKeyDown(KeyCode.Space)) sim.StartFrenzy();

            DrainEvents();

            fx.Ambient(GameData.LayerOfRow(sim.State.depth), world.CameraPosition, Time.unscaledDeltaTime);
            for (int i = 0; i < GemProbes; i++)
                if (world.TryFindGem(out Vector2 glint)) fx.Twinkle(glint);

            autosave += Time.unscaledDeltaTime;
            if (autosave >= AutosaveSeconds)
            {
                autosave = 0;
                SaveNow();
            }
        }

        private void OnApplicationQuit() => SaveNow();

        /// <summary>Taps inside the dig area only; the bars and panels swallow their own clicks.</summary>
        public void HandleClick(Vector2 screen)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            Vector2 stage = Stage.ScreenToStage(screen);
            if (!Stage.InDigArea(stage)) return;
            Vector2 worldPos = Stage.StageToWorld(stage, world.CameraPosition);
            sim.ClickPixel(Mathf.FloorToInt((worldPos.x + 0.5f) * Stage.PixelsPerUnit),
                Mathf.FloorToInt((-worldPos.y + 0.5f) * Stage.PixelsPerUnit));
        }

        /// <summary>Taps a block by grid position; used by the screenshot driver.</summary>
        public void ClickCell(int row, int col) => sim.ClickPixel(col * 16 + 8, row * 16 + 8);

        private void DrainEvents()
        {
            var events = sim.Events;
            for (int i = 0; i < events.Count; i++) Present(events[i]);
            events.Clear();
        }

        private void Present(in SimEvent e)
        {
            switch (e.Kind)
            {
                case SimEventKind.BlockHit:
                    OnHit(e);
                    break;

                case SimEventKind.BlockBroken:
                    OnBroken(e);
                    break;

                case SimEventKind.Explosion:
                    fx.Explosion(WorldView.CellCenter(e.Row, e.Col), (int)e.Amount);
                    fx.Debris(WorldView.CellCenter(e.Row, e.Col), OreBits, 22);
                    world.Shake(0.2f, 3f);
                    sound.Play("blast", 0.7f, 0, true);
                    break;

                case SimEventKind.FrenzyStarted:
                    hud.ShowToast("狂热模式！", 1.5f);
                    world.Shake(0.25f, 2f);
                    break;

                case SimEventKind.DepthChanged:
                    if (e.Row % DepthMilestone == 0) hud.ShowToast($"挖到 {GameData.FormatDepth(e.Row)} 了", 3f);
                    break;

                case SimEventKind.Landed:
                    OnLanded(e);
                    break;

                case SimEventKind.BossStarted:
                    hud.ShowToast($"{sim.CurrentBoss.BossName}！{sim.BossDuration:0} 秒内打穿它", 3f);
                    world.Shake(0.3f, 3f);
                    sound.Play("boss", 0.8f, 0, true);
                    hud.ShowBossBanner(sim.CurrentBoss.BossName, $"第 {sim.CurrentBoss.Index + 1} 层守卫 · 招式：{sim.CurrentBossMove.Name}");
                    break;

                case SimEventKind.BossFailed:
                    hud.ShowToast($"{sim.CurrentBoss.BossName}恢复了，升级后再来", 3f);
                    break;

                case SimEventKind.BossMoveWarned:
                    hud.ShowToast($"{sim.CurrentBoss.BossName}要放{sim.CurrentBossMove.Name}了！", 1.5f);
                    sound.Play("boss", 0.4f, 0.3f, true);
                    break;

                case SimEventKind.BossMoveStruck:
                    hud.ShowToast(sim.CurrentBossMove.Desc, 2.5f);
                    world.Shake(0.3f, 4f);
                    break;

                case SimEventKind.BossMoveEnded:
                    break;

                case SimEventKind.BossArmorBroken:
                    hud.ShowToast("甲壳碎了！", 2f);
                    world.Shake(0.3f, 4f);
                    break;

                case SimEventKind.DrillBoosted:
                    if (fx.Allow()) fx.Sparks(WorldView.CellCenter(e.Row, e.Col) + Vector2.down * 0.4f, 1);
                    break;

                case SimEventKind.PixelsDug:
                    OnPixelsDug(e);
                    break;

                case SimEventKind.Overheated:
                    hud.ShowToast("过热！开启狂热可以散热", 2f);
                    break;

                case SimEventKind.LayerEntered:
                    hud.ShowToast($"进入{GameData.Layers[(int)e.Amount].Name}", 3.5f);
                    break;

                case SimEventKind.BossDefeated:
                    hud.ShowToast($"打穿了！+{NumberFormat.Short(e.Amount)} 金币", 4f);
                    fx.Explosion(WorldView.CellCenter(e.Row, e.Col), Balance.BossBreachHalfWidth);
                    fx.Flash(WorldView.CellCenter(e.Row, e.Col), LootFlashSize);
                    fx.Sparks(WorldView.CellCenter(e.Row, e.Col), LootFountain);
                    fx.Debris(WorldView.CellCenter(e.Row, e.Col), DirtBits, 60);
                    world.Shake(0.4f, 8f);
                    sound.Play("blast", 0.9f, -0.2f, true);
                    slowMotion = SlowMotionSeconds;
                    Vector2 bossStage = CellStage(e.Row, e.Col);
                    for (int i = 0; i < LootFountain; i++)
                        fx.FlyIcon(art.Get(i % LootOreEvery == 0 ? "icon_copper" : "icon_coin"), bossStage,
                            i % LootOreEvery == 0 ? hud.OreTarget : hud.CoinTarget, () => hud.Land(0, 0));
                    break;

                case SimEventKind.SkillBought:
                    sound.Play("upgrade", 0.6f, 0, true);
                    break;

                case SimEventKind.DrillMerged:
                    sound.Play("merge", 0.6f);
                    OnMerged();
                    hud.ShowMerged((int)e.Amount);
                    hud.ShowToast($"合成了{HudView.KindName((int)e.Amount)}", 2f);
                    break;

                case SimEventKind.RareFound:
                    hud.ShowToast($"发现稀有物品：{GameData.Rares[(int)e.Amount].Name}", 4f);
                    break;

                case SimEventKind.AchievementUnlocked:
                    hud.ShowToast($"成就：{GameData.Achievements[(int)e.Amount].Name}", 3f);
                    break;

                case SimEventKind.SurfaceEventAppeared:
                    sound.Play("upgrade", 0.5f, 0.4f, true);
                    break;

                case SimEventKind.SurfaceEventClaimed:
                    sound.Play("merge", 0.7f);
                    world.Shake(0.15f, 2f);
                    break;

                case SimEventKind.SchoolCleared:
                    hud.ShowToast($"{GameData.Schools[(int)e.Amount].Name}通关：{GameData.Schools[(int)e.Amount].Reward.Desc}", 5f);
                    break;

                case SimEventKind.ResearchDone:
                    hud.ShowToast($"研究完成：{GameData.Research[(int)e.Amount].Name}", 4f);
                    break;

                case SimEventKind.ExpeditionReturned:
                    hud.ShowToast($"{GameData.Expeditions[(int)e.Amount].Name}{(e.Row > 0 ? "成功归来" : "空手而归")}", 4f);
                    break;

                case SimEventKind.ExplorerRescued:
                    hud.ShowToast("救出一名探险员", 4f);
                    break;

                case SimEventKind.ChallengeDone:
                    hud.ShowToast($"挑战完成：{GameData.Challenges[(int)e.Amount].Reward.Desc}", 5f);
                    break;

                case SimEventKind.RunRestarted:
                    hud.ClearInFlight();
                    world.SnapToDrill();
                    SaveNow();
                    break;

                case SimEventKind.Warped:
                    hud.ShowToast($"星系跃迁：抵达{sim.Planet.Name}，获得 {e.Amount:0} 星点", 5f);
                    break;

                case SimEventKind.MilestoneReached:
                    hud.ShowToast($"里程碑：{GameData.Milestones[(int)e.Amount][e.Row].Desc}", 3f);
                    break;

                case SimEventKind.MasteryUp:
                    hud.ShowToast($"{GameData.Layers[(int)e.Amount].Name}精通升到 {e.Row} 级", 3f);
                    break;

                case SimEventKind.Rebirth:
                    hud.ClearInFlight();
                    hud.ShowToast($"地核重铸：获得 {NumberFormat.Short(e.Amount)} 星核碎片", 4f);
                    world.SnapToDrill();
                    SaveNow();
                    break;
            }
        }

        // Only the player's own taps and rare moments shake the screen, or a swarm never stops.
        private void OnHit(in SimEvent e)
        {
            world.NoteHit(e.Row, e.Col);
            if (e.FromClick) fx.Flash(WorldView.CellCenter(e.Row, e.Col), 1.2f); // the rock blinks white
            bool boss = e.Block == BlockType.BossRoot;
            if (e.FromClick)
            {
                string text = NumberFormat.Short(e.Amount) + (e.Crit ? "!" : "");
                var style = e.Crit ? FxView.Text3.Crit : (boss ? FxView.Text3.Boss : FxView.Text3.Normal);
                fx.FloatText(text, CellStage(e.Row, e.Col), boss ? BossText : (e.Crit ? CoinText : HitText), e.Crit ? 13 : 8, style);
                fx.Debris(WorldView.CellCenter(e.Row, e.Col), BitsColor(e.Block), 2);
                if (e.Crit) world.Shake(0.14f, 2f);
            }
            else if (e.Crit && sim.Units.Count <= QuietCritDrills)
            {
                fx.FloatText(NumberFormat.Short(e.Amount) + "!", CellStage(e.Row, e.Col), CoinText, 10, FxView.Text3.Crit);
            }
            if ((e.Block == BlockType.HardRock || e.Block == BlockType.SuperHard) && fx.Allow())
                fx.Sparks(WorldView.CellCenter(e.Row, e.Col), 3);
        }

        // Dug pixels fly off in the colour of what they were.
        private void OnPixelsDug(in SimEvent e)
        {
            if (!fx.Allow()) return;
            int bits = Mathf.Min(MaxBitsPerSwing, 1 + (int)e.Amount / 8);
            var at = new Vector2(e.Col / 16f - 0.5f, -(e.Row / 16f - 0.5f));
            fx.Debris(at, PixelPainter.ColorOf((byte)e.Ore, GameData.LayerOfRow(e.Row / 16), e.Col, e.Row), bits);
        }

        // The new drill flashes where it stands, throws out a star of sparks and announces itself.
        private void OnMerged()
        {
            Vector2 at = new Vector2((float)sim.Lead.Body.X, -(float)sim.Lead.Body.Y);
            fx.Flash(at, 2.5f);
            fx.Sparks(at, 10);
            fx.FloatText("升级！", CellStage(sim.Lead.Body.BodyRow - 1, sim.Lead.Body.Column), CoinText, 11, FxView.Text3.Ore);
        }

        // A one-row drop is the normal dig rhythm; only longer falls earn a thump.
        private void OnLanded(in SimEvent e)
        {
            if (e.Amount < SoftLandingSpeed) return;
            bool hard = e.Amount >= HardLandingSpeed;
            var feet = new Vector2(e.Col, -e.Row + 0.5f);
            fx.Debris(feet, DirtBits, hard ? 14 : 6);
            if (e.Lead) world.Shake(hard ? 0.28f : 0.12f, hard ? 4f : 2f);
        }

        private void OnBroken(in SimEvent e)
        {
            Vector2 center = WorldView.CellCenter(e.Row, e.Col);
            fx.Debris(center, BitsColor(e.Block), e.Block == BlockType.Chest ? 18 : 6);
            fx.Puff(center);
            sound.Play(e.Block == BlockType.HardRock || e.Block == BlockType.SuperHard ? "hard" : "break", 0.35f);

            if (e.FromClick && hitstopCooldown <= 0)
            {
                // a bigger hit holds the frame longer
                bool big = e.Block == BlockType.HardRock || e.Block == BlockType.SuperHard;
                hitstop = e.Block == BlockType.Chest ? 3 : (big ? HitstopFrames : 1);
                hitstopCooldown = HitstopCooldown;
                world.Shake(0.08f, big ? 2f : 1f);
            }

            Vector2 stage = CellStage(e.Row, e.Col);
            if (e.Block == BlockType.Ore || e.Block == BlockType.Chest)
            {
                var color = e.Block == BlockType.Ore ? OreTextColor(e.Ore) : CoinText;
                fx.FloatText("+" + NumberFormat.Short(e.Amount), stage, color, e.Block == BlockType.Chest ? 11 : 8, FxView.Text3.Ore);
                sound.Play("coin", 0.3f, Mathf.Min(0.6f, sim.Combo * 0.004f));
            }

            bool ore = e.Block == BlockType.Ore;
            double value = e.Amount;
            double oreCount = ore ? 1 : 0;
            if (ore || e.Block == BlockType.Chest)
            {
                string icon = ore ? $"oreicon_{e.Ore}" : "icon_coin";
                Vector2 target = ore ? hud.OreTarget : hud.CoinTarget;
                bool flying = fx.FlyIcon(art.Get(icon), stage, target, () => hud.Land(value, oreCount));
                if (flying) hud.AddInFlight(value, oreCount);
            }
        }

        private Vector2 CellStage(int row, int col) =>
            Stage.WorldToStage(WorldView.CellCenter(row, col), world.CameraPosition);

        // mid tones of the 24 ores, in design order
        private static readonly Color32[] OreColors =
        {
            new Color32(0x9a, 0x9a, 0xa8, 255), new Color32(0xe0, 0x8a, 0x4a, 255), new Color32(0xc0, 0xc0, 0xcc, 255),
            new Color32(0xe8, 0xe8, 0xf0, 255), new Color32(0xb0, 0x90, 0xf0, 255), new Color32(0xe8, 0xb0, 0x2a, 255),
            new Color32(0xe0, 0xd8, 0xe0, 255), new Color32(0x60, 0x90, 0xf0, 255), new Color32(0xb0, 0x8c, 0x38, 255),
            new Color32(0xe8, 0x30, 0x5a, 255), new Color32(0xc8, 0xc8, 0xd8, 255), new Color32(0x6a, 0xa8, 0xc0, 255),
            new Color32(0x90, 0x98, 0xb0, 255), new Color32(0xc8, 0xd0, 0xe4, 255), new Color32(0x90, 0xc0, 0x40, 255),
            new Color32(0x90, 0xe8, 0xf8, 255), new Color32(0x50, 0x60, 0xe0, 255), new Color32(0xe8, 0x40, 0x80, 255),
            new Color32(0x80, 0xb0, 0x50, 255), new Color32(0xc0, 0x50, 0xb0, 255), new Color32(0xe8, 0xc8, 0x70, 255),
            new Color32(0x60, 0xb0, 0xf0, 255), new Color32(0xff, 0xe2, 0x7a, 255), new Color32(0xc0, 0x5c, 0xff, 255),
        };

        private static Color OreTextColor(int ore) => OreColors[ore];

        private static Color BitsColor(BlockType type)
        {
            switch (type)
            {
                case BlockType.Grass: return GrassBits;
                case BlockType.HardRock: return RockBits;
                case BlockType.Ore: return OreBits;
                case BlockType.Sand: return SandBits;
                case BlockType.Chest: return ChestBits;
                case BlockType.Tnt: return TntBits;
                default: return DirtBits;
            }
        }
    }

    public static class CommandLine
    {
        public static string Value(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == flag) return args[i + 1];
            return null;
        }
    }
}
