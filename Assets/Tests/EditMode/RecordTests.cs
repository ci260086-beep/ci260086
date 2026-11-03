using DeepDrill.Core;
using NUnit.Framework;

namespace DeepDrill.Tests
{
    /// <summary>The records the leaderboard shows: kept through runs, and sampled once a second.</summary>
    public class RecordTests
    {
        [Test]
        public void PlayTime_CountsEveryRunAndKeepsTheLongest()
        {
            var sim = CoreTests.NewSim();
            for (int i = 0; i < 30; i++) sim.Tick(0.1);
            Assert.AreEqual(3, sim.State.playSecondsEver, 1e-6);
            Assert.AreEqual(3, sim.State.longestRunSeconds, 1e-6);
            sim.State.runSeconds = 0; // a fresh run
            sim.Tick(1);
            Assert.AreEqual(4, sim.State.playSecondsEver, 1e-6);
            Assert.AreEqual(3, sim.State.longestRunSeconds, 1e-6);
        }

        [Test]
        public void StandardDepth_CountsOnlyRunsWithStandardDrills()
        {
            var sim = CoreTests.NewSim();
            sim.State.maxDepth = 40;
            sim.UpdateRecords();
            Assert.IsTrue(sim.StandardOnlyRun);
            Assert.AreEqual(40, sim.State.standardBestRow);

            sim.State.drills.Add(DrillCode.Make(2, 0)); // a heavy drill joins
            sim.State.maxDepth = 90;
            sim.UpdateRecords();
            Assert.IsFalse(sim.StandardOnlyRun);
            Assert.AreEqual(40, sim.State.standardBestRow);
        }

        [Test]
        public void MostDrills_IsTheHighestCountEverOwned()
        {
            var sim = CoreTests.NewSim();
            int start = sim.State.drills.Count;
            for (int i = 0; i < 5; i++) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.UpdateRecords();
            sim.State.drills.RemoveRange(0, 3);
            sim.UpdateRecords();
            Assert.AreEqual(start + 5, sim.State.mostDrills);
        }
    }
}
