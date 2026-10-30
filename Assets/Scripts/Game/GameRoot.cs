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
        private const float HitstopCooldown = 0.12f;

        private static readonly Color DirtBits = new Color32(0x8c, 0x5f, 0x3f, 0xff);
        private static readonly Color RockBits = new Color32(0x8a, 0x8a, 0x98, 0xff);
        private static readonly Color CoalBits = new Color32(0x3a, 0x3a, 0x44, 0xff);
        private static readonly Color CopperBits = new Color32(0xe0, 0x8a, 0x4a, 0xff);
        private static readonly Color GrassBits = new Color32(0x76, 0xa8, 0x3a, 0xff);
        private static readonly Color ChestBits = new Color32(0xff, 0xe2, 0x7a, 0xff);
        private static readonly Color HitText = Color.white;
        private static readonly Color CoinText = new Color32(0xff, 0xe2, 0x7a, 0xff);

        private GameSim sim;
        private SaveService saves;
        private WorldView world;
        private FxView fx;
        private HudView hud;
        private ArtLibrary art;
        private float autosave;
        private int hitstop;
        private float hitstopCooldown;

        public GameSim Sim => sim;
        public SaveService Saves => saves;

        public void SaveNow() => saves.Save(sim.State);

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

            string savePath = CommandLine.Value("--save") ?? Path.Combine(Application.persistentDataPath, "save.json");
            saves = new SaveService(savePath);
            sim = new GameSim(saves.Load());
            double offline = sim.ApplyOffline(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var stageTexture = new RenderTexture(Stage.Width, Stage.Height, 16) { filterMode = FilterMode.Point };
            var camera = BuildCamera(stageTexture);

            hud = gameObject.AddComponent<HudView>();
            hud.Init(sim, art, stageTexture);

            world = new GameObject("World").AddComponent<WorldView>();
            world.Init(sim, art, camera);

            fx = new GameObject("Fx").AddComponent<FxView>();
            fx.Init(art, hud.FxLayer, hud.Font);

            if (offline > 0) hud.ShowNotice($"离线收益 +{NumberFormat.Short(offline)}", 5f);

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

        private void Update()
        {
            hitstopCooldown -= Time.deltaTime;
            if (hitstop > 0) hitstop--;
            else sim.Tick(Time.deltaTime);

            if (Input.GetMouseButtonDown(0)) HandleClick(Input.mousePosition);

            DrainEvents();

            autosave += Time.unscaledDeltaTime;
            if (autosave >= AutosaveSeconds)
            {
                autosave = 0;
                saves.Save(sim.State);
            }
        }

        private void OnApplicationQuit() => saves.Save(sim.State);

        /// <summary>Taps inside the dig area only; the bars and panels swallow their own clicks.</summary>
        public void HandleClick(Vector2 screen)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            Vector2 stage = Stage.ScreenToStage(screen);
            if (stage.y < Stage.TopBarHeight || stage.y >= Stage.BottomPanelTop) return;
            Vector2 worldPos = Stage.StageToWorld(stage, world.CameraPosition);
            if (world.TryCellAt(worldPos, out int row, out int col)) sim.Click(row, col);
        }

        /// <summary>Taps a block by grid position; used by the screenshot driver.</summary>
        public void ClickCell(int row, int col) => sim.Click(row, col);

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
                    world.NoteHit(e.Row, e.Col);
                    if (e.FromClick)
                    {
                        fx.FloatText(NumberFormat.Short(e.Amount), CellStage(e.Row, e.Col), HitText, 8);
                        fx.Debris(WorldView.CellCenter(e.Row, e.Col), BitsColor(e.Block), 2);
                    }
                    break;

                case SimEventKind.BlockBroken:
                    OnBroken(e);
                    break;
            }
        }

        private void OnBroken(in SimEvent e)
        {
            Vector2 center = WorldView.CellCenter(e.Row, e.Col);
            fx.Debris(center, BitsColor(e.Block), e.Block == BlockType.Chest ? 18 : 9);
            world.Shake(0.08f, 1f);

            if (hitstopCooldown <= 0)
            {
                hitstop = HitstopFrames;
                hitstopCooldown = HitstopCooldown;
            }

            Vector2 stage = CellStage(e.Row, e.Col);
            fx.FloatText("+" + NumberFormat.Short(e.Amount), stage, CoinText, e.Block == BlockType.Chest ? 11 : 8);

            bool ore = WorldGen.IsOre(e.Block);
            double value = e.Amount;
            double oreCount = ore ? 1 : 0;
            if (ore || e.Block == BlockType.Chest)
            {
                string icon = e.Block == BlockType.Coal ? "icon_coal"
                    : e.Block == BlockType.Copper ? "icon_copper" : "icon_coin";
                Vector2 target = ore ? hud.OreTarget : hud.CoinTarget;
                bool flying = fx.FlyIcon(art.Get(icon), stage, target, () => hud.Land(value, oreCount));
                if (flying) hud.AddInFlight(value, oreCount);
            }
        }

        private Vector2 CellStage(int row, int col) =>
            Stage.WorldToStage(WorldView.CellCenter(row, col), world.CameraPosition);

        private static Color BitsColor(BlockType type)
        {
            switch (type)
            {
                case BlockType.Grass: return GrassBits;
                case BlockType.HardRock: return RockBits;
                case BlockType.Coal: return CoalBits;
                case BlockType.Copper: return CopperBits;
                case BlockType.Chest: return ChestBits;
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
