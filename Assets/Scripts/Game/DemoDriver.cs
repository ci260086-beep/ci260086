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
            yield return new WaitForSeconds(StartPlaySeconds);
            yield return Shot("start");

            sim.State.coins = 1e12;
            for (int i = 0; i < SwarmSize; i++) sim.BuyDrill(0);
            sim.Merge(DrillCode.Make(0, 0));
            yield return new WaitForSeconds(6f);
            yield return MeasureFrames();
            yield return Shot("swarm");

            sim.State.coins = 1e30;
            sim.Buy("root");
            sim.Buy("drill_01");
            sim.Buy("wealth_01");
            sim.Buy("blast_01");
            root.Hud.ShowTreePage();
            yield return new WaitForSeconds(0.3f);
            yield return Shot("tree");

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
            while (Time.realtimeSinceStartup - start < 3f)
            {
                frames++;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                yield return null;
            }
            Debug.Log($"[demo] swarm units={root.Sim.Units.Count} owned={root.Sim.State.drills.Count} " +
                $"depth={root.Sim.State.depth} fps={frames / 3f:0} worstFrameMs={worst * 1000:0}");
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
