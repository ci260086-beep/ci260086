using System;
using System.IO;
using System.Linq;
using DeepDrill.Core;
using NUnit.Framework;

namespace DeepDrill.Tests
{
    public class CoreTests
    {
        [OneTimeSetUp]
        public void LoadData() => EnsureData();

        // Walks up from wherever the tests run until it finds the project's data folder.
        private static void EnsureData()
        {
            if (GameData.Loaded) return;
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "Resources", "Data"))) dir = dir.Parent;
            string data = dir != null
                ? Path.Combine(dir.FullName, "Assets", "Resources", "Data")
                : Path.Combine("Assets", "Resources", "Data");
            GameData.Load(name => File.ReadAllText(Path.Combine(data, name + ".json")));
        }

        // Most rules are tested with auto drilling on; FreshSim is a game as the player first meets it.
        private static GameSim NewSim(int seed = 42)
        {
            var sim = FreshSim(seed);
            sim.GrantSkill(GameSim.RootSkill, 1);
            return sim;
        }

        private static GameSim FreshSim(int seed = 42)
        {
            EnsureData();
            return new GameSim(new GameState { seed = seed });
        }

        [Test]
        public void BeforeUnlock_DrillsDoNotDigByThemselves()
        {
            var sim = FreshSim();
            for (int i = 0; i < 200; i++) sim.Tick(0.1);
            Assert.AreEqual(0, sim.State.coins, 1e-9);
            Assert.AreEqual(0, sim.State.depth);
            Assert.IsFalse(sim.Events.Exists(e => e.Kind == SimEventKind.BlockHit));
        }

        [Test]
        public void BeforeUnlock_TappingADrillSwingsItOnce()
        {
            var sim = FreshSim();
            sim.Tick(0.1);
            sim.Events.Clear();
            var body = sim.Lead.Body;
            Assert.IsTrue(sim.Click(body.BodyRow, body.Column), "the drill itself");
            Assert.IsTrue(sim.Click(body.BodyRow + 1, body.Column), "the block under it");
            var hits = sim.Events.FindAll(e => e.Kind == SimEventKind.BlockHit);
            Assert.AreEqual(2, hits.Count);
            Assert.IsTrue(hits.TrueForAll(e => e.FromClick), "full click feedback");
            Assert.IsFalse(sim.Click(body.BodyRow + 1, 3), "other blocks do nothing yet");
        }

        [Test]
        public void TappingALot_EarnsTheUnlockInAboutHalfAMinuteToAMinute()
        {
            var sim = FreshSim();
            double cost = GameData.Skill(GameSim.RootSkill).CostAt(0);
            const double tapsPerSecond = 5;
            double seconds = 0;
            while (sim.State.coins < cost && seconds < 600)
            {
                sim.Tick(1 / tapsPerSecond);
                sim.Click(sim.Lead.Body.BodyRow + 1, sim.Lead.Body.Column);
                seconds += 1 / tapsPerSecond;
            }
            Assert.That(seconds, Is.InRange(20, 75));
        }

        [Test]
        public void Unlocking_StartsAutomaticDigging()
        {
            var sim = FreshSim();
            sim.State.coins = 15;
            Assert.IsTrue(sim.Buy(GameSim.RootSkill));
            Assert.IsTrue(sim.AutoDrillUnlocked);
            for (int i = 0; i < 300; i++) sim.Tick(0.1);
            Assert.Greater(sim.State.depth, 0);
        }

        [Test]
        public void RootLevels_UnlockFirstThenAddDamage()
        {
            var sim = FreshSim();
            int code = DrillCode.Make(0, 0);
            double damage = sim.DamageOf(code);
            sim.GrantSkill(GameSim.RootSkill, 1);
            Assert.AreEqual(damage, sim.DamageOf(code), 1e-9, "level 1 is the switch");
            sim.GrantSkill(GameSim.RootSkill, 3);
            Assert.AreEqual(damage * 1.1, sim.DamageOf(code), 1e-9);
        }

        [Test]
        public void BeforeUnlock_NoOfflineIncomeOrAutoHangar()
        {
            var sim = FreshSim();
            sim.State.incomePerSecond = 100;
            sim.State.lastSaveUnixSeconds = 1000;
            Assert.AreEqual(0, sim.ApplyOffline(5000), 1e-9);

            sim.GrantSkill(GameData.Skills.First(s => s.Effect == SkillEffect.auto_merge).Id, 1);
            sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.Tick(1.1);
            Assert.AreEqual(2, sim.State.drills.Count, "auto merge waits for the unlock");
        }

        [Test]
        public void StartGoldSkill_KeepsAutoDrillThroughRebirth()
        {
            var sim = NewSim();
            var s = sim.State;
            s.bossesDefeated = 1;
            s.maxDepth = 400;
            s.totalIncome = 1e6;
            sim.GrantSkill(GameData.Skills.First(x => x.Effect == SkillEffect.start_gold).Id, 1);
            Assert.IsTrue(sim.Rebirth());
            Assert.IsTrue(sim.AutoDrillUnlocked);

            var plain = NewSim();
            plain.State.bossesDefeated = 1;
            plain.State.maxDepth = 400;
            plain.State.totalIncome = 1e6;
            Assert.IsTrue(plain.Rebirth());
            Assert.IsFalse(plain.AutoDrillUnlocked, "the switch is cleared like any skill");
        }

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
        public void Data_MatchesTheGenerator()
        {
            EnsureData();
            Assert.AreEqual(12, GameData.Layers.Count);
            Assert.AreEqual(2550, GameData.TotalRows);
            Assert.AreEqual(5, GameData.Families.Count);
            Assert.AreEqual(12, GameData.Tiers);
            Assert.AreEqual(151, GameData.Skills.Count, "150 plus the root");
            Assert.AreEqual(46, GameData.Achievements.Count);
            Assert.AreEqual(36, GameData.Rares.Count);
            Assert.AreEqual(0, GameData.Layers[0].RowStart);
            Assert.AreEqual(100, GameData.Layers[1].RowStart);
            Assert.AreEqual(250, GameData.Layers[1].RowEnd);
        }

        [Test]
        public void Every35Effects_IsUsedByASkill()
        {
            EnsureData();
            var used = GameData.Skills.Select(s => s.Effect).Distinct().Count();
            Assert.AreEqual(Enum.GetValues(typeof(SkillEffect)).Length, used);
        }

        [Test]
        public void SkillTree_HasNoOverlapsAndValidLinks()
        {
            EnsureData();
            var cells = GameData.Skills.Select(s => $"{s.X:0.00}:{s.Y:0.00}").ToList();
            Assert.AreEqual(cells.Count, cells.Distinct().Count());
            foreach (var s in GameData.Skills)
            {
                foreach (string p in s.Prereq) Assert.IsNotNull(GameData.Skill(p), s.Id);
                if (s.Id != "root") Assert.IsNotEmpty(s.Prereq, s.Id);
                Assert.That(s.RequiresLayer, Is.InRange(0, 11), s.Id);
            }
        }

        [Test]
        public void Formulas_MatchTheBrief()
        {
            Assert.AreEqual(10, Balance.BlockHp(0), 1e-9);
            Assert.AreEqual(10 * Math.Pow(1.03, 50), Balance.BlockHp(50), 1e-9);
            Assert.AreEqual(2 * Math.Pow(1.028, 10), Balance.OreValue(10, 2), 1e-9);
            EnsureData();
            Assert.AreEqual(10 * Math.Pow(1.045, 3) * 0.9 * 9, Balance.DrillPrice(10, 3, 0.1, 2), 1e-6);
        }

        [Test]
        public void Depth_MapsRowsOntoRealMetres()
        {
            EnsureData();
            Assert.AreEqual(0, GameData.MetresAt(0), 1e-9);
            Assert.AreEqual(50, GameData.MetresAt(50), 1e-9);
            Assert.AreEqual(100, GameData.MetresAt(100), 1e-9);
            Assert.AreEqual(300, GameData.MetresAt(175), 1e-9, "halfway through sandstone");
            Assert.AreEqual(6371000, GameData.MetresAt(GameData.TotalRows - 1), 30000);
            Assert.AreEqual("50 米", GameData.FormatDepth(50));
            Assert.AreEqual("750 米", GameData.FormatDepth(280));
            Assert.AreEqual("2.3 千米", GameData.FormatDepth(450));
        }

        [TestCase(0, "0")]
        [TestCase(0.5, "0.5")]
        [TestCase(1000, "1.00K")]
        [TestCase(999999, "1.00M")]
        [TestCase(1e15, "1.00e15")]
        public void Numbers_AreShortened(double value, string expected)
        {
            Assert.AreEqual(expected, NumberFormat.Short(value));
        }

        [Test]
        public void EveryLayer_GeneratesItsOwnOres()
        {
            EnsureData();
            foreach (var layer in GameData.Layers)
            {
                var found = new System.Collections.Generic.HashSet<int>();
                for (int row = layer.RowStart + 10; row < layer.RowEnd - 10; row++)
                    for (int col = 0; col < Balance.Columns; col++)
                    {
                        var b = new Block();
                        WorldGen.Fill(b, 9, row, col, 0.02, 0.005);
                        if (b.Type == BlockType.Ore) found.Add(b.Ore);
                    }
                Assert.AreEqual(layer.Ores.Count, found.Count, layer.Name);
            }
        }

        [Test]
        public void Layers_MixNearTheirBoundary()
        {
            EnsureData();
            Assert.AreEqual(0, WorldGen.LayerAt(3, 90, 5));
            int mixed = 0;
            for (int col = 0; col < Balance.Columns; col++) mixed += WorldGen.LayerAt(3, 99, col);
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
        }

        [Test]
        public void TiltedDrill_DigsWhereItsTipPoints()
        {
            var sim = NewSim();
            var body = sim.Lead.Body;
            body.Place(20, 10.5 - DrillBody.HalfHeight);
            sim.Carve(11, 20);
            sim.Carve(10, 20);
            body.Angle = -80; // the tip reaches into the left neighbour
            Assert.IsTrue(sim.FindTarget(body, out _, out int col));
            Assert.Less(col, 20);
        }

        [Test]
        public void BuyPrice_FollowsTheFormula()
        {
            var sim = NewSim();
            sim.State.coins = 1e9;
            double first = sim.DrillPrice(0);
            Assert.AreEqual(10, first, 1e-9);
            Assert.IsTrue(sim.BuyDrill(0));
            Assert.AreEqual(10 * 1.045, sim.DrillPrice(0), 1e-9);
            Assert.AreEqual(2, sim.Units.Count);
        }

        [Test]
        public void Families_WaitForTheirLayer()
        {
            var sim = NewSim();
            sim.State.coins = 1e12;
            Assert.IsFalse(sim.BuyDrill(1), "swift needs layer 2");
            sim.State.maxDepthEver = 100;
            Assert.IsTrue(sim.BuyDrill(1));
        }

        [Test]
        public void Merging_TwoMakeOneOfTheNextMaterialWithTripleDamage()
        {
            var sim = NewSim();
            int wood = DrillCode.Make(0, 0), stone = DrillCode.Make(0, 1);
            sim.State.drills.Add(wood);
            Assert.IsTrue(sim.Merge(wood));
            Assert.AreEqual(1, sim.State.drills.Count);
            Assert.AreEqual(stone, sim.State.drills[0]);
            Assert.AreEqual(GameSim.KindDamage(wood) * 3, GameSim.KindDamage(stone), 1e-9);
            Assert.IsFalse(sim.Merge(stone), "needs two");
        }

        [Test]
        public void TopMaterial_CannotMerge()
        {
            var sim = NewSim();
            int top = DrillCode.Make(0, GameData.Tiers - 1);
            sim.State.drills.Add(top);
            sim.State.drills.Add(top);
            Assert.IsFalse(sim.CanMerge(top));
        }

        [Test]
        public void AutoMerge_SkillMergesInTheHangar()
        {
            var sim = NewSim();
            var skill = GameData.Skills.First(s => s.Effect == SkillEffect.auto_merge);
            sim.GrantSkill(skill.Id, 1);
            for (int i = 0; i < 3; i++) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.Tick(1.1);
            Assert.Less(sim.State.drills.Count, 4);
        }

        [Test]
        public void BuyTier_StartsHigherAndCostsTriple()
        {
            var sim = NewSim();
            var skill = GameData.Skills.First(s => s.Effect == SkillEffect.buy_tier);
            sim.State.coins = 1e6;
            double before = sim.DrillPrice(0);
            sim.GrantSkill(skill.Id, 1);
            Assert.AreEqual(before * 3, sim.DrillPrice(0), 1e-9);
            sim.BuyDrill(0);
            Assert.Contains(DrillCode.Make(0, 1), sim.State.drills);
        }

        [Test]
        public void OwnedDrills_HaveNoCap_AndExtrasAddTheirPower()
        {
            var sim = NewSim();
            for (int i = 0; i < 5000; i++) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.Merge(DrillCode.Make(0, 0));
            Assert.LessOrEqual(sim.Units.Count, sim.SimulationLimit);
            Assert.Greater(sim.State.drills.Count, 4990);
            Assert.Greater(sim.SwarmMultiplier, 10);
        }

        [Test]
        public void TwoHundredDrills_DigWithoutGettingStuckOrSharingCells()
        {
            var sim = NewSim();
            for (int i = 0; i < 200; i++) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.Merge(DrillCode.Make(0, 0));
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 600; i++) sim.Tick(1.0 / 60);
            clock.Stop();
            Assert.Greater(sim.State.depth, 3);
            Assert.Less(clock.Elapsed.TotalMilliseconds / 600, 8, "a sim tick must leave room for a 60 FPS frame");
            var settled = sim.Units.Where(u => u.Body.VelocityY == 0).Select(u => (u.Body.BodyRow, u.Body.Column)).ToList();
            Assert.Greater(settled.Distinct().Count(), settled.Count * 0.95);
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
        public void Skills_NeedLayerThenPrereq()
        {
            var sim = FreshSim();
            sim.State.coins = 1e30;
            Assert.IsFalse(sim.Buy("drill_01"), "root first");
            Assert.IsTrue(sim.Buy("root"));
            Assert.IsTrue(sim.Buy("drill_01"));
            Assert.AreEqual(GameData.Skill("root").CostAt(0), 15, 1e-9);

            var deep = GameData.Skills.Last(s => s.Branch == "drill");
            Assert.IsFalse(sim.Buy(deep.Id), "locked by layer");
        }

        [Test]
        public void SkillEffects_ChangeTheGame()
        {
            var sim = NewSim();
            int code = DrillCode.Make(0, 0);
            double damage = sim.DamageOf(code);
            sim.GrantSkill("drill_01", 5);
            Assert.AreEqual(damage * 1.5, sim.DamageOf(code), 1e-9);

            double speed = sim.HitsPerSecond(code);
            sim.GrantSkill("drill_02", 5);
            Assert.AreEqual(speed * 1.2, sim.HitsPerSecond(code), 1e-9);
        }

        [Test]
        public void LayerSkill_OnlyCountsInItsLayer()
        {
            var sim = NewSim();
            int code = DrillCode.Make(0, 0);
            var sand = GameData.Skills.First(s => s.Effect == SkillEffect.layer_pct && s.ForLayer == 1);
            double before = sim.DamageOf(code);
            sim.GrantSkill(sand.Id, 2);
            Assert.AreEqual(before, sim.DamageOf(code), 1e-9, "still in the topsoil");
            sim.Teleport(150, Balance.StartColumn);
            Assert.AreEqual(before * 2, sim.DamageOf(code), 1e-9);
        }

        [Test]
        public void Combo_GrowsAndIsCapped()
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
            Assert.IsTrue(sim.StartFrenzy());
            for (int i = 0; i < 110; i++) sim.Tick(0.1);
            Assert.IsFalse(sim.InFrenzy);
        }

        [Test]
        public void Tnt_ChainsUpToTheLimit_ThenFreelyWithTheSkill()
        {
            var sim = NewSim();
            for (int col = 0; col < 8; col++) MakeBlock(sim, 10, col, BlockType.Tnt);
            sim.Click(10, 0);
            Assert.AreEqual(Balance.BaseChainLimit, sim.Events.FindAll(e => e.Kind == SimEventKind.Explosion).Count);

            sim = NewSim();
            sim.GrantSkill(GameData.Skills.First(s => s.Effect == SkillEffect.chain).Id, 1);
            for (int col = 0; col < 8; col++) MakeBlock(sim, 10, col, BlockType.Tnt);
            sim.Click(10, 0);
            Assert.AreEqual(8, sim.Events.FindAll(e => e.Kind == SimEventKind.Explosion).Count);
        }

        private static GameSim SimAtBoss(int layer)
        {
            var sim = NewSim();
            sim.State.bossesDefeated = layer;
            sim.Teleport(GameData.Layers[layer].RowEnd, Balance.StartColumn);
            sim.Tick(0.1);
            return sim;
        }

        [Test]
        public void EveryLayer_HasAGuardianThatBlocksTheWay()
        {
            foreach (var layer in GameData.Layers)
            {
                if (layer.Index >= GameData.Layers.Count) break;
                var sim = SimAtBoss(layer.Index);
                Assert.IsTrue(sim.BossActive, layer.BossName);
                Assert.AreEqual(BlockType.BossRoot, sim.Row(layer.RowEnd)[3].Type);
            }
        }

        [Test]
        public void Boss_HpComesFromTheData()
        {
            var sim = SimAtBoss(3);
            var layer = GameData.Layers[3];
            Assert.AreEqual(Balance.BlockHp(layer.RowEnd) * layer.BossHpMult, sim.BossMaxHp, 1);
        }

        [Test]
        public void Boss_DefeatOpensTheWayAndAdvances()
        {
            var sim = SimAtBoss(0);
            for (int i = 0; i < 6; i++) sim.State.drills.Add(DrillCode.Make(0, 8));
            sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.Merge(DrillCode.Make(0, 0));
            for (int i = 0; i < 3000 && sim.State.bossesDefeated == 0; i++) sim.Tick(0.05);
            Assert.AreEqual(1, sim.State.bossesDefeated);
            for (int i = 0; i < 100; i++) sim.Tick(0.05);
            Assert.Greater(sim.State.depth, 100);
        }

        [Test]
        public void Armour_HalvesDamageUntilGone()
        {
            var sim = SimAtBoss(1);
            Assert.Greater(sim.BossArmor, 0);
            sim.Events.Clear();
            sim.Click(GameData.Layers[1].RowEnd, 0);
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
            s.drills.Add(DrillCode.Make(0, 3));
            s.AddRare("rare_1_1");
            sim.GrantSkill("root", 3);

            Assert.IsTrue(sim.Rebirth());
            Assert.AreEqual(48, s.shards);
            Assert.AreEqual(Balance.StartGold, s.coins, 1e-9);
            Assert.AreEqual(0, s.depth);
            Assert.AreEqual(1, s.drills.Count);
            Assert.AreEqual(1, sim.Units.Count);
            Assert.AreEqual(0, s.LevelOf("root"));
            Assert.AreEqual(400, s.maxDepthEver, "records stay");
            Assert.AreEqual(1, s.RareCountOf("rare_1_1"), "rare finds stay");
            Assert.AreEqual(1, s.rebirths);
        }

        [Test]
        public void Sand_FallsCellByCellAndStops()
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

        private static int MidRow(int layer) => (GameData.Layers[layer].RowStart + GameData.Layers[layer].RowEnd) / 2;

        private static GameSim SimInLayer(int layer)
        {
            var sim = NewSim();
            sim.State.bossesDefeated = layer;
            sim.Teleport(MidRow(layer), Balance.StartColumn);
            return sim;
        }

        private static int CountType(BlockType type, int layer)
        {
            int n = 0;
            for (int row = GameData.Layers[layer].RowStart + 10; row < GameData.Layers[layer].RowEnd - 10; row++)
                for (int col = 0; col < Balance.Columns; col++)
                {
                    var b = new Block();
                    WorldGen.Fill(b, 4, row, col, 0.02, 0.005);
                    if (b.Type == type) n++;
                }
            return n;
        }

        [Test]
        public void SpecialBlocks_AppearOnlyInTheirLayers()
        {
            EnsureData();
            Assert.Greater(CountType(BlockType.Water, Balance.WaterLayer), 100);
            Assert.AreEqual(0, CountType(BlockType.Water, Balance.CrystalLayer));
            Assert.Greater(CountType(BlockType.Magma, Balance.MantleLayer), 100);
            Assert.Greater(CountType(BlockType.SuperHard, Balance.GraniteLayer), 100);
            Assert.AreEqual(0, CountType(BlockType.SuperHard, Balance.MantleLayer));
        }

        [Test]
        public void Crystal_ReflectsIntoItsNeighbours()
        {
            var sim = SimInLayer(Balance.CrystalLayer);
            int row = MidRow(Balance.CrystalLayer) + 6;
            MakeBlock(sim, row, 5, BlockType.Ore, 10);
            sim.Row(row)[5].OreValue = 16;
            sim.Row(row)[5].Hp = 0.1;
            double before = sim.Row(row + 2)[5].Hp;
            sim.Click(row, 5);
            Assert.Less(sim.Row(row + 2)[5].Hp, before);
        }

        [Test]
        public void Water_PushesNearbyDrillsAway()
        {
            var sim = SimInLayer(Balance.WaterLayer);
            int row = MidRow(Balance.WaterLayer);
            int col = Balance.StartColumn - 2;
            sim.Row(row)[col].Type = BlockType.Water;
            sim.Row(row)[col].Hp = 0.01;
            double before = sim.Lead.Body.VelocityX;
            sim.Click(row, col);
            Assert.Greater(sim.Lead.Body.VelocityX, before);
        }

        [Test]
        public void Ruins_HaveHiddenRoomsWithChests()
        {
            EnsureData();
            var layer = GameData.Layers[Balance.RuinsLayer];
            int rooms = 0, chests = 0;
            for (int row = layer.RowStart + 10; row < layer.RowEnd - 10; row++)
                for (int col = 0; col < Balance.Columns; col++)
                    if (WorldGen.RoomAt(6, row, col, out bool treasure))
                    {
                        rooms++;
                        if (treasure) chests++;
                        var b = new Block();
                        WorldGen.Fill(b, 6, row, col, 0.02, 0.005);
                        if (treasure) Assert.AreEqual(BlockType.Chest, b.Type);
                        else Assert.IsFalse(b.Alive);
                    }
            Assert.Greater(chests, 3);
            Assert.Greater(rooms, chests * 10);
        }

        [Test]
        public void SuperHard_OnlyGivesWayToHeavyDrills()
        {
            var sim = SimInLayer(Balance.GraniteLayer);
            int row = MidRow(Balance.GraniteLayer) + 6;
            MakeBlock(sim, row, 3, BlockType.SuperHard, 1e12);
            sim.Click(row, 3);
            var weak = sim.Events.Find(e => e.Kind == SimEventKind.BlockHit && e.Block == BlockType.SuperHard);
            double crit = weak.Crit ? Balance.CritMultiplier : 1;
            Assert.AreEqual(sim.ClickDamage * crit * Balance.SuperHardWeakDamage, weak.Amount, 1e-3);
        }

        [Test]
        public void Moho_OverheatsAndFrenzyCools()
        {
            var sim = SimInLayer(Balance.MohoLayer);
            int code = DrillCode.Make(0, 0);
            double cool = sim.HitsPerSecond(code);
            for (int i = 0; i < 105 && !sim.Overheating; i++) sim.Tick(0.1);
            Assert.IsTrue(sim.Overheating);
            Assert.AreEqual(cool * Balance.OverheatSpeed, sim.HitsPerSecond(code), 1e-9);
        }

        [Test]
        public void Magma_ErruptsInAChain()
        {
            var sim = SimInLayer(Balance.MantleLayer);
            int row = MidRow(Balance.MantleLayer) + 8;
            for (int col = 0; col < 6; col++) MakeBlock(sim, row, col, BlockType.Magma);
            sim.Events.Clear();
            sim.Click(row, 0);
            Assert.AreEqual(6, sim.Events.FindAll(e => e.Kind == SimEventKind.Explosion).Count);
        }

        [Test]
        public void PhaseLayer_AlternatesItsRockEveryThirtyRows()
        {
            EnsureData();
            int Hard(int row)
            {
                int n = 0;
                for (int r = row; r < row + 10; r++)
                    for (int col = 0; col < Balance.Columns; col++)
                    {
                        var b = new Block();
                        WorldGen.Fill(b, 2, r, col, 0.02, 0.005);
                        if (b.Type == BlockType.HardRock) n++;
                    }
                return n;
            }
            var layer = GameData.Layers[Balance.PhaseLayer];
            int band = layer.RowStart / Balance.PhaseBandRows + 2;
            int even = band % 2 == 0 ? band : band + 1;
            Assert.Greater(Hard(even * Balance.PhaseBandRows + 5), Hard((even + 1) * Balance.PhaseBandRows + 5) * 2);
        }

        [Test]
        public void Convection_SlidesTheRowsAhead()
        {
            var sim = SimInLayer(Balance.ConvectionLayer);
            int row = MidRow(Balance.ConvectionLayer) + 4;
            var marked = sim.Row(row)[10];
            for (int i = 0; i < 22; i++) sim.Tick(0.1);
            Assert.AreNotSame(marked, sim.Row(row)[10]);
        }

        [Test]
        public void Liquid_FlowsBackIntoDugCells()
        {
            var sim = SimInLayer(Balance.LiquidLayer);
            int row = MidRow(Balance.LiquidLayer) + 8;
            MakeBlock(sim, row, 3, BlockType.Dirt);
            sim.Click(row, 3);
            Assert.IsFalse(sim.Row(row)[3].Alive);
            for (int i = 0; i < 70; i++) sim.Tick(0.1);
            Assert.IsTrue(sim.Row(row)[3].Alive);
        }

        [Test]
        public void Core_OnlyTakesFullDamageInFrenzy()
        {
            var sim = SimInLayer(Balance.CoreLayer);
            int row = MidRow(Balance.CoreLayer) + 6;
            MakeBlock(sim, row, 3, BlockType.Dirt, 1e12);
            sim.Events.Clear();
            sim.Click(row, 3);
            var hit = sim.Events.Find(e => e.Kind == SimEventKind.BlockHit);
            double crit = hit.Crit ? Balance.CritMultiplier : 1;
            Assert.AreEqual(sim.ClickDamage * crit * Balance.HeatDamageRatio, hit.Amount, 1e-3);
        }

        // An open floor at row 13, or with walls either side of column 20 when asked for a well.
        private static GameSim Arena(int drills, bool well)
        {
            var sim = NewSim();
            for (int r = 0; r < 13; r++)
                for (int c = 0; c < Balance.Columns; c++)
                {
                    sim.Row(r)[c].Hp = 0;
                    if (well && c != 20) MakeBlock(sim, r, c, BlockType.Dirt, 1e15);
                }
            for (int r = 13; r < 16; r++)
                for (int c = 0; c < Balance.Columns; c++)
                    MakeBlock(sim, r, c, BlockType.Dirt, 1e15);
            for (int i = 1; i < drills; i++) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.SyncDrills();
            return sim;
        }

        private static void Stack(GameSim sim)
        {
            for (int i = 0; i < sim.Units.Count; i++)
                sim.Units[i].Body.Place(20, 12.5 - DrillBody.HalfHeight - i);
            sim.Units[0].HitClock = 0;
        }

        [Test]
        public void DrillOnADrill_SlidesOffAndDigs()
        {
            var sim = Arena(2, false);
            Stack(sim);
            for (int i = 0; i < 40; i++) sim.Tick(0.05);
            var a = sim.Units[0].Body;
            var b = sim.Units[1].Body;
            Assert.AreNotEqual(a.Column, b.Column, "no longer stacked");
            Assert.IsTrue(sim.Units.TrueForAll(u => !u.Boosting));
            Assert.IsTrue(sim.IsSolid(b.FeetRow, b.Column), "standing on rock");
        }

        [Test]
        public void NoRoom_TopDrillBoostsTheOneBelow()
        {
            var sim = Arena(2, true);
            Stack(sim);
            sim.Tick(0.05);
            Assert.IsTrue(sim.Units.Exists(u => u.Boosting));

            var floor = sim.Row(13)[20];
            double before = floor.Hp;
            sim.Events.Clear();
            for (int i = 0; i < 20; i++) sim.Tick(0.05);
            double perSwing = sim.DamageOf(DrillCode.Make(0, 0));
            var hits = sim.Events.FindAll(e => e.Kind == SimEventKind.BlockHit);
            Assert.GreaterOrEqual(hits.Count, 5, "both drills swing at 3 a second");
            Assert.IsTrue(hits.TrueForAll(e => e.Row == 13 && e.Col == 20), "all on the block under the bottom drill");
            Assert.Less(floor.Hp, before - perSwing * 4);
        }

        [Test]
        public void Stacking_LosesNoDamage()
        {
            double Dealt(int drills)
            {
                var sim = Arena(drills, true);
                Stack(sim);
                sim.Tick(0.05);
                foreach (var u in sim.Units) u.HitClock = 0;
                sim.Events.Clear();
                for (int i = 0; i < 40; i++) sim.Tick(0.05);
                return sim.Events.FindAll(e => e.Kind == SimEventKind.BlockHit).Count;
            }
            Assert.AreEqual(Dealt(1) * 3, Dealt(3), 3, "three stacked drills swing three times as often");
        }

        [Test]
        public void NewDrills_SpreadAcrossColumns()
        {
            var sim = NewSim();
            for (int i = 0; i < 12; i++) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.SyncDrills();
            int columns = sim.Units.Select(u => u.Body.Column).Distinct().Count();
            Assert.Greater(columns, 9);
        }

        [Test]
        public void Rares_DropFromChestsAndDuplicatesStack()
        {
            var sim = NewSim();
            int code = DrillCode.Make(0, 0);
            double before = sim.DamageOf(code);
            sim.State.AddRare("rare_1_1"); // rank 0: +1% damage
            var chest = sim.Row(10)[0];
            chest.Type = BlockType.Chest;
            chest.Hp = 0.1;
            sim.GrantSkill("root", 1); // recomputes bonuses
            Assert.AreEqual(before * 1.01, sim.DamageOf(code), 1e-9);
            sim.State.AddRare("rare_1_1");
            sim.GrantSkill("root", 1);
            Assert.AreEqual(before * 1.015, sim.DamageOf(code), 1e-9);
        }

        [Test]
        public void Rares_ComeOutOfChests()
        {
            var sim = NewSim();
            sim.GrantSkill(GameData.Skills.First(s => s.Effect == SkillEffect.rare_pct).Id, 10);
            for (int i = 0; i < 3000; i++)
            {
                MakeBlock(sim, 8, 0, BlockType.Chest);
                sim.Click(8, 0);
                sim.Events.Clear();
            }
            Assert.Greater(sim.State.rares.Count, 0);
            Assert.IsTrue(sim.State.rares.All(r => r.id.StartsWith("rare_1_", StringComparison.Ordinal)), "this layer's items");
        }

        [Test]
        public void Achievements_UnlockFromProgressOnce()
        {
            var sim = NewSim();
            for (int i = 0; i < 12; i++) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.Tick(1.1);
            Assert.Contains("drills_10", sim.State.achievements);
            Assert.Contains("reach_1", sim.State.achievements);
            int count = sim.State.achievements.Count;
            sim.Tick(1.1);
            Assert.AreEqual(count, sim.State.achievements.Count);
        }

        [Test]
        public void EveryAchievement_HasACheckableCondition()
        {
            var sim = NewSim();
            foreach (var a in GameData.Achievements) Assert.DoesNotThrow(() => sim.AchievementMet(a), a.Id);
        }

        [Test]
        public void Offline_UsesRateAndHourSkills()
        {
            Assert.AreEqual(50, Balance.OfflineEarnings(10, 10), 1e-9);
            Assert.AreEqual(10 * 7200 * 0.5, Balance.OfflineEarnings(10, 999999), 1e-9);
            Assert.AreEqual(10 * 10800 * 0.6, Balance.OfflineEarnings(10, 999999, 0.1, 1), 1e-9);
        }

        [Test]
        public void OldSave_RestartsTheWorldButKeepsCoins()
        {
            EnsureData();
            var state = new GameState { version = 3, coins = 5, depth = 80 };
            state.SetLevel("drill_damage", 5);
            state.Upgrade();
            Assert.IsEmpty(state.skills);
            Assert.AreEqual(0, state.depth);
            Assert.AreEqual(5, state.coins, 1e-9);
            Assert.AreEqual(GameState.CurrentVersion, state.version);
        }

        [Test]
        public void ReloadedRun_KeepsTheDrills()
        {
            var sim = NewSim();
            for (int i = 0; i < 5; i++) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.Merge(DrillCode.Make(0, 0));
            while (sim.State.depth < 5) sim.Tick(0.1);
            var reloaded = new GameSim(sim.State);
            Assert.AreEqual(sim.State.drills.Count, reloaded.Units.Count);
        }
    }
}
