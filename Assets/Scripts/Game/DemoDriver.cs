using System.Collections;
using System.IO;
using DeepDrill.Core;
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
        private const float LayerSeconds = 2.5f;

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

            sim.State.coins = 1e30;
            sim.Buy("root");
            sim.Buy("drill_01");
            sim.Buy("wealth_01");
            sim.Buy("blast_01");
            sim.State.maxDepthEver = 500;
            foreach (string id in new[] { "drill_02", "drill_03", "wealth_02", "hangar_01", "hangar_04", "frenzy_01", "strata_01", "blast_02" }) sim.Buy(id);
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

        private IEnumerator MeasureFrames()
        {
            float start = Time.realtimeSinceStartup;
            int frames = 0;
            float worst = 0;
            root.ResetTimers();
            int gcBefore = System.GC.CollectionCount(0);
            while (Time.realtimeSinceStartup - start < 3f)
            {
                frames++;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                yield return null;
            }
            Debug.Log($"[demo] swarm units={root.Sim.Units.Count} owned={root.Sim.State.drills.Count} " +
                $"depth={root.Sim.State.depth} fps={frames / 3f:0} worstFrameMs={worst * 1000:0} " +
                $"worstTickMs={root.MaxTickMs:0.0} worstPaintMs={root.MaxDrawMs:0.0} gen0={System.GC.CollectionCount(0) - gcBefore}");
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
