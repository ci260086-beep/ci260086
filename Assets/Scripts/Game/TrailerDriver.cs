using System;
using System.Collections;
using System.IO;
using DeepDrill.Core;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// Started only by the --trailer flag (menu DeepDrill > Trailer: Record). Plays scripted shots
    /// for the trailer and writes every frame of each one to its own folder at a fixed frame rate,
    /// so the game runs in lockstep with the recording however slow the capture is. The edit, the
    /// title cards and the music are put together afterwards by tools/trailer.
    /// </summary>
    public sealed class TrailerDriver : MonoBehaviour
    {
        public const int Fps = 30;
        private const float Tail = 0.5f;    // each clip runs a little long so the edit can trim on the beat
        private const int LayersShown = 9;  // layers 2 to 10; the guardian of the tenth comes next
        private const int BossLayer = 9;    // deeper than every layer shown, so its body is still there

        private GameRoot root;
        private string folder;

        public void Init(GameRoot gameRoot, string recordFolder)
        {
            root = gameRoot;
            folder = recordFolder;
            root.Hud.GuidesOff = true;
            root.Hud.Filming = true;
            Directory.CreateDirectory(folder);
            StartCoroutine(Run());
        }

        // Records one clip: perFrame runs before each frame with the clip's time in seconds.
        private IEnumerator Record(string clip, float seconds, Action<float> perFrame = null)
        {
            string dir = Path.Combine(folder, clip);
            if (Directory.Exists(dir)) foreach (string old in Directory.GetFiles(dir, "*.png")) File.Delete(old);
            Directory.CreateDirectory(dir);
            int frames = Mathf.CeilToInt((seconds + Tail) * Fps);
            Time.captureFramerate = Fps;
            for (int i = 0; i < frames; i++)
            {
                perFrame?.Invoke(i / (float)Fps);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"f_{i:00000}.png"));
                yield return null;
            }
            yield return null; // the last capture lands at the end of this frame
            Time.captureFramerate = 0;
            Debug.Log($"[trailer] {clip} {frames} frames");
        }

        private IEnumerator Run()
        {
            var sim = root.Sim;
            sim.State.autoDrills = sim.State.autoSkills = false; // nothing spends the scripted coins
            yield return new WaitForSeconds(1f);

            // 1. a fresh game: the player taps the drill and it bites into the topsoil
            float nextTap = 0;
            yield return Record("intro", 3.75f, t =>
            {
                if (t < nextTap) return;
                nextTap = t + 0.12f;
                root.ClickCell(sim.Lead.Body.BodyRow + 1, sim.Lead.Body.Column);
            });

            // 2. a handful of drills dig on their own
            sim.State.coins = 1e6;
            sim.Buy(GameSim.RootSkill);
            for (int i = 0; i < 6; i++) sim.BuyDrill(0);
            sim.State.coins = 120; // an early-game purse, not the million it took to set this up
            yield return new WaitForSeconds(2.5f);
            yield return Record("dig", 3.75f);

            // 3. the chain builds: the lead drill's flame goes from ember to blue
            for (int i = 0; i < 10; i++) sim.BuyDrill(1);
            yield return new WaitForSeconds(1f);
            yield return Record("combo", 5.6f, t => sim.PrimeCombo(2 + (int)(t / 5f * 120)));

            // 4. a hangar full of drills merges up the ladder
            sim.State.coins = 1e12;
            sim.BuyDrills(0, 96);
            yield return new WaitForSeconds(1f);
            bool merged = false, refilled = false, mergedAgain = false;
            yield return Record("merge", 3.75f, t =>
            {
                if (!merged && t >= 0.6f) { merged = true; sim.MergeEverything(); }
                if (!refilled && t >= 1.9f) { refilled = true; for (int i = 0; i < 64; i++) sim.State.drills.Add(DrillCode.Make(2, 3)); sim.SyncDrills(); }
                if (!mergedAgain && t >= 2.5f) { mergedAgain = true; sim.MergeEverything(); }
                sim.PrimeCombo(150);
            });

            // 5. down through the layers, one beat each, in a frenzy
            sim.FillFrenzy();
            sim.StartFrenzy();
            int shown = 0;
            float beat = 7.5f / LayersShown;
            yield return Record("layers", 7.5f, t =>
            {
                int s = Mathf.Min(LayersShown, 1 + (int)(t / beat));
                if (s == shown) return;
                shown = s;
                var layer = GameData.Layers[s];
                sim.State.bossesDefeated = s;
                int mid = (layer.RowStart + layer.RowEnd) / 2;
                sim.Teleport(mid, Balance.StartColumn);
                root.SnapCamera();
                if (s == 1) sim.World.CarveRect(16 * 16, (mid + 2) * 16, 24 * 16, (mid + 6) * 16); // sand pours
                if (s == 7 || s == 9) sim.Detonate(mid + 3, Balance.StartColumn + 3); // magma pockets go up
                if (!sim.InFrenzy) { sim.FillFrenzy(); sim.StartFrenzy(); }
                sim.PrimeCombo(150);
            });

            // 6. a guardian: it arrives, winds up, and the swarm breaks through it. It has to be
            // deeper than anything shown before, as guardians above the drill are already gone.
            sim.State.bossesDefeated = BossLayer;
            while (sim.State.drills.Count < 1000) sim.State.drills.Add(DrillCode.Make(sim.State.drills.Count % GameData.Families.Count, 6 + sim.State.drills.Count % 6));
            sim.SyncDrills();
            sim.Teleport(GameData.Layers[BossLayer].RowEnd, Balance.StartColumn); // feet on the guardian's back
            root.SnapCamera();
            float bossWait = Time.time + 4f;
            while (!sim.BossActive && Time.time < bossWait) yield return null;
            Debug.Log($"[trailer] boss active={sim.BossActive}");
            bool hurried = false, beaten = false;
            yield return Record("boss", 5.6f, t =>
            {
                if (!hurried && t >= 1.2f) { hurried = true; sim.HurryBossMove(); }
                if (!beaten && t >= 3.4f && sim.BossActive) { beaten = true; sim.DefeatBossNow(); }
            });

            // 7. past the guardian with a thousand drills, and the coin count runs away
            int deepLayer = BossLayer + 1;
            sim.State.bossesDefeated = deepLayer;
            var deep = GameData.Layers[deepLayer];
            sim.Teleport(deep.RowStart + 12, Balance.StartColumn);
            root.SnapCamera();
            sim.FillFrenzy();
            sim.StartFrenzy();
            yield return new WaitForSeconds(2f);
            yield return Record("coins", 5.6f, t =>
            {
                sim.State.coins = Math.Pow(10, 3 + 27 * Mathf.Clamp01(t / 5f));
                sim.PrimeCombo(150);
                if (!sim.InFrenzy) { sim.FillFrenzy(); sim.StartFrenzy(); }
            });

            // 8. a warp to the storm planet, and its first lightning strike
            sim.State.warps = GameData.Planets.FindIndex(p => p.Rule == PlanetRule.storm);
            sim.Teleport(GameData.Layers[2].RowStart + 10, Balance.StartColumn);
            root.SnapCamera();
            bool struck = false;
            yield return Record("warp", 3.75f, t =>
            {
                if (!struck && t >= 1.0f) { struck = true; sim.HurryStorm(0.6); } // lightning lands near 1.6 s, after the card
            });

            Debug.Log("[trailer] done");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#endif
        }
    }
}
