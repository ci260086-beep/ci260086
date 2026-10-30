using System;
using System.Linq;
using DeepDrill.Core;
using NUnit.Framework;

namespace DeepDrill.Tests
{
    /// <summary>Rules that do not depend on the shape of the ground.</summary>
    public class RuleTests
    {
        private static void EnsureData() => CoreTests.EnsureData();
        private static GameSim NewSim(int seed = 42) => CoreTests.NewSim(seed);
        private static GameSim FreshSim(int seed = 42) => CoreTests.FreshSim(seed);

        // Walks up from wherever the tests run until it finds the project's data folder.
        // Most rules are tested with auto drilling on; FreshSim is a game as the player first meets it.
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

        // Stands on column 20 of a floor at row 9; the neighbouring columns are floor only if asked.
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
        public void Drill_DigsDownAndEarns()
        {
            var sim = NewSim();
            for (int i = 0; i < 600; i++) sim.Tick(0.1);
            Assert.Greater(sim.State.depth, 0);
            Assert.Greater(sim.State.coins, 0);
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

        // An open floor at row 13, or with walls either side of column 20 when asked for a well.
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
