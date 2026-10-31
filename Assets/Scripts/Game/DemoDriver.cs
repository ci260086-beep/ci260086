using System.Collections;
using System.IO;
using DeepDrill.Core;
using Unity.Profiling;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// Started only by the --shots command line flag. Plays the game unattended, then writes
    /// screenshots of the swarm, every layer and the new pages, and quits, so the game can be
    /// checked by script.
    /// </summary>
    public sealed class DemoDriver : MonoBehaviour
    {
        private const float StartPlaySeconds = 5f;
        private const int SwarmSize = 200;
        private const int MergeShotTier = 6;
        private const float LayerSeconds = 2.5f;
        private const int FullSwarm = 1000;
        private const float MeasureSeconds = 5f;
        private const int SlowFrames = 4;
        private const long PartWorthNaming = 1_000_000; // one millisecond

        private static readonly string[] FrameParts =
        {
            "PlayerLoop", "EditorLoop", "GC.Collect", "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate",
            "PostLateUpdate.PlayerUpdateCanvases", "Canvas.SendWillRenderCanvases", "Canvas.BuildBatch",
            "PostLateUpdate.PlayerSendFrameComplete", "Camera.Render", "UGUI.Rendering.RenderOverlays", "Gfx.WaitForPresentOnGfxThread",
            "WaitForTargetFPS", "Physics2D.Simulate", "Texture2D.Apply",
        };

        private GameRoot root;
        private string folder;

        public void Init(GameRoot gameRoot, string shotFolder)
        {
            root = gameRoot;
            folder = shotFolder;
            Directory.CreateDirectory(folder);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var sim = root.Sim;
            yield return new WaitForSeconds(1f);
            yield return Shot("intro_tap");

            // tap the drill like a player until the first coins arrive
            float tapUntil = Time.time + StartPlaySeconds;
            while (sim.State.coins < 1 && Time.time < tapUntil)
            {
                root.ClickCell(sim.Lead.Body.BodyRow + 1, sim.Lead.Body.Column);
                yield return new WaitForSeconds(0.05f);
            }
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[demo] tapped coins={sim.State.coins:0.0} auto={sim.AutoDrillUnlocked}");
            yield return Shot("intro_coins");

            sim.State.coins = 15;
            sim.Buy(GameSim.RootSkill);
            yield return new WaitForSeconds(4f);
            Debug.Log($"[demo] unlocked auto={sim.AutoDrillUnlocked} depth={sim.State.depth}");
            yield return Shot("unlocked");

            // set off a charge beside the drill to catch the explosion mid-air
            sim.Detonate(sim.Lead.Body.FeetRow + 3, sim.Lead.Body.Column + 4);
            yield return new WaitForSeconds(0.12f);
            yield return Shot("blast");

            sim.State.coins = 1e12;
            for (int i = 0; i < SwarmSize; i++) sim.BuyDrill(0);
            sim.Merge(DrillCode.Make(0, 0));
            yield return new WaitForSeconds(6f);
            yield return MeasureFrames();
            yield return Shot("swarm");

            sim.FillFrenzy();
            sim.StartFrenzy();
            yield return new WaitForSeconds(1f);
            yield return Shot("frenzy");

            // one drill of every material, so the hangar shows the whole ladder
            for (int t = 1; t < GameData.Tiers; t++) sim.State.drills.Add(DrillCode.Make(t % GameData.Families.Count, t));
            sim.SyncDrills();
            root.Hud.ShowMerged(DrillCode.Make(0, MergeShotTier));
            yield return new WaitForSeconds(0.4f);
            yield return Shot("merge");

            while (sim.State.drills.Count < FullSwarm) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.SyncDrills();
            yield return new WaitForSeconds(3f);
            yield return MeasureFrames();
            yield return Shot("swarm_full");

            sim.State.coins = 1e30;
            sim.Buy("root");
            sim.Buy("drill_01");
            sim.Buy("wealth_01");
            sim.Buy("blast_01");
            sim.State.maxDepthEver = 500;
            foreach (string id in new[] { "drill_02", "drill_03", "wealth_02", "hangar_01", "hangar_04", "frenzy_01", "strata_01", "blast_02" }) sim.Buy(id);
            root.Hud.ShowOffline(new OfflineReport { SecondsAway = 11520, SecondsPaid = 7200, Rate = 0.5, Coins = 128000 });
            yield return new WaitForSeconds(1f);
            yield return Shot("offline");
            root.Hud.CloseOffline();
            root.Hud.ShowTreePage();
            yield return new WaitForSeconds(0.6f);
            yield return Shot("tree");
            root.Hud.HoverSkill("drill_01");
            yield return new WaitForSeconds(0.2f);
            yield return Shot("tree_hover");
            root.Hud.BurstSkill("strata_01");
            yield return new WaitForSeconds(0.1f);
            yield return Shot("tree_burst");
            root.Hud.FocusBranch("hangar");
            yield return new WaitForSeconds(1f);
            yield return Shot("tree_focus");
            root.Hud.CenterTree();
            root.Hud.ZoomTree(-10);
            yield return new WaitForSeconds(1f);
            yield return Shot("tree_full");

            sim.State.AddRare("rare_1_1");
            sim.State.AddRare("rare_2_2");
            root.Hud.ShowGalleryPage();
            yield return new WaitForSeconds(0.3f);
            yield return Shot("gallery");
            root.Hud.ShowHangarPage();

            for (int s = 1; s < GameData.Layers.Count; s++)
            {
                var layer = GameData.Layers[s];
                sim.State.bossesDefeated = s;
                sim.Teleport((layer.RowStart + layer.RowEnd) / 2, Balance.StartColumn);
                root.SnapCamera();
                if (s == 1)
                {
                    // open the sand from below and catch it pouring
                    int mid = (layer.RowStart + layer.RowEnd) / 2 + 2;
                    sim.World.CarveRect(16 * 16, (mid + 1) * 16, 24 * 16, (mid + 5) * 16);
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot("sand");
                }
                yield return new WaitForSeconds(LayerSeconds);
                yield return Shot($"layer_{s + 1:00}");
            }

            sim.State.bossesDefeated = 2;
            sim.Teleport(GameData.Layers[2].RowEnd - 1, Balance.StartColumn);
            root.SnapCamera();
            float wait = Time.time + 8f;
            while (!sim.BossActive && Time.time < wait) yield return null;
            yield return new WaitForSeconds(1f);
            yield return Shot("boss");

            VerifySaveRoundTrip();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }

        // Runs the frame clock for a few seconds and logs the slowest frames, part by part.
        private IEnumerator MeasureFrames()
        {
            var recorders = new ProfilerRecorder[FrameParts.Length];
            for (int i = 0; i < recorders.Length; i++)
                recorders[i] = ProfilerRecorder.StartNew(ProfilerCategory.Internal, FrameParts[i]);
            var allocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            var slowest = new float[SlowFrames];
            var parts = new string[SlowFrames];
            float start = Time.realtimeSinceStartup;
            int frames = 0;
            float worst = 0;
            yield return null; // the first frame back still carries the screenshot before it
            root.ResetTimers();
            int gcBefore = System.GC.CollectionCount(0);
            while (Time.realtimeSinceStartup - start < MeasureSeconds)
            {
                frames++;
                float ms = Time.unscaledDeltaTime * 1000f;
                worst = Mathf.Max(worst, ms);
                int slot = System.Array.IndexOf(slowest, Mathf.Min(slowest));
                if (ms > slowest[slot])
                {
                    slowest[slot] = ms;
                    var text = new System.Text.StringBuilder($"{ms:0.0}ms alloc={allocated.LastValue / 1024}KB");
                    for (int i = 0; i < recorders.Length; i++)
                        if (recorders[i].LastValue >= PartWorthNaming) text.Append($" {FrameParts[i]}={recorders[i].LastValue / 1e6:0.0}");
                    parts[slot] = text.ToString();
                }
                yield return null;
            }
            Debug.Log($"[demo] swarm units={root.Sim.Units.Count} owned={root.Sim.State.drills.Count} " +
                $"depth={root.Sim.State.depth} fps={frames / MeasureSeconds:0} worstFrameMs={worst:0} " +
                $"worstTickMs={root.MaxTickMs:0.0} worstPaintMs={root.MaxDrawMs:0.0} gen0={System.GC.CollectionCount(0) - gcBefore}");
            foreach (string line in parts)
                if (line != null) Debug.Log("[demo] slow frame " + line);
            foreach (var recorder in recorders) recorder.Dispose();
            allocated.Dispose();
        }

        private IEnumerator Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return null;
            yield return null;
            Debug.Log($"[demo] shot {name} depth={root.Sim.State.depth} coins={root.Sim.State.coins:0.0}");
        }

        // Writes the real save file, reads it back and checks the fields that matter.
        private void VerifySaveRoundTrip()
        {
            var live = root.Sim.State;
            root.SaveNow();
            var loaded = root.Saves.Load();
            bool same = loaded.depth == live.depth && Close(loaded.coins, live.coins) && loaded.seed == live.seed
                && Close(loaded.drillX, live.drillX) && Close(loaded.drillY, live.drillY)
                && loaded.drills.Count == live.drills.Count && loaded.version == GameState.CurrentVersion;
            Debug.Log($"[demo] save roundtrip same={same} depth={loaded.depth} drills={loaded.drills.Count}");
        }

        // JsonUtility may round a double's last digit; anything larger is a real loss
        private static bool Close(double a, double b) => System.Math.Abs(a - b) <= System.Math.Abs(a) * 1e-12;
    }
}
