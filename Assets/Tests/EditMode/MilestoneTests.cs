using System.Linq;
using DeepDrill.Core;
using NUnit.Framework;

namespace DeepDrill.Tests
{
    /// <summary>Drill milestones and layer mastery: both last through a rebirth.</summary>
    public class MilestoneTests
    {
        private const int Std = 0;
        private const int Swift = 1;
        private const int Heavy = 2;
        private const int Magnet = 4;

        private static void Own(GameSim sim, int family, int count)
        {
            for (int i = 0; i < count; i++) sim.State.AddBought(family);
            sim.Tick(1.1);
        }

        [Test]
        public void Data_HasARewardForEveryCount()
        {
            CoreTests.EnsureData();
            CollectionAssert.AreEqual(new[] { 10, 25, 50, 100, 200 }, GameData.MilestoneCounts);
            Assert.AreEqual(GameData.Families.Count, GameData.Milestones.Count);
            foreach (var rewards in GameData.Milestones) Assert.AreEqual(5, rewards.Count);
            Assert.IsTrue(GameData.Milestones.Skip(1).All(r => r[4].Kind == MilestoneKind.trait), "every family but the plain one doubles its trait");
            Assert.AreEqual(10, GameData.MasteryBlocks.Count);
        }

        [Test]
        public void Milestone_ComesOnceTheCountIsReached()
        {
            var sim = CoreTests.NewSim();
            Own(sim, Std, 9);
            Assert.AreEqual(0, sim.MilestonesOf(Std));
            Own(sim, Std, 1);
            Assert.AreEqual(1, sim.MilestonesOf(Std));
            Assert.IsTrue(sim.Events.Exists(e => e.Kind == SimEventKind.MilestoneReached && (int)e.Amount == Std && e.Row == 0));
        }

        [Test]
        public void Milestone_RaisesThatFamilysDamageOnly()
        {
            var sim = CoreTests.NewSim();
            int std = DrillCode.Make(Std, 0), heavy = DrillCode.Make(Heavy, 0);
            double stdBefore = sim.DamageOf(std), heavyBefore = sim.DamageOf(heavy);
            Own(sim, Std, 10);
            Assert.AreEqual(stdBefore * 1.15, sim.DamageOf(std), 1e-9);
            Assert.AreEqual(heavyBefore, sim.DamageOf(heavy), 1e-9);
        }

        [Test]
        public void Milestone_RaisesThatFamilysSpeed()
        {
            var sim = CoreTests.NewSim();
            int code = DrillCode.Make(Std, 0);
            double before = sim.HitsPerSecond(code);
            Own(sim, Std, 25);
            Assert.AreEqual(before * 1.1, sim.HitsPerSecond(code), 1e-9);
        }

        [Test]
        public void Milestone_SurvivesRebirth()
        {
            var sim = CoreTests.NewSim();
            Own(sim, Std, 30);
            sim.State.bossesDefeated = 1;
            sim.State.maxDepth = 400;
            sim.State.totalIncome = 1e6;
            Assert.IsTrue(sim.Rebirth());

            Assert.AreEqual(0, sim.State.BoughtOf(Std));
            Assert.AreEqual(2, sim.MilestonesOf(Std));
            Assert.AreEqual(sim.DamageOf(DrillCode.Make(Std, 0)), GameSim.KindDamage(DrillCode.Make(Std, 0)) * 1.15 * sim.DamageOf(DrillCode.Make(Heavy, 0)) / GameSim.KindDamage(DrillCode.Make(Heavy, 0)), 1e-9);
        }

        [Test]
        public void Milestone_TraitDoublesSwiftCombo()
        {
            var sim = CoreTests.NewSim();
            Own(sim, Swift, 200);
            sim.State.drills.Clear();
            sim.State.drills.Add(DrillCode.Make(Swift, 0));
            sim.SyncDrills();
            sim.Teleport(40, 20);
            for (int i = 0; i < 40; i++) sim.Tick(0.05);

            var plain = CoreTests.NewSim();
            plain.State.drills.Clear();
            plain.State.drills.Add(DrillCode.Make(Swift, 0));
            plain.SyncDrills();
            plain.Teleport(40, 20);
            for (int i = 0; i < 40; i++) plain.Tick(0.05);

            Assert.Greater(sim.Combo, plain.Combo);
        }

        [Test]
        public void Milestone_TraitDoublesMagnetOre()
        {
            var sim = CoreTests.NewSim();
            Own(sim, Magnet, 200);
            Assert.AreEqual(5, sim.MilestonesOf(Magnet));
            Assert.AreEqual(MilestoneKind.trait, GameData.Milestones[Magnet][4].Kind);
        }

        [Test]
        public void UpgradedSave_KeepsWhatWasAlreadyBought()
        {
            var state = new GameState { version = 5 };
            state.bought.Add(30);
            state.familyBest.Clear();
            state.Upgrade();
            Assert.AreEqual(30, state.FamilyBestOf(0));
            Assert.AreEqual(GameState.CurrentVersion, state.version);
        }

        [Test]
        public void Mastery_LevelsUpFromBlocksDug()
        {
            var sim = CoreTests.FreshSim(); // no auto drilling, so only the pixels added here count
            int first = (int)(GameData.MasteryBlocks[0] * 256);
            for (int i = 0; i < first - 1; i++) sim.State.AddLayerPixel(0);
            sim.Tick(1.1);
            Assert.AreEqual(0, sim.MasteryOf(0));

            sim.State.AddLayerPixel(0);
            sim.Tick(1.1);
            Assert.AreEqual(1, sim.MasteryOf(0));
            Assert.IsTrue(sim.Events.Exists(e => e.Kind == SimEventKind.MasteryUp && (int)e.Amount == 0 && e.Row == 1));
        }

        [Test]
        public void Mastery_CountsWhatTheDrillsDig()
        {
            var sim = CoreTests.NewSim();
            for (int i = 0; i < 400; i++) sim.Tick(0.05);
            Assert.Greater(sim.MasteryBlocksDug(0), 0);
        }

        [Test]
        public void Mastery_BoostsDamageInItsOwnLayerOnly()
        {
            var sim = CoreTests.NewSim();
            int code = DrillCode.Make(Std, 0);
            double before = sim.DamageOf(code);
            for (int i = 0; i < GameData.MasteryBlocks[1] * 256; i++) sim.State.AddLayerPixel(0);
            sim.Tick(1.1);
            double bonus = 1 + 2 * GameData.MasteryDamagePct / 100;
            Assert.AreEqual(2, sim.MasteryOf(0));
            Assert.AreEqual(before * bonus, sim.DamageOf(code), before * 1e-6);

            sim.Teleport(150, 20);
            Assert.AreEqual(before, sim.DamageOf(code), before * 1e-6);
        }

        [Test]
        public void Mastery_RaisesThePayForThatLayer()
        {
            var sim = CoreTests.NewSim();
            for (int i = 0; i < GameData.MasteryBlocks[1] * 256; i++) sim.State.AddLayerPixel(0);
            sim.Tick(1.1);
            Assert.AreEqual(2 * GameData.MasteryValuePct, sim.MasteryValue(0), 1e-9);
            Assert.AreEqual(0, sim.MasteryValue(1), 1e-9);
        }

        [Test]
        public void Mastery_SurvivesRebirth()
        {
            var sim = CoreTests.NewSim();
            for (int i = 0; i < GameData.MasteryBlocks[0] * 256; i++) sim.State.AddLayerPixel(1);
            sim.Tick(1.1);
            sim.State.bossesDefeated = 1;
            sim.State.maxDepth = 400;
            sim.State.totalIncome = 1e6;
            Assert.IsTrue(sim.Rebirth());
            Assert.AreEqual(1, sim.MasteryOf(1));
        }

        [Test]
        public void ChainReaction_StopsWellBelowTheDrill()
        {
            var sim = CoreTests.NewSim();
            sim.GrantSkill(GameData.Skills.First(s => s.Effect == SkillEffect.chain).Id, 1);
            for (int tnt = 0; tnt < 2; tnt++) sim.GrantSkill(GameData.Skills.Where(s => s.Effect == SkillEffect.tnt_rate).ElementAt(tnt).Id, 20);
            sim.Teleport(30, 20);
            sim.Detonate(32, 20);
            Assert.LessOrEqual(sim.Events.Count(e => e.Kind == SimEventKind.Explosion), Balance.LinkedChainLimit + 1);
            foreach (var blast in sim.Events.Where(e => e.Kind == SimEventKind.Explosion))
                Assert.Less(blast.Row, 32 + Balance.ChainReachRows + 8);
        }

        [Test]
        public void Blast_SpendsDamageInsteadOfIgnoringRock()
        {
            int weak = CraterFrom(CoreTests.NewSim());
            Assert.Greater(weak, 0, "even a weak drill's charge opens a small hole");

            var strong = CoreTests.NewSim();
            strong.State.drills.Clear();
            strong.State.drills.Add(DrillCode.Make(0, 11));
            strong.SyncDrills();
            Assert.Greater(CraterFrom(strong), weak * 2);
        }

        private static int CraterFrom(GameSim sim)
        {
            sim.GrantSkill(GameData.Skills.First(s => s.Effect == SkillEffect.blast_radius).Id, 2);
            sim.Teleport(20, 20);
            int before = Crater(sim, 30);
            sim.Detonate(30, 20);
            return Crater(sim, 30) - before;
        }

        private static int Crater(GameSim sim, int row)
        {
            int cx = 20 * 16 + 8, cy = row * 16 + 8, open = 0;
            for (int y = cy - 40; y <= cy + 40; y++)
                for (int x = cx - 40; x <= cx + 40; x++)
                    if (!sim.IsSolidPixel(x, y)) open++;
            return open;
        }

        [Test]
        public void Body_NeverHoversJustAboveTheGround()
        {
            var body = new DrillBody();
            // a floor that a whole step cannot reach but the foot probe cannot feel either
            body.Place(5, 10 - DrillBody.HalfHeight - 0.04);
            for (int i = 0; i < 30; i++) body.Step(DrillBody.StepSeconds, (x, y) => y >= 10);
            Assert.IsTrue(body.Grounded);
        }
    }
}
