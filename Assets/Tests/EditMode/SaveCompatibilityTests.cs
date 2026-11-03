#if UNITY_5_3_OR_NEWER
using System.IO;
using DeepDrill.Core;
using NUnit.Framework;
using UnityEngine;

namespace DeepDrill.Tests
{
    /// <summary>Real saves kept from earlier versions: every one must still load and play.</summary>
    public class SaveCompatibilityTests
    {
        private const string Folder = "Assets/Tests/EditMode/Saves";

        private static GameState Load(string file)
        {
            var state = JsonUtility.FromJson<GameState>(File.ReadAllText(Path.Combine(Folder, file)));
            state.Upgrade();
            return state;
        }

        [TestCase("save_v6_boss_stall.json")]
        [TestCase("save_v6_early.json")]
        public void RealSave_LoadsAndPlays(string file)
        {
            CoreTests.EnsureData();
            var original = JsonUtility.FromJson<GameState>(File.ReadAllText(Path.Combine(Folder, file)));
            int drills = original.drills.Count;
            double coins = original.coins;

            var state = Load(file);
            Assert.AreEqual(GameState.CurrentVersion, state.version);
            Assert.AreEqual(drills, state.drills.Count);
            Assert.AreEqual(coins, state.coins, 1e-6);
            Assert.AreEqual(-1, state.secondSchool);

            var sim = new GameSim(state);
            for (int i = 0; i < 600; i++) sim.Tick(1.0 / 60);
            sim.PackHoles();

            var again = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(sim.State));
            again.Upgrade();
            Assert.AreEqual(sim.State.seed, again.seed);
            Assert.AreEqual(sim.State.drills.Count, again.drills.Count);
        }

        [Test]
        public void OldSave_ReadsLeaderboardRecordsAsZero()
        {
            var state = Load("save_v6_early.json");
            Assert.AreEqual(0, state.standardBestRow);
            Assert.AreEqual(0, state.playSecondsEver, 1e-9);
            Assert.AreEqual(0, state.longestRunSeconds, 1e-9);
            Assert.AreEqual(0, state.mostDrills);
        }

        [Test]
        public void OldSave_DoesNotReplayCardsItHasOutgrown()
        {
            var state = Load("save_v6_boss_stall.json");
            Assert.IsTrue(state.seenGuides.Contains("tap"));
            Assert.AreEqual(state.bossesEver > 0, state.seenGuides.Contains("boss"));
            Assert.AreEqual(state.mergesEver > 0, state.seenGuides.Contains("merge"));
        }
    }
}
#endif
