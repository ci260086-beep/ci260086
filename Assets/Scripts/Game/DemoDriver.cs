using System.Collections;
using System.IO;
using DeepDrill.Core;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// Started only by the --shots command line flag. Plays the game unattended, buying upgrades
    /// and tapping blocks, then writes screenshots and quits so a build can be checked by script.
    /// </summary>
    public sealed class DemoDriver : MonoBehaviour
    {
        private static readonly float[] ShotTimes = { 1.5f, 6f, 14f, 25f };
        private const float SecondsPerTap = 0.25f;

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
            int shot = 0;
            float nextTap = 0;
            while (shot < ShotTimes.Length)
            {
                float t = Time.time - start;
                if (t >= nextTap)
                {
                    nextTap = t + SecondsPerTap;
                    Play();
                }
                if (t >= ShotTimes[shot])
                {
                    shot++;
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"shot_{shot:00}.png"));
                    Debug.Log($"[demo] shot {shot} depth={root.Sim.State.depth} coins={root.Sim.State.coins:0.0}");
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            VerifySaveRoundTrip();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }

        // Writes the real save file, reads it back and prices an hour offline on the loaded copy.
        private void VerifySaveRoundTrip()
        {
            var live = root.Sim.State;
            root.SaveNow();
            var loaded = root.Saves.Load();
            bool same = loaded.depth == live.depth && loaded.coins == live.coins && loaded.seed == live.seed
                && loaded.skills.Count == live.skills.Count && loaded.version == GameState.CurrentVersion;
            double before = loaded.coins;
            double offline = new GameSim(loaded).ApplyOffline(loaded.lastSaveUnixSeconds + 3600);
            Debug.Log($"[demo] save roundtrip same={same} depth={loaded.depth} incomePerSec={loaded.incomePerSecond:0.00} " +
                $"offline1h={offline:0.0} coins {before:0.0}->{loaded.coins:0.0}");
        }

        private void Play()
        {
            var sim = root.Sim;
            foreach (var skill in SkillCatalog.All) sim.Buy(skill.Id);
            int row = sim.State.depth + Random.Range(0, 5);
            root.ClickCell(row, Random.Range(1, Balance.Columns - 1));
        }
    }
}
