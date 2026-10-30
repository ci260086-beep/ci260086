using System.Linq;
using DeepDrill.Core;
using NUnit.Framework;

namespace DeepDrill.Tests
{
    /// <summary>The systems of the third phase, one region per step of the plan.</summary>
    public class Phase3Tests
    {
        private static void Run(GameSim sim, double seconds)
        {
            for (double t = 0; t < seconds; t += 0.5) sim.Tick(0.5);
        }

        #region step 1: surface events

        [Test]
        public void Event_AppearsByItselfAndFades()
        {
            var sim = CoreTests.NewSim();
            bool seen = false;
            for (double t = 0; t < GameData.Events.EveryMax + 5 && !seen; t += 0.5)
            {
                sim.Tick(0.5);
                seen = sim.ActiveEvent != SurfaceEvent.None;
                sim.Events.Clear();
            }
            Assert.IsTrue(seen);
            Run(sim, GameData.Events.Lifetime + 1);
            Assert.AreEqual(SurfaceEvent.None, sim.ActiveEvent);
        }

        [Test]
        public void Event_WaitsForAutoDrilling()
        {
            var sim = CoreTests.FreshSim();
            Run(sim, GameData.Events.EveryMax + 5);
            Assert.AreEqual(SurfaceEvent.None, sim.ActiveEvent);
        }

        [Test]
        public void Event_ClaimingPaysOutOnce()
        {
            var sim = CoreTests.NewSim();
            sim.ForceEvent(SurfaceEvent.Golden);
            Assert.IsNotNull(sim.ClaimEvent());
            Assert.IsNull(sim.ClaimEvent());
            Assert.AreEqual(1, sim.State.eventsClaimed);
        }

        [Test]
        public void Event_DamageBuffMultipliesThenEnds()
        {
            var sim = CoreTests.NewSim();
            int code = DrillCode.Make(0, 0);
            double before = sim.DamageOf(code);
            sim.Apply(new EventOutcome { Kind = EventKind.damage, Amount = 3, Seconds = 5 });
            Assert.AreEqual(before * 3, sim.DamageOf(code), 1e-9);
            sim.Tick(5.1);
            Assert.AreEqual(before, sim.DamageOf(code), before);
            Assert.AreEqual(1, sim.DamageBuffMult, 1e-9);
        }

        [Test]
        public void Event_UnstableCanSlowTheDrills()
        {
            var sim = CoreTests.NewSim();
            int code = DrillCode.Make(0, 0);
            double before = sim.HitsPerSecond(code);
            sim.Apply(new EventOutcome { Kind = EventKind.speed, Amount = 0.5, Seconds = 5 });
            Assert.AreEqual(before * 0.5, sim.HitsPerSecond(code), 1e-9);
        }

        [Test]
        public void Event_LossTakesAShareOfCoins()
        {
            var sim = CoreTests.NewSim();
            sim.State.coins = 1000;
            sim.Apply(new EventOutcome { Kind = EventKind.lose, Amount = 0.1 });
            Assert.AreEqual(900, sim.State.coins, 1e-9);
        }

        [Test]
        public void Event_CoinsScaleWithIncomeAndHaveAFloor()
        {
            var sim = CoreTests.NewSim();
            sim.Apply(new EventOutcome { Kind = EventKind.coins, Amount = 10 });
            Assert.AreEqual(Balance.EventCoinFloor * 10, sim.State.coins, 1e-9);
        }

        #endregion
    }
}
