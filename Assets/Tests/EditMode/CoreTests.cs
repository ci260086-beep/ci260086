using DeepDrill.Core;
using NUnit.Framework;

namespace DeepDrill.Tests
{
    public class CoreTests
    {
        private static GameSim NewSim(int seed = 42) => new GameSim(new GameState { seed = seed });

        [Test]
        public void Formulas_MatchTheBrief()
        {
            Assert.AreEqual(10, Balance.BlockHp(0), 1e-9);
            Assert.AreEqual(10 * System.Math.Pow(1.035, 50), Balance.BlockHp(50), 1e-9);
            Assert.AreEqual(2 * System.Math.Pow(1.03, 10), Balance.OreValue(10, 2), 1e-9);
            Assert.AreEqual(8 * 1.1 * 1.1, Balance.Cost(8, 1.1, 2), 1e-9);
        }

        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(0.5, "0.5")]
        [TestCase(999.9, "999.9")]
        [TestCase(1000, "1.00K")]
        [TestCase(1234567, "1.23M")]
        [TestCase(999999, "1.00M")]
        [TestCase(2.5e9, "2.50B")]
        [TestCase(7e12, "7.00T")]
        [TestCase(1e15, "1.00e15")]
        [TestCase(3.456e20, "3.46e20")]
        public void Numbers_AreShortened(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormat.Short(value));
        }

        [Test]
        public void WorldGen_IsDeterministic()
        {
            for (int row = 0; row < 200; row++)
                for (int col = 0; col < Balance.Columns; col++)
                    Assert.AreEqual(WorldGen.TypeAt(7, row, col), WorldGen.TypeAt(7, row, col));
        }

        [Test]
        public void WorldGen_ProducesOreAndRock()
        {
            int ore = 0, rock = 0;
            for (int row = 3; row < 300; row++)
                for (int col = 0; col < Balance.Columns; col++)
                {
                    var t = WorldGen.TypeAt(3, row, col);
                    if (WorldGen.IsOre(t)) ore++;
                    if (t == BlockType.HardRock) rock++;
                }
            Assert.Greater(ore, 200);
            Assert.Greater(rock, 200);
        }

        [Test]
        public void WorldGen_HasNoLeftRightSymmetry()
        {
            int matches = 0, total = 0;
            for (int row = 3; row < 400; row++)
                for (int col = 0; col < Balance.ShaftCenter; col++)
                {
                    total++;
                    var a = WorldGen.TypeAt(5, row, col);
                    var b = WorldGen.TypeAt(5, row, Balance.Columns - 1 - col);
                    if (a != BlockType.Dirt && a == b) matches++;
                }
            Assert.Less(matches, total * 0.03);
        }

        [Test]
        public void HardRock_HasTripleHp()
        {
            Assert.AreEqual(Balance.BlockHp(9) * 3, WorldGen.HpOf(BlockType.HardRock, 9), 1e-9);
        }

        [Test]
        public void Drill_DigsDownAndEarns()
        {
            var sim = NewSim();
            for (int i = 0; i < 600; i++) sim.Tick(0.1);
            Assert.Greater(sim.State.depth, 0);
            Assert.Greater(sim.State.coins, 0);
            Assert.AreEqual(sim.State.depth, sim.State.maxDepth);
        }

        [Test]
        public void DepthAdvances_OnlyWhenTheWholeShaftRowIsGone()
        {
            var sim = NewSim();
            var row = sim.Row(0);
            row[Balance.ShaftCenter].Hp = 0.1;
            row[Balance.ShaftCenter - 1].Hp = 0.1;
            double oneHit = 1.001 / Balance.BaseHitsPerSecond;
            sim.Tick(oneHit);
            sim.Tick(oneHit);
            Assert.AreEqual(0, sim.State.depth);
            row[Balance.ShaftCenter + 1].Hp = 0.1;
            sim.Tick(oneHit);
            Assert.AreEqual(1, sim.State.depth);
        }

        [Test]
        public void Click_DamagesAnyBlock_AndIgnoresBrokenOnes()
        {
            var sim = NewSim();
            var block = sim.Row(5)[0];
            double before = block.Hp;
            Assert.IsTrue(sim.Click(5, 0));
            Assert.AreEqual(before - sim.DamagePerHit, block.Hp, 1e-9);
            block.Hp = 0;
            Assert.IsFalse(sim.Click(5, 0));
        }

        [Test]
        public void Buying_ChargesAndRespectsRequirements()
        {
            var sim = NewSim();
            Assert.IsFalse(sim.Buy(SkillCatalog.DrillDamage), "no money yet");

            sim.State.coins = 1000;
            Assert.IsTrue(sim.Buy(SkillCatalog.DrillDamage));
            Assert.AreEqual(Balance.BaseDamage + 1, sim.DamagePerHit, 1e-9);
            Assert.AreEqual(1000 - 8, sim.State.coins, 1e-9);

            Assert.IsFalse(sim.Buy(SkillCatalog.DrillSpeed), "locked until damage is level 3");
            sim.Buy(SkillCatalog.DrillDamage);
            sim.Buy(SkillCatalog.DrillDamage);
            Assert.IsTrue(sim.Buy(SkillCatalog.DrillSpeed));
            Assert.AreEqual(Balance.BaseHitsPerSecond * 1.05, sim.HitsPerSecond, 1e-9);
        }

        [Test]
        public void Offline_PaysHalfAndCapsAtTwoHours()
        {
            Assert.AreEqual(50, Balance.OfflineEarnings(10, 10), 1e-9);
            Assert.AreEqual(10 * 7200 * 0.5, Balance.OfflineEarnings(10, 999999), 1e-9);
            Assert.AreEqual(0, Balance.OfflineEarnings(10, -5), 1e-9);

            var state = new GameState { incomePerSecond = 4, lastSaveUnixSeconds = 1000 };
            var sim = new GameSim(state);
            Assert.AreEqual(200, sim.ApplyOffline(1100), 1e-9);
            Assert.AreEqual(200, state.coins, 1e-9);
        }

        [Test]
        public void ReloadedRun_RebuildsTheSameGroundWithTheShaftDug()
        {
            var sim = NewSim();
            while (sim.State.depth < 5) sim.Tick(0.1);
            var reloaded = new GameSim(sim.State);
            Assert.IsFalse(reloaded.Row(2)[Balance.ShaftCenter].Alive);
            Assert.IsTrue(reloaded.Row(2)[0].Alive);
            Assert.AreEqual(sim.Row(40)[3].Type, reloaded.Row(40)[3].Type);
        }
    }
}
