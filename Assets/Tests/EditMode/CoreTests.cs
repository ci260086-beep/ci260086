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
            int lastTopsoilRow = Balance.SandstoneRow - Balance.TransitionRows;
            for (int row = 3; row < lastTopsoilRow; row++)
                for (int col = 1; col < Balance.StartColumn; col++)
                {
                    total++;
                    var a = WorldGen.TypeAt(5, row, col);
                    var b = WorldGen.TypeAt(5, row, 2 * Balance.StartColumn - col);
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

        // A flat floor one row below the body, with no blocks anywhere else near it.
        private static DrillBody BodyOnFloor(out System.Func<int, int, bool> solid, bool left, bool right)
        {
            var body = new DrillBody { X = 20, Y = 8.5 - DrillBody.HalfHeight };
            solid = (row, col) =>
            {
                if (row < 9) return false;
                if (col < 20) return left;
                if (col > 20) return right;
                return left && right;
            };
            return body;
        }

        private static void Run(DrillBody body, System.Func<int, int, bool> solid, double seconds)
        {
            for (double t = 0; t < seconds; t += DrillBody.StepSeconds)
                body.Step(DrillBody.StepSeconds, solid);
        }

        [Test]
        public void Physics_LeftHole_TiltsAndSlidesLeft()
        {
            var body = BodyOnFloor(out var solid, false, true);
            Run(body, solid, 0.3);
            Assert.Less(body.Angle, -10, "tips towards the hole");
            Assert.Less(body.X, 20, "slides towards the hole");
            Assert.Less(body.TipX, body.X, "the tip now points down and left");
        }

        [Test]
        public void Physics_RightHole_TiltsAndSlidesRight()
        {
            var body = BodyOnFloor(out var solid, true, false);
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
            var body = BodyOnFloor(out var solid, true, true);
            body.Angle = 30;
            double y = body.Y;
            Run(body, solid, 3);
            Assert.AreEqual(0, body.Angle, 0.5);
            Assert.AreEqual(y, body.Y, 1e-9);
            Assert.AreEqual(20, body.X, 1e-9);
        }

        [Test]
        public void Physics_TiltNeverSnapsAndStaysNearTheLimit()
        {
            var body = BodyOnFloor(out var solid, false, true);
            body.Step(DrillBody.StepSeconds, solid);
            Assert.Greater(body.Angle, -5, "one step must not jump to full tilt");

            double most = 0;
            for (double t = 0; t < 2; t += DrillBody.StepSeconds)
            {
                body.Step(DrillBody.StepSeconds, solid);
                most = System.Math.Min(most, body.Angle);
            }
            Assert.Greater(most, -DrillBody.MaxTilt * 1.4, "spring overshoot stays small");
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
        public void Physics_WallStopsSideways()
        {
            var body = BodyOnFloor(out var floor, false, true);
            System.Func<int, int, bool> solid = (row, col) => col <= 18 || floor(row, col);
            Run(body, solid, 1);
            Assert.AreEqual(0, body.VelocityX, 1e-9);
            Assert.Greater(body.X - DrillBody.HalfWidth, 18.4);
        }

        [Test]
        public void Tilted_DrillDigsTheBlockItsTipPointsAt()
        {
            var sim = NewSim();
            for (int r = 0; r < 12; r++) // a floor at row 12, with a hole to the left of the body
                for (int c = 10; c < 30; c++)
                    sim.Row(r)[c].Hp = 0;
            sim.Drill.X = 20;
            sim.Drill.Y = 11.5 - DrillBody.HalfHeight;
            sim.Drill.Angle = -30;
            Assert.IsTrue(sim.FindTarget(out int row, out int col));
            Assert.AreEqual(12, row);
            Assert.Less(col, 20, "digs down and left");

            sim.Drill.Angle = 30;
            Assert.IsTrue(sim.FindTarget(out _, out col));
            Assert.Greater(col, 20, "digs down and right");
        }

        [Test]
        public void BlastHole_BesideTheDrill_MakesItTilt()
        {
            var sim = NewSim();
            MakeBlock(sim, 1, 18, BlockType.Tnt);
            sim.Row(0)[19].Hp = 0;
            sim.Row(0)[18].Hp = 0;
            sim.Row(0)[17].Hp = 0;
            sim.Click(1, 18);
            sim.Tick(0.05);
            for (int i = 0; i < 20; i++)
            {
                for (int c = 20; c <= 23; c++) sim.Row(0)[c].Hp = 1e12; // the right foot stays planted
                sim.Tick(0.02);
            }
            Assert.Less(sim.Drill.Angle, -5);
        }

        [Test]
        public void Drill_WalksRoundACarvedPocketAndKeepsGoing()
        {
            var sim = NewSim();
            for (int r = 3; r < 7; r++)
                for (int c = 15; c < 20; c++)
                    sim.Carve(r, c);
            double startX = sim.Drill.X;
            for (int i = 0; i < 1500; i++) sim.Tick(0.1);
            Assert.Greater(sim.State.depth, 10, "never wedges");
            Assert.AreNotEqual(startX, sim.Drill.X, "the tunnel bent");
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

        private static void MakeBlock(GameSim sim, int row, int col, BlockType type, double hp = 0.1)
        {
            var b = sim.Row(row)[col];
            b.Type = type;
            b.Hp = hp;
            b.MaxHp = hp;
        }

        [Test]
        public void Combo_GrowsResetsAndBoostsPerTenBreaks()
        {
            var sim = NewSim();
            for (int col = 0; col < 10; col++)
            {
                MakeBlock(sim, 8, col, BlockType.Dirt);
                sim.Click(8, col);
            }
            Assert.AreEqual(10, sim.Combo);
            Assert.AreEqual(1.1, sim.ComboMultiplier, 1e-9);
            // the drill must not break anything while the window runs out
            for (int col = 0; col < Balance.Columns; col++) sim.Row(0)[col].Hp = 1e12;
            sim.Tick(Balance.ComboWindowSeconds + 0.01);
            Assert.AreEqual(0, sim.Combo);
        }

        [Test]
        public void Crits_HappenAtAboutTheConfiguredRateAndMultiply()
        {
            var sim = NewSim();
            int crits = 0;
            for (int i = 0; i < 4000; i++)
            {
                MakeBlock(sim, 8, 0, BlockType.Dirt, 1e12);
                sim.Click(8, 0);
            }
            foreach (var e in sim.Events)
            {
                if (e.Kind != SimEventKind.BlockHit) continue;
                if (e.Crit)
                {
                    crits++;
                    Assert.AreEqual(sim.DamagePerHit * Balance.CritMultiplier, e.Amount, 1e-9);
                }
            }
            Assert.That(crits, Is.InRange(320, 480));
        }

        [Test]
        public void Frenzy_ChargesFromCombosRunsThenStops()
        {
            var sim = NewSim();
            Assert.IsFalse(sim.StartFrenzy());
            for (int i = 0; i < 140; i++)
            {
                MakeBlock(sim, 8, 0, BlockType.Dirt);
                sim.Click(8, 0);
            }
            Assert.IsTrue(sim.FrenzyReady);
            double normalSpeed = sim.HitsPerSecond;
            Assert.IsTrue(sim.StartFrenzy());
            Assert.AreEqual(normalSpeed * 5, sim.HitsPerSecond, 1e-9);
            Assert.AreEqual(0, sim.FrenzyEnergy, 1e-9);
            sim.Tick(Balance.FrenzySeconds + 0.01);
            Assert.IsFalse(sim.InFrenzy);
            Assert.AreEqual(normalSpeed, sim.HitsPerSecond, 1e-9);
        }

        [Test]
        public void FrenzyBurst_ExplodesWhenFrenzyEnds()
        {
            var sim = NewSim();
            sim.State.SetLevel(SkillCatalog.FrenzyBurst, 1);
            for (int i = 0; i < 140; i++)
            {
                MakeBlock(sim, 8, 0, BlockType.Dirt);
                sim.Click(8, 0);
            }
            sim.StartFrenzy();
            sim.Events.Clear();
            sim.Tick(Balance.FrenzySeconds + 0.01);
            Assert.IsTrue(sim.Events.Exists(e => e.Kind == SimEventKind.Explosion));
        }

        [Test]
        public void Tnt_BreaksNeighboursAndPaysForThem()
        {
            var sim = NewSim();
            MakeBlock(sim, 10, 2, BlockType.Tnt);
            sim.Row(10)[3].Type = BlockType.Dirt;
            sim.Click(10, 2);
            Assert.IsFalse(sim.Row(10)[3].Alive, "right neighbour");
            Assert.IsFalse(sim.Row(9)[2].Alive, "above");
            Assert.IsFalse(sim.Row(11)[2].Alive, "below");
            Assert.IsTrue(sim.Row(10)[5].Alive, "outside the radius");
            Assert.Greater(sim.State.coins, 0);
        }

        [Test]
        public void Tnt_ChainsUpToTheLimit()
        {
            var sim = NewSim();
            for (int col = 0; col < 8; col++) MakeBlock(sim, 10, col, BlockType.Tnt);
            sim.Click(10, 0);
            int blasts = sim.Events.FindAll(e => e.Kind == SimEventKind.Explosion).Count;
            Assert.AreEqual(Balance.BaseChainLimit, blasts);

            sim = NewSim();
            sim.State.SetLevel(SkillCatalog.BlastChain, 1);
            for (int col = 0; col < 8; col++) MakeBlock(sim, 10, col, BlockType.Tnt);
            sim.Click(10, 0);
            blasts = sim.Events.FindAll(e => e.Kind == SimEventKind.Explosion).Count;
            Assert.AreEqual(Balance.BaseChainLimit + Balance.ChainLimitPerLevel, blasts);
        }

        [Test]
        public void BlastLoot_DoublesOreFromExplosions()
        {
            var sim = NewSim();
            sim.State.SetLevel(SkillCatalog.BlastLoot, 1);
            MakeBlock(sim, 10, 2, BlockType.Tnt);
            for (int r = 9; r <= 11; r++)
                for (int c = 1; c <= 3; c++)
                    if (r != 10 || c != 2) sim.Row(r)[c].Type = BlockType.Dirt;
            sim.Row(10)[3].Type = BlockType.Copper;
            sim.Click(10, 2);
            Assert.AreEqual(2, sim.State.ores, 1e-9);
        }

        [Test]
        public void TwinDrill_SwingsTwice()
        {
            var sim = NewSim();
            sim.Tick(1.001 / Balance.BaseHitsPerSecond);
            Assert.AreEqual(3, sim.Events.FindAll(e => e.Kind == SimEventKind.BlockHit).Count);

            sim = NewSim();
            sim.State.SetLevel(SkillCatalog.DrillTwin, 1);
            sim.Tick(1.001 / Balance.BaseHitsPerSecond);
            Assert.AreEqual(6, sim.Events.FindAll(e => e.Kind == SimEventKind.BlockHit).Count);
        }

        [Test]
        public void EvenGround_IsDugStraightDown()
        {
            var sim = NewSim();
            for (int r = 0; r < 30; r++) // plain dirt with no holes to fall into
                for (int c = 0; c < Balance.Columns; c++)
                    MakeBlock(sim, r, c, BlockType.Dirt, Balance.BlockHp(r));
            while (sim.State.depth < 20) sim.Tick(0.05);
            Assert.AreEqual(Balance.StartColumn, sim.Drill.X, 1e-9);
        }

        [Test]
        public void ToughBlockUnderOneFoot_BendsTheTunnelAwayFromIt()
        {
            var sim = NewSim();
            for (int r = 0; r < 12; r++)
                for (int c = 0; c < Balance.Columns; c++)
                    MakeBlock(sim, r, c, BlockType.Dirt, Balance.BlockHp(r));
            MakeBlock(sim, 3, Balance.StartColumn + 1, BlockType.HardRock, 1e6);
            for (int i = 0; i < 400 && sim.State.depth < 8; i++) sim.Tick(0.05);
            Assert.Less(sim.Drill.X, Balance.StartColumn, "slid off the rock into the left side of the hole");
        }

        [Test]
        public void Pierce_AlsoDamagesTheBlockBelow()
        {
            var sim = NewSim();
            sim.State.SetLevel(SkillCatalog.DrillPierce, 2);
            double full = sim.Row(1)[Balance.StartColumn].MaxHp;
            sim.Tick(1.001 / Balance.BaseHitsPerSecond);
            Assert.Less(sim.Row(1)[Balance.StartColumn].Hp, full);
        }

        [Test]
        public void HardRockSkill_RaisesDamageOnHardRockOnly()
        {
            var sim = NewSim();
            sim.State.SetLevel(SkillCatalog.DrillHard, 5);
            MakeBlock(sim, 8, 0, BlockType.HardRock, 1e12);
            sim.Click(8, 0);
            var hit = sim.Events.Find(e => e.Kind == SimEventKind.BlockHit);
            double crit = hit.Crit ? Balance.CritMultiplier : 1;
            Assert.AreEqual(sim.DamagePerHit * crit * 2, hit.Amount, 1e-6);
        }

        private static GameSim SimAtBoss()
        {
            var sim = NewSim();
            sim.Teleport(Balance.BossRow, Balance.StartColumn);
            sim.Tick(0.05);
            return sim;
        }

        [Test]
        public void Boss_StartsWhenTheDrillReachesTheRootAndBlocksTheWay()
        {
            var sim = SimAtBoss();
            Assert.IsTrue(sim.BossActive);
            Assert.AreEqual(Balance.BossSeconds, sim.BossTimeLeft, 0.2);
            for (int i = 0; i < 40; i++) sim.Tick(0.1);
            Assert.AreEqual(Balance.BossRow, sim.State.depth, "the root holds the drill");
            Assert.Less(sim.BossHp, sim.BossMaxHp);
        }

        [Test]
        public void Boss_RegrowsWhenTimeRunsOut()
        {
            var sim = SimAtBoss();
            for (int i = 0; i < 20; i++) sim.Tick(0.1);
            sim.Events.Clear();
            for (int i = 0; i < Balance.BossSeconds * 10 + 5; i++) sim.Tick(0.1);
            Assert.IsTrue(sim.Events.Exists(e => e.Kind == SimEventKind.BossFailed));
            Assert.IsTrue(sim.BossActive);
            Assert.IsFalse(sim.State.AnyBossDown);
            Assert.Greater(sim.BossTimeLeft, Balance.BossSeconds - 6);
        }

        [Test]
        public void Boss_DefeatOpensTheWayAndPays()
        {
            var sim = SimAtBoss();
            sim.State.SetLevel(SkillCatalog.DrillDamage, 100);
            sim.State.SetLevel(SkillCatalog.DrillSpeed, 50);
            for (int i = 0; i < 2000 && !sim.State.AnyBossDown; i++) sim.Tick(0.05);
            Assert.IsTrue(sim.State.AnyBossDown);
            Assert.Greater(sim.State.coins, 1000);
            for (int i = 0; i < 100; i++) sim.Tick(0.05);
            Assert.Greater(sim.State.depth, Balance.BossRow + 1, "falls through the breach");
        }

        [Test]
        public void Boss_IgnoresTntAndEarlyClicks()
        {
            var sim = NewSim();
            Assert.IsFalse(sim.Click(Balance.BossRow, 3), "not started yet");
            MakeBlock(sim, Balance.BossRow - 1, 3, BlockType.Tnt);
            sim.Click(Balance.BossRow - 1, 3);
            Assert.IsTrue(sim.Row(Balance.BossRow)[3].Alive);
        }

        private static GameSim SimAtBeetle()
        {
            var sim = NewSim();
            sim.State.bossesDefeated = 1;
            sim.Teleport(Balance.BeetleRow, Balance.StartColumn);
            sim.Tick(0.05);
            return sim;
        }

        [Test]
        public void Beetle_ArmourHalvesDamageUntilItBreaks()
        {
            var sim = SimAtBeetle();
            Assert.IsTrue(sim.BossActive);
            Assert.Greater(sim.BossArmor, 0);

            sim.Events.Clear();
            sim.Click(Balance.BeetleRow, 0);
            var armoured = sim.Events.Find(e => e.Kind == SimEventKind.BlockHit);
            double crit = armoured.Crit ? Balance.CritMultiplier : 1;
            Assert.AreEqual(sim.DamagePerHit * crit * 0.5, armoured.Amount, 1e-6);

            sim.State.SetLevel(SkillCatalog.DrillDamage, 100);
            sim.State.SetLevel(SkillCatalog.DrillSpeed, 50);
            for (int i = 0; i < 1000 && sim.BossArmor > 0; i++) sim.Tick(0.05);
            Assert.AreEqual(0, sim.BossArmor, 1e-9);

            sim.Events.Clear();
            sim.Click(Balance.BeetleRow, 0);
            var bare = sim.Events.Find(e => e.Kind == SimEventKind.BlockHit);
            crit = bare.Crit ? Balance.CritMultiplier : 1;
            Assert.AreEqual(sim.DamagePerHit * crit, bare.Amount, 1e-6);
        }

        [Test]
        public void Beetle_ArmourGrowsBackWhenTimeRunsOut()
        {
            var sim = SimAtBeetle();
            sim.Click(Balance.BeetleRow, 0);
            Assert.Less(sim.BossArmor, sim.BossMaxArmor);
            for (int i = 0; i < Balance.BossSeconds * 10 + 5; i++) sim.Tick(0.1);
            Assert.Greater(sim.BossArmor, sim.BossMaxArmor * 0.5);
        }

        [Test]
        public void SecondBoss_OnlyWaitsBehindTheFirst()
        {
            var sim = NewSim();
            Assert.AreEqual("巨型木根", sim.CurrentBoss.Name);
            sim.State.bossesDefeated = 1;
            Assert.AreEqual("岩甲虫", sim.CurrentBoss.Name);
            sim.State.bossesDefeated = 2;
            Assert.IsNull(sim.CurrentBoss);
        }

        [Test]
        public void Sandstone_HasItsOreAndSandAndTheStrataMix()
        {
            int iron = 0, silver = 0, sand = 0, copper = 0;
            for (int row = 110; row < 190; row++)
                for (int col = 0; col < Balance.Columns; col++)
                {
                    var t = WorldGen.TypeAt(3, row, col);
                    if (t == BlockType.Iron) iron++;
                    if (t == BlockType.Silver) silver++;
                    if (t == BlockType.Sand) sand++;
                    if (t == BlockType.Copper) copper++;
                }
            Assert.Greater(iron, silver);
            Assert.Greater(silver, copper, "copper is rare here");
            Assert.Greater(sand, iron);
            Assert.Greater(WorldGen.ValueOf(BlockType.Iron, 150), WorldGen.ValueOf(BlockType.Copper, 150));
            Assert.Greater(WorldGen.ValueOf(BlockType.Silver, 150), WorldGen.ValueOf(BlockType.Iron, 150));

            int mixedSand = 0;
            for (int col = 0; col < Balance.Columns; col++)
                mixedSand += WorldGen.StratumAt(3, Balance.SandstoneRow - 1, col);
            Assert.That(mixedSand, Is.InRange(10, 39), "the last rows are a mix, not a hard line");
            Assert.AreEqual(0, WorldGen.StratumAt(3, Balance.SandstoneRow - Balance.TransitionRows - 1, 5));
            Assert.AreEqual(1, WorldGen.StratumAt(3, Balance.SandstoneRow, 5));
        }

        [Test]
        public void Sand_FallsCellByCellAndStopsOnTheGround()
        {
            var sim = NewSim();
            for (int r = 5; r <= 10; r++) sim.Carve(r, 3);
            MakeBlock(sim, 4, 3, BlockType.Sand, 50);
            sim.Row(10);
            sim.Row(11);

            sim.Tick(Balance.SandStepSeconds * 1.01);
            Assert.IsFalse(sim.Row(4)[3].Alive);
            Assert.AreEqual(BlockType.Sand, sim.Row(5)[3].Type);
            Assert.IsTrue(sim.Row(5)[3].Alive);

            for (int i = 0; i < 12; i++) sim.Tick(Balance.SandStepSeconds);
            Assert.IsTrue(sim.Row(10)[3].Alive, "rests on the block below");
            Assert.AreEqual(BlockType.Sand, sim.Row(10)[3].Type);
            Assert.AreEqual(50, sim.Row(10)[3].Hp, 1e-9);
        }

        [Test]
        public void Sand_WaitsOnTheDrillInsteadOfFallingThroughIt()
        {
            var sim = NewSim();
            sim.Teleport(10, Balance.StartColumn);
            int col = Balance.StartColumn;
            for (int c = col - 2; c <= col + 2; c++)
            {
                MakeBlock(sim, 10, c, BlockType.Dirt, 1e9);
                MakeBlock(sim, 6, c, BlockType.Sand, 1e9);
                sim.Carve(7, c);
                sim.Carve(8, c);
                sim.Carve(9, c);
            }
            for (int i = 0; i < 12; i++) sim.Tick(Balance.SandStepSeconds);
            Assert.AreEqual(BlockType.Sand, sim.Row(7)[col].Type);
            Assert.IsTrue(sim.Row(7)[col].Alive, "stopped on top of the body");
            Assert.IsFalse(sim.Row(8)[col].Alive);
            Assert.IsFalse(sim.Row(9)[col].Alive);
        }

        [Test]
        public void Sand_LandingUnderAFootGivesTheDrillSupport()
        {
            var sim = NewSim();
            sim.Teleport(10, Balance.StartColumn);
            int leftFoot = Balance.StartColumn - 1;
            sim.Carve(10, leftFoot);
            MakeBlock(sim, 9, leftFoot - 2, BlockType.Sand, 1e9);
            Assert.IsFalse(sim.IsSolid(10, leftFoot));

            sim.Row(9)[leftFoot].Hp = 0;
            var sand = sim.Row(9)[leftFoot - 2];
            sim.Row(9)[leftFoot - 2] = sim.Row(9)[leftFoot];
            sim.Row(9)[leftFoot] = sand; // sand has drifted over the hole the foot hangs above
            sim.Drill.X = Balance.StartColumn + 2.2; // clear of the column while it drops
            sim.Tick(Balance.SandStepSeconds * 1.01);
            Assert.IsTrue(sim.IsSolid(10, leftFoot), "the hole is plugged");
        }

        [Test]
        public void EnteringSandstone_FiresOnce()
        {
            var sim = NewSim();
            sim.State.bossesDefeated = 1;
            sim.Teleport(Balance.SandstoneRow - 1, Balance.StartColumn);
            for (int c = 16; c <= 24; c++)
                for (int r = Balance.SandstoneRow - 1; r <= Balance.SandstoneRow + 1; r++)
                    sim.Carve(r, c);
            sim.Events.Clear();
            for (int i = 0; i < 40; i++) sim.Tick(0.05);
            Assert.AreEqual(1, sim.Events.FindAll(e => e.Kind == SimEventKind.StratumEntered).Count);
        }

        [Test]
        public void OldSave_KeepsItsBossKill()
        {
            var state = new GameState { version = 2, bossDefeated = true };
            state.Upgrade();
            Assert.AreEqual(1, state.bossesDefeated);
            Assert.AreEqual(GameState.CurrentVersion, state.version);

            var fresh = new GameState { version = 3, bossesDefeated = 2 };
            fresh.Upgrade();
            Assert.AreEqual(2, fresh.bossesDefeated);
        }

        [Test]
        public void Rebirth_NeedsTheBossDown()
        {
            var sim = NewSim();
            sim.State.maxDepth = 300;
            sim.State.totalIncome = 1e6;
            Assert.IsFalse(sim.CanRebirth);
            Assert.IsFalse(sim.Rebirth());
        }

        [Test]
        public void Rebirth_FollowsTheShardFormulaAndClearsTheRun()
        {
            Assert.AreEqual((int)System.Math.Floor(System.Math.Pow(2, 1.5) * 6), Balance.ShardsForRebirth(200, 1e6));

            var sim = NewSim();
            var s = sim.State;
            s.bossesDefeated = 1;
            s.maxDepth = 400;
            s.depth = 400;
            s.totalIncome = 1e6;
            s.coins = 12345;
            s.ores = 99;
            s.SetLevel(SkillCatalog.DrillDamage, 10);
            int oldSeed = s.seed;
            int expected = (int)System.Math.Floor(8 * 6.0);

            Assert.IsTrue(sim.Rebirth());
            Assert.AreEqual(expected, s.shards);
            Assert.AreEqual(0, s.coins);
            Assert.AreEqual(0, s.ores);
            Assert.AreEqual(0, s.depth);
            Assert.AreEqual(0, s.maxDepth);
            Assert.AreEqual(0, s.LevelOf(SkillCatalog.DrillDamage));
            Assert.IsFalse(s.AnyBossDown);
            Assert.AreNotEqual(oldSeed, s.seed);
            Assert.AreEqual(Balance.StartColumn, sim.Drill.X, 1e-9);

            double bonus = 1 + Balance.ShardBonus * expected;
            Assert.AreEqual(Balance.BaseDamage * bonus, sim.DamagePerHit, 1e-9);
            Assert.IsFalse(sim.Rebirth(), "nothing to reforge straight after");
        }

        [Test]
        public void EverySkillRequirement_PointsAtARealSkill()
        {
            foreach (var skill in SkillCatalog.All)
            {
                if (skill.RequiresId == null) continue;
                var parent = SkillCatalog.Get(skill.RequiresId);
                Assert.IsNotNull(parent, skill.Id);
                Assert.LessOrEqual(skill.RequiresLevel, parent.MaxLevel, skill.Id);
            }
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
            Assert.AreEqual(sim.Drill.X, reloaded.Drill.X, 1e-9);
            Assert.AreEqual(sim.Drill.Y, reloaded.Drill.Y, 1e-9);
            Assert.IsFalse(reloaded.Row(2)[(int)System.Math.Round(reloaded.Drill.X)].Alive);
            Assert.IsTrue(reloaded.Row(2)[0].Alive);
            Assert.AreEqual(sim.Row(40)[3].Type, reloaded.Row(40)[3].Type);
        }
    }
}
