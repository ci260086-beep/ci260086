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

        #region step 3: rebirth milestones and automation

        private static GameSim Rebirthed(int times)
        {
            var sim = CoreTests.NewSim();
            sim.State.rebirths = times - 1;
            sim.State.bossesDefeated = 1;
            sim.State.maxDepth = 400;
            sim.State.totalIncome = 1e6;
            Assert.IsTrue(sim.Rebirth());
            return sim;
        }

        [Test]
        public void RebirthOne_StartsWithMoreCoins()
        {
            Assert.AreEqual(Balance.StartGold + 2000, Rebirthed(1).State.coins, 1e-9);
        }

        [Test]
        public void RebirthThree_KeepsAutoDrillAndOpensRules()
        {
            var sim = Rebirthed(3);
            Assert.IsTrue(sim.AutoDrillUnlocked);
            Assert.IsTrue(sim.AutomationUnlocked);
            Assert.IsFalse(Rebirthed(2).AutomationUnlocked);
        }

        [Test]
        public void RebirthFive_BringsDrills()
        {
            var sim = Rebirthed(5);
            Assert.AreEqual(9, sim.State.drills.Count);
            Assert.AreEqual(8, sim.State.drills.Count(c => c == DrillCode.Make(0, 2)));
        }

        [Test]
        public void RebirthTen_BringsTheAutoHangar()
        {
            var sim = Rebirthed(10);
            Assert.Greater(sim.Effect(SkillEffect.auto_merge), 0);
            Assert.Greater(sim.Effect(SkillEffect.auto_buy), 0);
        }

        [Test]
        public void RebirthTwentyFive_StartsTheFirstRingAtFive()
        {
            Assert.AreEqual(5, Rebirthed(25).State.LevelOf("drill_01"));
        }

        [Test]
        public void Automation_BuysNothingUntilUnlocked()
        {
            var sim = CoreTests.NewSim();
            sim.State.autoSkills = true;
            sim.State.coins = 1000;
            sim.Tick(1.1);
            Assert.AreEqual(1, sim.State.skills.Count);
        }

        [Test]
        public void Automation_KeepsTheReserve()
        {
            var sim = Rebirthed(3);
            sim.State.autoDrills = true;
            sim.State.autoReservePct = 50;
            sim.State.coins = 1000;
            sim.Tick(1.1);
            Assert.Greater(sim.State.drills.Count, 1);
            Assert.GreaterOrEqual(sim.State.coins, 500);
        }

        [Test]
        public void Automation_BuysSkillsWhenAsked()
        {
            var sim = Rebirthed(3);
            sim.State.autoSkills = true;
            sim.State.coins = 1000;
            sim.Tick(1.1);
            Assert.Greater(sim.State.skills.Count, 1);
        }

        [Test]
        public void Automation_RebirthsAtTheChosenDepth()
        {
            var sim = Rebirthed(3);
            sim.State.autoRebirthRow = 100;
            sim.State.bossesDefeated = 1;
            sim.State.maxDepth = 120;
            sim.State.totalIncome = 1e6;
            sim.Tick(1.1);
            Assert.AreEqual(4, sim.State.rebirths);
        }

        #endregion

        #region step 4: schools

        private static int School(string id) => GameData.Schools.FindIndex(s => s.Id == id);

        [Test]
        public void School_NeedsARebirthFirst()
        {
            var sim = CoreTests.NewSim();
            Assert.IsFalse(sim.ChooseSchool(0));
        }

        [Test]
        public void School_IsPickedOncePerRun()
        {
            var sim = Rebirthed(1);
            Assert.IsTrue(sim.ChooseSchool(School("blast")));
            Assert.IsFalse(sim.ChooseSchool(School("rich")));
        }

        [Test]
        public void School_AddsItsBonuses()
        {
            var sim = Rebirthed(1);
            double before = sim.Effect(SkillEffect.blast_dmg);
            sim.ChooseSchool(School("blast"));
            Assert.AreEqual(before + 200, sim.Effect(SkillEffect.blast_dmg), 1e-9);
        }

        [Test]
        public void School_DamageStacksAsItsOwnFactor()
        {
            var sim = Rebirthed(1);
            int code = DrillCode.Make(0, 0);
            double before = sim.DamageOf(code);
            sim.ChooseSchool(School("craft"));
            Assert.AreEqual(before * 2, sim.DamageOf(code), 1e-9);
        }

        [Test]
        public void School_MakesItsBranchCheaper()
        {
            var sim = Rebirthed(1);
            var drill = GameData.Skills.First(s => s.Branch == "drill");
            var other = GameData.Skills.First(s => s.Branch == "wealth");
            double full = sim.SkillCost(drill), otherFull = sim.SkillCost(other);
            sim.ChooseSchool(School("craft"));
            Assert.AreEqual(full * 0.7, sim.SkillCost(drill), 1e-9);
            Assert.AreEqual(otherFull, sim.SkillCost(other), 1e-9);
        }

        [Test]
        public void School_EndsWithTheRun()
        {
            var sim = Rebirthed(1);
            sim.ChooseSchool(0);
            sim.State.bossesDefeated = 1;
            sim.State.maxDepth = 400;
            sim.State.totalIncome = 1e6;
            Assert.IsTrue(sim.Rebirth());
            Assert.IsNull(sim.CurrentSchool);
        }

        [Test]
        public void School_ClearingPaysPermanently()
        {
            var sim = Rebirthed(1);
            int code = DrillCode.Make(0, 0);
            double before = sim.DamageOf(code);
            sim.ChooseSchool(School("craft"));
            sim.State.maxDepth = GameData.TotalRows;
            sim.Tick(1.1);
            Assert.IsTrue(sim.SchoolCleared(School("craft")));

            sim.State.bossesDefeated = 1;
            sim.State.totalIncome = 1e6;
            Assert.IsTrue(sim.Rebirth());
            Assert.Greater(sim.DamageOf(code), before);
        }

        #endregion

        #region step 5: factory and crystals

        [Test]
        public void Crystal_GrowsWithTime()
        {
            var sim = CoreTests.NewSim();
            sim.Tick(GameData.CrystalSeconds / 2);
            Assert.AreEqual(0, sim.Crystals);
            Assert.AreEqual(0.5, sim.CrystalProgress, 1e-3);
        }

        [Test]
        public void Crystal_GrowsWhileAway()
        {
            var sim = CoreTests.NewSim();
            sim.State.lastSaveUnixSeconds = 1000;
            sim.ApplyOffline(1000 + (long)GameData.CrystalSeconds * 3);
            Assert.AreEqual(3, sim.Crystals);
        }

        [Test]
        public void Crystal_StopsAtTheCap()
        {
            var sim = CoreTests.NewSim();
            sim.State.lastSaveUnixSeconds = 1000;
            sim.ApplyOffline(1000 + (long)(GameData.CrystalSeconds * (GameData.CrystalCap + 5)));
            Assert.AreEqual(GameData.CrystalCap, sim.Crystals);
            Assert.IsTrue(sim.CrystalsFull);
        }

        [Test]
        public void Factory_SpendsOreAndAddsItsBonus()
        {
            var sim = CoreTests.NewSim();
            int forge = GameData.Factory.FindIndex(f => f.Id == "forge");
            int code = DrillCode.Make(0, 0);
            double before = sim.DamageOf(code);
            sim.State.ores = 100;
            Assert.IsTrue(sim.UpgradeFactory(forge));
            Assert.AreEqual(60, sim.State.ores, 1e-9);
            Assert.AreEqual(before * 1.1, sim.DamageOf(code), 1e-9);
        }

        [Test]
        public void Factory_CannotBeBoughtWithoutOre()
        {
            var sim = CoreTests.NewSim();
            Assert.IsFalse(sim.UpgradeFactory(0));
            Assert.AreEqual(0, sim.FactoryLevel(0));
        }

        [Test]
        public void Factory_TakesACrystalEveryFewLevels()
        {
            var sim = CoreTests.NewSim();
            sim.State.ores = 1e6;
            for (int i = 0; i < GameData.FactoryCrystalEvery - 1; i++) Assert.IsTrue(sim.UpgradeFactory(0));
            Assert.IsFalse(sim.UpgradeFactory(0), "the fifth level needs a crystal");
            sim.State.crystals = 1;
            Assert.IsTrue(sim.UpgradeFactory(0));
            Assert.AreEqual(0, sim.Crystals);
        }

        [Test]
        public void Factory_OutlivesARebirth()
        {
            var sim = CoreTests.NewSim();
            sim.State.ores = 100;
            sim.UpgradeFactory(0);
            sim.State.bossesDefeated = 1;
            sim.State.maxDepth = 400;
            sim.State.totalIncome = 1e6;
            Assert.IsTrue(sim.Rebirth());
            Assert.AreEqual(1, sim.FactoryLevel(0));
        }

        #endregion

        #region step 6: research and expeditions

        [Test]
        public void Research_SpendsCrystalsAndFinishesInTime()
        {
            var sim = CoreTests.NewSim();
            int code = DrillCode.Make(0, 0);
            double before = sim.DamageOf(code);
            sim.State.crystals = 1;
            Assert.IsTrue(sim.StartResearch(0));
            Assert.AreEqual(0, sim.Crystals);
            sim.Tick(GameData.Research[0].Seconds - 1);
            Assert.IsFalse(sim.ResearchDone(0));
            sim.Tick(2);
            Assert.IsTrue(sim.ResearchDone(0));
            Assert.Greater(sim.DamageOf(code), before);
        }

        [Test]
        public void Research_RunsOneAtATime()
        {
            var sim = CoreTests.NewSim();
            sim.State.crystals = 5;
            Assert.IsTrue(sim.StartResearch(0));
            Assert.IsFalse(sim.StartResearch(1));
        }

        [Test]
        public void Research_CannotRepeat()
        {
            var sim = CoreTests.NewSim();
            sim.State.crystals = 5;
            sim.StartResearch(0);
            sim.Tick(GameData.Research[0].Seconds + 1);
            Assert.IsFalse(sim.StartResearch(0));
        }

        [Test]
        public void Research_CrystalCutsTheWait()
        {
            var sim = CoreTests.NewSim();
            sim.State.crystals = 2;
            sim.StartResearch(0);
            Assert.IsTrue(sim.RushResearch());
            Assert.IsTrue(sim.ResearchDone(0), "half an hour of research is under one rush");
        }

        [Test]
        public void Research_RunsWhileAway()
        {
            var sim = CoreTests.NewSim();
            sim.State.crystals = 1;
            sim.StartResearch(0);
            sim.State.lastSaveUnixSeconds = 1000;
            sim.ApplyOffline(1000 + (long)GameData.Research[0].Seconds + 1);
            Assert.IsTrue(sim.ResearchDone(0));
        }

        [Test]
        public void Expedition_NeedsAnIdleExplorer()
        {
            var sim = CoreTests.NewSim();
            Assert.IsFalse(sim.SendExpedition(0));
            sim.State.explorers = 1;
            Assert.IsTrue(sim.SendExpedition(0));
            Assert.AreEqual(0, sim.ExplorersIdle);
            Assert.IsFalse(sim.SendExpedition(0));
        }

        [Test]
        public void Expedition_ComesBackAndPaysOnSuccess()
        {
            var sim = CoreTests.NewSim();
            sim.State.explorers = 1;
            int paid = 0;
            for (int i = 0; i < 40; i++)
            {
                sim.SendExpedition(0);
                int before = sim.State.shards;
                sim.State.lastSaveUnixSeconds = 1000;
                sim.ApplyOffline(1000 + (long)GameData.Expeditions[0].Seconds + 1);
                Assert.AreEqual(1, sim.ExplorersIdle, "the explorer always comes home");
                if (sim.State.shards > before) paid++;
            }
            Assert.Greater(paid, 25);
            Assert.Less(paid, 40, "ten percent of trips fail");
        }

        [Test]
        public void Expedition_RiskierTripsPayMore()
        {
            CoreTests.EnsureData();
            for (int i = 1; i < GameData.Expeditions.Count; i++)
            {
                Assert.Less(GameData.Expeditions[i].Success, GameData.Expeditions[i - 1].Success);
                Assert.Greater(GameData.Expeditions[i].Shards, GameData.Expeditions[i - 1].Shards);
            }
        }

        #endregion
    }
}
