using System.Collections;
using System.IO;
using DeepDrill.Core;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// Started only by the --shots command line flag. Plays the game unattended, then writes
    /// screenshots of normal digging and of the drill tipping into a carved hole, and quits, so
    /// the game can be checked by script.
    /// </summary>
    public sealed class DemoDriver : MonoBehaviour
    {
        private const float SecondsPerTap = 0.25f;
        private const float NormalPlaySeconds = 12f;
        private const double TiltShotAngle = -15;
        private const float TiltTimeout = 10f;

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
            float start = Time.time;
            float nextTap = 0;
            while (Time.time - start < NormalPlaySeconds)
            {
                if (Time.time >= nextTap)
                {
                    nextTap = Time.time + SecondsPerTap;
                    Play();
                }
                yield return null;
            }
            yield return Shot("normal");

            CarveLeftPocket();
            float tiltStart = Time.time;
            while (root.Sim.Drill.Angle > TiltShotAngle && Time.time - tiltStart < TiltTimeout)
                yield return null;
            Debug.Log($"[demo] tilt angle={root.Sim.Drill.Angle:0.0} x={root.Sim.Drill.X:0.00} vx={root.Sim.Drill.VelocityX:0.00}");
            yield return Shot("tilt");

            yield return new WaitForSeconds(2.5f);
            Debug.Log($"[demo] after hole x={root.Sim.Drill.X:0.00} depth={root.Sim.State.depth}");
            yield return Shot("after_hole");

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

        // Opens the floor under the left foot and the rows below it.
        private void CarveLeftPocket()
        {
            var sim = root.Sim;
            int feet = sim.Drill.FeetRow;
            int col = Mathf.RoundToInt((float)sim.Drill.X);
            for (int row = feet; row < feet + 4; row++)
                for (int c = col - 5; c < col; c++)
                    if (c >= 0) sim.Carve(row, c);
        }

        // Writes the real save file, reads it back and prices an hour offline on the loaded copy.
        private void VerifySaveRoundTrip()
        {
            var live = root.Sim.State;
            root.SaveNow();
            var loaded = root.Saves.Load();
            bool same = loaded.depth == live.depth && Close(loaded.coins, live.coins) && loaded.seed == live.seed
                && Close(loaded.drillX, live.drillX) && Close(loaded.drillY, live.drillY)
                && loaded.skills.Count == live.skills.Count && loaded.version == GameState.CurrentVersion;
            Debug.Log($"[demo] coins {live.coins:R}/{loaded.coins:R} x {live.drillX:R}/{loaded.drillX:R} " +
                $"y {live.drillY:R}/{loaded.drillY:R} skills {live.skills.Count}/{loaded.skills.Count}");
            double before = loaded.coins;
            double offline = new GameSim(loaded).ApplyOffline(loaded.lastSaveUnixSeconds + 3600);
            Debug.Log($"[demo] save roundtrip same={same} depth={loaded.depth} incomePerSec={loaded.incomePerSecond:0.00} " +
                $"offline1h={offline:0.0} coins {before:0.0}->{loaded.coins:0.0}");
        }

        // JsonUtility may round a double's last digit; anything larger is a real loss
        private static bool Close(double a, double b) => System.Math.Abs(a - b) <= System.Math.Abs(a) * 1e-12;

        private void Play()
        {
            var sim = root.Sim;
            foreach (var skill in SkillCatalog.All) sim.Buy(skill.Id);
            // tap away from the drill's path so the normal shot shows it digging upright
            int row = sim.State.depth + Random.Range(4, 8);
            int col = Mathf.RoundToInt((float)sim.Drill.X) + Random.Range(3, 7) * (Random.value < 0.5f ? -1 : 1);
            root.ClickCell(row, col);
        }
    }
}
