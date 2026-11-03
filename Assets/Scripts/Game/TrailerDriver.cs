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
        private const int LayersShown = 8;  // layers 2 to 9, a quarter bar each
        private const int StarTab = 6;      // the base page's star map
        private const int DeepStormLayer = 8;
        private const int BossLayer = 9;    // deeper than every layer shown, so its body is still there

        private GameRoot root;
        private string folder;

        public void Init(GameRoot gameRoot, string recordFolder)
        {
            root = gameRoot;
            folder = recordFolder;
            root.Hud.GuidesOff = true;
            root.Hud.Filming = true;
            root.TrailerHype = true;
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

        private const float Bar = 240f / 140f; // the trailer is cut at 140 beats a minute

        private IEnumerator Run()
        {
            var sim = root.Sim;
            sim.State.autoDrills = sim.State.autoSkills = false; // nothing spends the scripted coins
            yield return new WaitForSeconds(1f);

            // cold open (after the edit's "ready?" card): a fresh game, the player taps the drill in
            float nextTap = 0;
            yield return Record("intro", Bar, t =>
            {
                if (t < nextTap) return;
                nextTap = t + 0.1f;
                root.ClickCell(sim.Lead.Body.BodyRow + 1, sim.Lead.Body.Column);
            });

            // the drop: a swarm digging in a frenzy
            sim.State.coins = 1e9;
            sim.Buy(GameSim.RootSkill);
            sim.BuyDrills(0, 40);
            sim.BuyDrills(1, 12);
            sim.State.coins = 250;
            sim.FillFrenzy();
            sim.StartFrenzy();
            yield return new WaitForSeconds(2f);
            yield return Record("dig", Bar, t => sim.PrimeCombo(30));

            // the chain heats the lead drill's flame from ember to blue in one bar
            yield return Record("combo", Bar, t => sim.PrimeCombo(2 + (int)(t / Bar * 130)));

            // a hangar full of drills merges up the ladder
            sim.State.coins = 1e12;
            sim.BuyDrills(0, 120);
            yield return new WaitForSeconds(0.6f);
            bool merged = false;
            yield return Record("merge", Bar, t =>
            {
                if (!merged && t >= 0.25f) { merged = true; sim.MergeEverything(); }
                sim.PrimeCombo(150);
            });

            // charges going off one after another
            int blasts = 0;
            yield return Record("blast", Bar, t =>
            {
                int due = (int)(t / (Bar / 6));
                while (blasts <= due && blasts < 6)
                {
                    sim.Detonate(sim.Lead.Body.FeetRow + 2 + blasts % 3, Balance.StartColumn - 6 + blasts * 3);
                    blasts++;
                }
            });

            // the skill tree, zoomed out, lighting up node after node
            sim.State.coins = 1e30;
            sim.State.maxDepthEver = Math.Max(sim.State.maxDepthEver, GameData.TotalRows);
            root.Hud.ShowTreePage();
            root.Hud.CenterTree();
            root.Hud.ZoomTree(-10);
            yield return new WaitForSeconds(0.6f);
            int nextSkill = 0;
            yield return Record("tree", 2 * Bar, t =>
            {
                int due = (int)(t / (2 * Bar) * GameData.Skills.Count);
                while (nextSkill <= due && nextSkill < GameData.Skills.Count)
                {
                    string id = GameData.Skills[nextSkill++].Id;
                    if (sim.Buy(id)) root.Hud.BurstSkill(id);
                }
            });

            // the base's systems, one beat each
            sim.State.rebirths = Math.Max(1, sim.State.rebirths);
            sim.State.schoolsCleared.Add(GameData.Schools.Count - 1);
            sim.ChooseSchool(0);
            int[] tabs = { 1, 2, 3, 4, 5 };
            int shownTab = -1;
            root.Hud.ShowBasePage(tabs[0]);
            yield return new WaitForSeconds(0.4f);
            yield return Record("systems", 2 * Bar, t =>
            {
                int i = Mathf.Min(tabs.Length - 1, (int)(t / (2 * Bar / tabs.Length)));
                if (i == shownTab) return;
                shownTab = i;
                root.Hud.ShowBasePage(tabs[i]);
            });

            // the collection filling up
            var achievements = GameData.Achievements;
            sim.State.achievements.Clear();
            root.Hud.ShowGalleryPage();
            yield return new WaitForSeconds(0.3f);
            int unlocked = 0;
            yield return Record("achievements", 2 * Bar, t =>
            {
                int due = (int)(t / (1.6f * Bar) * achievements.Count);
                while (unlocked <= due && unlocked < achievements.Count)
                {
                    string id = achievements[unlocked++].Id;
                    if (!sim.State.achievements.Contains(id)) sim.State.achievements.Add(id);
                    var rare = GameData.Rares[unlocked * 7 % GameData.Rares.Count]; // the rare items fill in alongside
                    sim.State.AddRare(rare.Id);
                }
            });
            root.Hud.ShowHangarPage();

            // down through the layers, a quarter bar each
            sim.FillFrenzy();
            sim.StartFrenzy();
            int shown = 0;
            float step = 2 * Bar / LayersShown;
            yield return Record("layers", 2 * Bar, t =>
            {
                int layerIndex = Mathf.Min(LayersShown, 1 + (int)(t / step));
                if (layerIndex == shown) return;
                shown = layerIndex;
                var layer = GameData.Layers[layerIndex];
                sim.State.bossesDefeated = layerIndex;
                int mid = (layer.RowStart + layer.RowEnd) / 2;
                sim.Teleport(mid, Balance.StartColumn);
                root.SnapCamera();
                if (layerIndex == 1) sim.World.CarveRect(16 * 16, (mid + 2) * 16, 24 * 16, (mid + 6) * 16); // sand pours
                if (layerIndex >= 6) sim.Detonate(mid + 3, Balance.StartColumn + 3);
                if (!sim.InFrenzy) { sim.FillFrenzy(); sim.StartFrenzy(); }
                sim.PrimeCombo(150);
            });

            // a guardian, landed on by a thousand drills and broken through
            sim.State.bossesDefeated = BossLayer;
            while (sim.State.drills.Count < 1000) sim.State.drills.Add(DrillCode.Make(sim.State.drills.Count % GameData.Families.Count, 6 + sim.State.drills.Count % 6));
            sim.SyncDrills();
            sim.Teleport(GameData.Layers[BossLayer].RowEnd, Balance.StartColumn); // feet on the guardian's back
            root.SnapCamera();
            float bossWait = Time.time + 4f;
            while (!sim.BossActive && Time.time < bossWait) yield return null;
            Debug.Log($"[trailer] boss active={sim.BossActive}");
            bool hurried = false, beaten = false;
            yield return Record("boss", 2 * Bar, t =>
            {
                if (!hurried && t >= 0.5f) { hurried = true; sim.HurryBossMove(); }
                if (!beaten && t >= 1.8f && sim.BossActive) { beaten = true; sim.DefeatBossNow(); }
            });

            // the star map, then a real warp to the storm planet and its first strike
            root.Hud.ShowBasePage(StarTab);
            yield return new WaitForSeconds(0.3f);
            bool warped = false, settled = false, struck = false;
            yield return Record("planets", 2 * Bar, t =>
            {
                if (!warped && t >= 1.1f)
                {
                    warped = true;
                    sim.State.warps = GameData.Planets.FindIndex(p => p.Rule == PlanetRule.storm) - 1;
                    sim.State.maxDepthEver = GameData.TotalRows;
                    sim.State.challenge = -1;
                    sim.Warp();
                    root.Hud.ShowHangarPage();
                }
                if (warped && !settled && t >= 1.2f)
                {
                    settled = true;
                    root.Hud.CloseCard();
                    sim.State.coins = 1e9;
                    sim.Buy(GameSim.RootSkill);
                    sim.BuyDrills(0, 30);
                    sim.State.coins = 0;
                }
                if (settled && !struck && t >= 1.5f) { struck = true; sim.HurryStorm(0.5); }
            });
            Debug.Log($"[trailer] planet={sim.Planet.Name}");

            // the climax on the storm planet: a thousand drills, lightning and a runaway count
            sim.State.bossesDefeated = DeepStormLayer;
            var deep = GameData.Layers[DeepStormLayer];
            sim.Teleport((deep.RowStart + deep.RowEnd) / 2, Balance.StartColumn);
            while (sim.State.drills.Count < 1000) sim.State.drills.Add(DrillCode.Make(sim.State.drills.Count % GameData.Families.Count, 6 + sim.State.drills.Count % 6));
            sim.SyncDrills();
            root.SnapCamera();
            sim.FillFrenzy();
            sim.StartFrenzy();
            yield return new WaitForSeconds(2f);
            sim.State.coins = 1000;
            yield return null;
            float nextStrike = 0.3f;
            yield return Record("coins", 2.5f * Bar, t =>
            {
                sim.State.coins = Math.Pow(10, 3 + 27 * Mathf.Clamp01(t / (2.2f * Bar)));
                sim.PrimeCombo(150);
                if (!sim.InFrenzy) { sim.FillFrenzy(); sim.StartFrenzy(); }
                if (t >= nextStrike) { nextStrike += Bar / 2; sim.HurryStorm(0); }
            });

            Debug.Log("[trailer] done");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#endif
        }
    }
}
