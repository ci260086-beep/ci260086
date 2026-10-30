using System;
using System.Linq;
using DeepDrill.Core;
using NUnit.Framework;

namespace DeepDrill.Tests
{
    public class CoreTests
    {
        private static GameSim NewSim(int seed = 42) => new GameSim(new GameState { seed = seed });

        private static void MakeBlock(GameSim sim, int row, int col, BlockType type, double hp = 0.1)
        {
            var b = sim.Row(row)[col];
            b.Type = type;
            b.Hp = hp;
            b.MaxHp = hp;
        }

        private static void Run(DrillBody body, Func<int, int, bool> solid, double seconds)
        {
            for (double t = 0; t < seconds; t += DrillBody.StepSeconds)
                body.Step(DrillBody.StepSeconds, solid);
        }

        // Stands on column 20 of a floor at row 9; the neighbouring columns are floor only if asked.
        private static DrillBody OnFloor(out Func<int, int, bool> solid, bool left, bool right)
        {
            var body = new DrillBody { X = 20, Y = 8.5 - DrillBody.HalfHeight };
            solid = (row, col) =>
            {
                if (row < 9) return false;
                if (col < 20) return left;
                if (col > 20) return right;
                return true;
            };
            return body;
        }

        [Test]
        public void Formulas_MatchTheBrief()
        {
            Assert.AreEqual(10, Balance.BlockHp(0), 1e-9);
            Assert.AreEqual(10 * Math.Pow(1.035, 50), Balance.BlockHp(50), 1e-9);
            Assert.AreEqual(2 * Math.Pow(1.03, 10), Balance.OreValue(10, 2), 1e-9);
            Assert.AreEqual(8 * 1.1 * 1.1, Balance.Cost(8, 1.1, 2), 1e-9);
            Assert.AreEqual((int)Math.Floor(Math.Pow(2, 1.5) * 6), Balance.ShardsForRebirth(200, 1e6));
        }

        [TestCase(0, "0")]
        [TestCase(0.5, "0.5")]
        [TestCase(999.9, "999.9")]
        [TestCase(1000, "1.00K")]
        [TestCase(1234567, "1.23M")]
        [TestCase(999999, "1.00M")]
        [TestCase(2.5e9, "2.50B")]
        [TestCase(7e12, "7.00T")]
        [TestCase(1e15, "1.00e15")]
        public void Numbers_AreShortened(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormat.Short(value));
        }

        [Test]
        public void Strata_RunFromTheSurfaceToTheCore()
        {
            Assert.AreEqual(12, Strata.All.Length);
            Assert.AreEqual(0, Strata.OfDepth(0));
            Assert.AreEqual(1, Strata.OfDepth(100));
            Assert.AreEqual(11, Strata.OfDepth(1150));
            Assert.AreEqual(11, Strata.OfDepth(5000), "the last stratum runs on");
            foreach (var s in Strata.All)
            {
                Assert.IsNotEmpty(s.BossName);
                Assert.IsNotEmpty(s.Ores);
                foreach (int ore in s.Ores) Assert.Less(ore, Strata.Ores.Length);
            }
        }

        [Test]
        public void EveryStratum_GeneratesItsOwnOres()
        {
            for (int s = 0; s < Strata.All.Length; s++)
            {
                var found = new System.Collections.Generic.HashSet<int>();
                for (int row = s * 100 + 10; row < s * 100 + 90; row++)
                    for (int col = 0; col < Balance.Columns; col++)
                        if (WorldGen.TypeAt(9, row, col) == BlockType.Ore) found.Add(WorldGen.OreAt(9, row, col));
                Assert.That(found.Count, Is.EqualTo(Strata.All[s].Ores.Length), Strata.All[s].Name);
            }
        }

        [Test]
        public void StrataMix_NearTheBoundaryOnly()
        {
            Assert.AreEqual(0, WorldGen.StratumAt(3, 90, 5));
            int mixed = 0;
            for (int col = 0; col < Balance.Columns; col++) mixed += WorldGen.StratumAt(3, 99, col);
            Assert.That(mixed, Is.InRange(10, 39));
        }

        [Test]
        public void Physics_LeftHole_TiltsAndSlidesLeft()
        {
            var body = OnFloor(out var solid, false, true);
            Run(body, solid, 0.3);
            Assert.Less(body.Angle, -10);
            Assert.Less(body.X, 20);
            Assert.Less(body.TipX, body.X);
        }

        [Test]
        public void Physics_RightHole_TiltsAndSlidesRight()
        {
            var body = OnFloor(out var solid, true, false);
            Run(body, solid, 0.3);
            Assert.Greater(body.Angle, 10);
            Assert.Greater(body.X, 20);
        }

        [Test]
        public void Physics_NoSupport_FallsWithACappedSpeed()
        {
            var body = new DrillBody { X = 20, Y = 0 };
            Run(body, (row, col) => false, 2);
            Assert.Greater(body.Y, 5);
            Assert.AreEqual(DrillBody.MaxFallSpeed, body.VelocityY, 1e-9);
        }

        [Test]
        public void Physics_BothSupported_StandsAndReturnsUpright()
        {
            var body = OnFloor(out var solid, true, true);
            body.Angle = 30;
            double y = body.Y;
            Run(body, solid, 3);
            Assert.AreEqual(0, body.Angle, 0.5);
            Assert.AreEqual(y, body.Y, 1e-9);
        }

        [Test]
        public void Physics_TiltMovesSmoothly()
        {
            var body = OnFloor(out var solid, false, true);
            body.Step(DrillBody.StepSeconds, solid);
            Assert.Greater(body.Angle, -6, "one step must not jump to full tilt");
        }

        [Test]
        public void Physics_LandingReportsImpact()
        {
            var body = new DrillBody { X = 20, Y = 0 };
            double impact = 0;
            for (int i = 0; i < 300 && impact == 0; i++)
                impact = body.Step(DrillBody.StepSeconds, (row, col) => row >= 8);
            Assert.Greater(impact, 10);
            Assert.AreEqual(7.5 - DrillBody.HalfHeight, body.Y, 1e-9);
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
        public void TiltedDrill_DigsWhereItsTipPoints()
        {
            var sim = NewSim();
            var body = sim.Lead.Body;
            body.Place(20, 10.5 - DrillBody.HalfHeight);
            body.Angle = -35;
            Assert.IsTrue(sim.FindTarget(body, out int row, out int col));
            Assert.AreEqual(11, row);
            Assert.Less(col, 20);
        }

        [Test]
        public void BuyingDrills_AddsBodiesAndCostsMore()
        {
            var sim = NewSim();
            sim.State.coins = 1e6;
            double first = sim.DrillCost(0);
            Assert.IsTrue(sim.BuyDrill(0));
            Assert.AreEqual(2, sim.Units.Count);
            Assert.Greater(sim.DrillCost(0), first);
        }

        [Test]
        public void HigherDrills_WaitForTheirDepth()
        {
            var sim = NewSim();
            sim.State.coins = 1e12;
            Assert.IsFalse(sim.BuyDrill(5));
            sim.State.maxDepthEver = DrillTiers.UnlockDepth(5);
            Assert.IsTrue(sim.BuyDrill(5));
        }

        [Test]
        public void Merging_TurnsThreeIntoOneStrongerDrill()
        {
            var sim = NewSim();
            sim.State.SetDrillCount(0, 3);
            Assert.IsTrue(sim.CanMerge(0));
            Assert.IsTrue(sim.Merge(0));
            Assert.AreEqual(0, sim.State.DrillCount(0));
            Assert.AreEqual(1, sim.State.DrillCount(1));
            Assert.Greater(DrillTiers.Damage(1), DrillTiers.Damage(0) * Balance.MergeCost, "merging must gain power");
            Assert.IsFalse(sim.CanMerge(0));
        }

        [Test]
        public void TopTier_CannotMerge()
        {
            var sim = NewSim();
            sim.State.SetDrillCount(DrillTiers.Count - 1, 10);
            Assert.IsFalse(sim.CanMerge(DrillTiers.Count - 1));
        }

        [Test]
        public void OwnedDrills_HaveNoCap_AndExtrasAddTheirPower()
        {
            var sim = NewSim();
            sim.State.coins = 0;
            sim.State.SetDrillCount(0, 5000);
            sim.Merge(0); // rebuilds units
            Assert.LessOrEqual(sim.Units.Count, Balance.MaxSimulatedDrills);
            Assert.Greater(sim.State.DrillsOwned, 4990);
            double perSwing = sim.DamageOf(0) * sim.Units.Count(u => u.Tier == 0);
            Assert.Greater(perSwing, DrillTiers.Damage(0) * 4000, "the unsimulated drills still count");
        }

        [Test]
        public void ManyDrills_KeepDiggingWithoutGettingStuck()
        {
            var sim = NewSim();
            sim.State.SetDrillCount(0, 60);
            sim.Merge(0);
            for (int i = 0; i < 400; i++) sim.Tick(0.05);
            Assert.Greater(sim.State.depth, 5);
            int nearLead = sim.Units.Count(u => Math.Abs(u.Body.Y - sim.Lead.Body.Y) < 12);
            Assert.Greater(nearLead, sim.Units.Count / 2, "stragglers rejoin the lead");
        }

        [Test]
        public void Click_DamagesAnyBlock_AndIgnoresBrokenOnes()
        {
            var sim = NewSim();
            var block = sim.Row(5)[0];
            double before = block.Hp;
            Assert.IsTrue(sim.Click(5, 0));
            Assert.Less(block.Hp, before);
            block.Hp = 0;
            Assert.IsFalse(sim.Click(5, 0));
        }

        [Test]
        public void Skills_NumberExactlyOneHundredAndFifty()
        {
            Assert.AreEqual(150, SkillCatalog.All.Count);
            Assert.AreEqual(150, SkillCatalog.All.Select(s => s.Id).Distinct().Count());
            Assert.AreEqual(150, SkillCatalog.All.Select(s => s.Name).Distinct().Count());
            foreach (var s in SkillCatalog.All)
            {
                Assert.Greater(s.MaxLevel, 0, s.Id);
                Assert.Greater(s.PerLevel, 0, s.Id);
                if (s.RequiresId != null) Assert.IsNotNull(SkillCatalog.Get(s.RequiresId), s.Id);
            }
        }

        [Test]
        public void SkillCosts_FollowTheirUnlockDepth()
        {
            foreach (var s in SkillCatalog.All)
            {
                double value = Math.Pow(1.03, s.UnlockDepth);
                Assert.That(s.BaseCost / value, Is.InRange(SkillCatalog.CostScale, SkillCatalog.CostScale * 3.01), s.Id);
            }
            Assert.Greater(SkillCatalog.All.Max(s => s.UnlockDepth), 1100, "the last skills wait near the core");
        }

        [Test]
        public void Skills_NeedDepthThenThePreviousRank()
        {
            var sim = NewSim();
            sim.State.coins = 1e15;
            string second = SkillCatalog.IdOf(SkillEffect.Damage, 1);
            sim.State.maxDepthEver = SkillCatalog.Get(second).UnlockDepth;
            Assert.IsFalse(sim.Buy(second), "rank I comes first");
            Assert.IsTrue(sim.Buy(SkillCatalog.IdOf(SkillEffect.Damage, 0)));
            Assert.IsTrue(sim.Buy(second));

            string late = SkillCatalog.IdOf(SkillEffect.DepthValue, 5);
            Assert.IsFalse(sim.Buy(late), "too deep yet");
        }

        [Test]
        public void SkillEffects_ChangeTheGame()
        {
            var sim = NewSim();
            double damage = sim.DamageOf(0);
            sim.GrantSkill(SkillCatalog.IdOf(SkillEffect.Damage, 0), 5);
            Assert.AreEqual(damage * 1.5, sim.DamageOf(0), 1e-9);

            double speed = sim.HitsPerSecond;
            sim.GrantSkill(SkillCatalog.IdOf(SkillEffect.Speed, 0), 5);
            Assert.AreEqual(speed * 1.2, sim.HitsPerSecond, 1e-9);
        }

        [Test]
        public void Combo_GrowsResetsAndIsCapped()
        {
            var sim = NewSim();
            for (int i = 0; i < 10; i++)
            {
                MakeBlock(sim, 8, 0, BlockType.Dirt);
                sim.Click(8, 0);
            }
            Assert.AreEqual(1.1, sim.ComboMultiplier, 1e-9);
            for (int i = 0; i < 300; i++)
            {
                MakeBlock(sim, 8, 0, BlockType.Dirt);
                sim.Click(8, 0);
            }
            Assert.AreEqual(1 + Balance.ComboBonusMax, sim.ComboMultiplier, 1e-9);
        }

        [Test]
        public void Frenzy_ChargesRunsAndStops()
        {
            var sim = NewSim();
            Assert.IsFalse(sim.StartFrenzy());
            for (int i = 0; i < 140; i++)
            {
                MakeBlock(sim, 8, 0, BlockType.Dirt);
                sim.Click(8, 0);
            }
            Assert.IsTrue(sim.FrenzyReady);
            double normal = sim.HitsPerSecond;
            Assert.IsTrue(sim.StartFrenzy());
            Assert.AreEqual(normal * Balance.FrenzySpeedMultiplier, sim.HitsPerSecond, 1e-9);
            for (int i = 0; i < 110; i++) sim.Tick(0.1);
            Assert.IsFalse(sim.InFrenzy);
        }

        [Test]
        public void Tnt_ChainsUpToTheLimit()
        {
            var sim = NewSim();
            for (int col = 0; col < 8; col++) MakeBlock(sim, 10, col, BlockType.Tnt);
            sim.Click(10, 0);
            Assert.AreEqual(Balance.BaseChainLimit, sim.Events.FindAll(e => e.Kind == SimEventKind.Explosion).Count);
        }

        private static GameSim SimAtBoss(int stratum)
        {
            var sim = NewSim();
            sim.State.bossesDefeated = stratum;
            sim.Teleport(Strata.BossRow(stratum), Balance.StartColumn);
            sim.Tick(0.1);
            return sim;
        }

        [Test]
        public void EveryStratum_HasAGuardianThatBlocksTheWay()
        {
            for (int s = 0; s < Strata.All.Length; s++)
            {
                var sim = SimAtBoss(s);
                Assert.IsTrue(sim.BossActive, Strata.All[s].BossName);
                Assert.AreEqual(BlockType.BossRoot, sim.Row(Strata.BossRow(s))[3].Type);
            }
        }

        [Test]
        public void Boss_DefeatOpensTheWayAndAdvances()
        {
            var sim = SimAtBoss(0);
            sim.State.SetDrillCount(10, 5);
            sim.Merge(0);
            sim.State.SetDrillCount(0, 3);
            sim.Merge(0);
            for (int i = 0; i < 2000 && sim.State.bossesDefeated == 0; i++) sim.Tick(0.05);
            Assert.AreEqual(1, sim.State.bossesDefeated);
            for (int i = 0; i < 100; i++) sim.Tick(0.05);
            Assert.Greater(sim.State.depth, Strata.BossRow(0) + 1);
        }

        [Test]
        public void Armour_HalvesDamageUntilGone()
        {
            var sim = SimAtBoss(1);
            Assert.Greater(sim.BossArmor, 0);
            sim.Events.Clear();
            sim.Click(Strata.BossRow(1), 0);
            var hit = sim.Events.Find(e => e.Kind == SimEventKind.BlockHit);
            double crit = hit.Crit ? Balance.CritMultiplier : 1;
            Assert.AreEqual(sim.ClickDamage * crit * Balance.BossArmorDamageRatio, hit.Amount, 1e-6);
        }

        [Test]
        public void Rebirth_ClearsTheRunAndKeepsRecords()
        {
            var sim = NewSim();
            var s = sim.State;
            s.bossesDefeated = 1;
            s.maxDepth = 400;
            s.maxDepthEver = 400;
            s.depth = 400;
            s.totalIncome = 1e6;
            s.coins = 12345;
            s.SetDrillCount(3, 7);
            s.rareItems.Add("rare_0");
            sim.GrantSkill(SkillCatalog.IdOf(SkillEffect.Damage, 0), 3);

            Assert.IsTrue(sim.Rebirth());
            Assert.AreEqual(48, s.shards);
            Assert.AreEqual(0, s.coins);
            Assert.AreEqual(0, s.depth);
            Assert.AreEqual(1, s.DrillsOwned);
            Assert.AreEqual(1, sim.Units.Count);
            Assert.AreEqual(0, s.LevelOf(SkillCatalog.IdOf(SkillEffect.Damage, 0)));
            Assert.AreEqual(400, s.maxDepthEver, "records stay");
            Assert.AreEqual(1, s.rareItems.Count, "rare finds stay");
            Assert.AreEqual(1, s.rebirths);
        }

        [Test]
        public void Loose_FallsCellByCellAndStops()
        {
            var sim = NewSim();
            for (int r = 5; r <= 10; r++) sim.Carve(r, 3);
            MakeBlock(sim, 4, 3, BlockType.Sand, 50);
            sim.Row(11);
            sim.Tick(Balance.LooseStepSeconds * 1.01);
            Assert.IsFalse(sim.Row(4)[3].Alive);
            Assert.AreEqual(BlockType.Sand, sim.Row(5)[3].Type);
            for (int i = 0; i < 12; i++) sim.Tick(Balance.LooseStepSeconds);
            Assert.AreEqual(BlockType.Sand, sim.Row(10)[3].Type);
            Assert.AreEqual(50, sim.Row(10)[3].Hp, 1e-9);
        }

        [Test]
        public void Rares_AreTwoPerStratumAndPayIncome()
        {
            Assert.AreEqual(24, RareItems.All.Count);
            for (int s = 0; s < Strata.All.Length; s++)
                Assert.AreEqual(2, RareItems.All.Count(r => r.Stratum == s));
        }

        [Test]
        public void Achievements_UnlockFromProgressOnce()
        {
            Assert.Greater(AchievementCatalog.All.Count, 40);
            Assert.AreEqual(AchievementCatalog.All.Count, AchievementCatalog.All.Select(a => a.Id).Distinct().Count());

            var sim = NewSim();
            sim.State.SetDrillCount(0, 12);
            sim.Tick(1.1);
            Assert.Contains("drills_10", sim.State.achievements);
            int count = sim.State.achievements.Count;
            sim.Tick(1.1);
            Assert.AreEqual(count, sim.State.achievements.Count);
        }

        [Test]
        public void Offline_PaysHalfCapsAndUsesTheSkill()
        {
            Assert.AreEqual(50, Balance.OfflineEarnings(10, 10), 1e-9);
            Assert.AreEqual(10 * 7200 * 0.5, Balance.OfflineEarnings(10, 999999), 1e-9);
            Assert.AreEqual(0, Balance.OfflineEarnings(10, -5), 1e-9);
        }

        [Test]
        public void OldSave_DropsItsObsoleteSkills()
        {
            var state = new GameState { version = 3 };
            state.SetLevel("drill_damage", 5);
            state.Upgrade();
            Assert.IsEmpty(state.skills);
            Assert.AreEqual(GameState.CurrentVersion, state.version);
        }

        [Test]
        public void ReloadedRun_KeepsTheDrillsAndGround()
        {
            var sim = NewSim();
            sim.State.SetDrillCount(0, 5);
            sim.Merge(0);
            while (sim.State.depth < 5) sim.Tick(0.1);
            var reloaded = new GameSim(sim.State);
            Assert.AreEqual(sim.State.DrillsOwned, reloaded.Units.Count);
            Assert.AreEqual(sim.Lead.Body.X, reloaded.Lead.Body.X, 1e-9);
        }
    }
}
