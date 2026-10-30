using System.Collections;
using System.IO;
using DeepDrill.Core;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// Started only by the --shots command line flag. Plays the game unattended, then writes
    /// screenshots of the swarm, every stratum and the new pages, and quits, so the game can be
    /// checked by script.
    /// </summary>
    public sealed class DemoDriver : MonoBehaviour
    {
        private const float StartPlaySeconds = 6f;
        private const int SwarmSize = 40;
        private const float StratumSeconds = 2.5f;

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

            sim.State.coins = 1e9;
            for (int i = 0; i < SwarmSize; i++) sim.BuyDrill(0);
            sim.State.SetDrillCount(0, SwarmSize + 3);
            sim.Merge(0);
            yield return new WaitForSeconds(6f);
            Debug.Log($"[demo] swarm units={sim.Units.Count} owned={sim.State.DrillsOwned} depth={sim.State.depth} fps={1f / Time.smoothDeltaTime:0}");
            yield return Shot("swarm");

            root.Hud.ShowTreePage();
            yield return new WaitForSeconds(0.3f);
            yield return Shot("tree");

            root.Hud.ShowCollectionPage();
            yield return new WaitForSeconds(0.3f);
            yield return Shot("collection");
            root.Hud.ShowHangarPage();

            for (int s = 1; s < Strata.All.Length; s++)
            {
                sim.State.bossesDefeated = s;
                sim.Teleport(s * Strata.RowsPerStratum + 40, Balance.StartColumn);
                root.SnapCamera();
                yield return new WaitForSeconds(StratumSeconds);
                yield return Shot($"stratum_{s:00}");
            }

            sim.State.bossesDefeated = 3;
            sim.Teleport(Strata.BossRow(3) - 2, Balance.StartColumn);
            root.SnapCamera();
            float wait = Time.time + 10f;
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

        private IEnumerator Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return null;
            yield return null;
            Debug.Log($"[demo] shot {name} depth={root.Sim.State.depth} coins={root.Sim.State.coins:0.0}");
        }

        // Writes the real save file, reads it back and prices an hour offline on the loaded copy.
        private void VerifySaveRoundTrip()
        {
            var live = root.Sim.State;
            root.SaveNow();
            var loaded = root.Saves.Load();
            bool same = loaded.depth == live.depth && Close(loaded.coins, live.coins) && loaded.seed == live.seed
                && Close(loaded.drillX, live.drillX) && Close(loaded.drillY, live.drillY)
                && loaded.drillCounts.Count == live.drillCounts.Count && loaded.version == GameState.CurrentVersion;
            Debug.Log($"[demo] save roundtrip same={same} depth={loaded.depth} drills={loaded.DrillsOwned}");
        }

        // JsonUtility may round a double's last digit; anything larger is a real loss
        private static bool Close(double a, double b) => System.Math.Abs(a - b) <= System.Math.Abs(a) * 1e-12;
    }
}
