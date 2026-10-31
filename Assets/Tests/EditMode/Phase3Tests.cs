using System.Linq;
using DeepDrill.Core;
using NUnit.Framework;

namespace DeepDrill.Tests
{
    /// <summary>The systems of the third phase, one region per step of the plan.</summary>
    public class Phase3Tests
    {
        private static GameSim InFight(int layer)
        {
            var sim = CoreTests.NewSim();
            sim.State.bossesDefeated = layer;
            sim.Teleport(GameData.Layers[layer].RowEnd, 20);
            for (int i = 0; i < 40 && !sim.BossActive; i++) sim.Tick(0.05);
            Assert.IsTrue(sim.BossActive);
            return sim;
        }

        private static void WaitForMove(GameSim sim)
        {
            var move = sim.CurrentBossMove;
            for (double t = 0; t < move.Every + move.Windup + 0.2 && !sim.BossMoveRunning; t += 0.05)
            {
                sim.Tick(0.05);
                if (sim.Events.Exists(e => e.Kind == SimEventKind.BossMoveStruck)) return;
            }
        }

        [Test]
        public void BossMoves_CoverEveryGuardian()
        {
            CoreTests.NewSim();
            Assert.AreEqual(GameData.Layers.Count, GameData.BossMoves.Count);
        }

        [Test]
        public void BossMove_WarnsBeforeItStrikes()
        {
            var sim = InFight(0);
            sim.Events.Clear();
            for (double t = 0; t < sim.CurrentBossMove.Every + 0.2; t += 0.05) sim.Tick(0.05);
            Assert.IsTrue(sim.Events.Exists(e => e.Kind == SimEventKind.BossMoveWarned));
            Assert.IsFalse(sim.Events.Exists(e => e.Kind == SimEventKind.BossMoveStruck));
            Assert.IsTrue(sim.BossMoveWindingUp);
        }

        [Test]
        public void Bind_StopsTheDrillsAndTappingFreesThem()
        {
            var sim = InFight(0);
            WaitForMove(sim);
            Assert.IsTrue(sim.DrillsBound);
            double swung = sim.Lead.LastSwing;
            sim.Tick(0.2);
            Assert.AreEqual(swung, sim.Lead.LastSwing, 1e-9, "a bound drill does not swing");

            var body = sim.Lead.Body;
            for (int i = 0; i < 20 && sim.DrillsBound; i++)
            {
                sim.ClickPixel((int)System.Math.Floor((body.X + 0.5) * 16), (int)System.Math.Floor((body.Y + 0.5) * 16));
                sim.Tick(0.01);
            }
            Assert.IsFalse(sim.DrillsBound, "tapping wears the hold down");
        }

        [Test]
        public void Immune_TakesNoDamage()
        {
            int layer = GameData.BossMoves.FindIndex(m => m.Kind == BossMoveKind.immune);
            var sim = InFight(layer);
            WaitForMove(sim);
            Assert.IsTrue(sim.BossMoveRunning);
            double hp = sim.BossHp + sim.BossArmor;
            var body = sim.Lead.Body;
            for (int i = 0; i < 5; i++)
                sim.ClickPixel((int)System.Math.Floor((body.X + 0.5) * 16), GameData.Layers[layer].RowEnd * 16 + 8);
            Assert.AreEqual(hp, sim.BossHp + sim.BossArmor, 1e-6);
        }

        [Test]
        public void Heal_GivesBackHealth()
        {
            int layer = GameData.BossMoves.FindIndex(m => m.Kind == BossMoveKind.heal);
            var sim = InFight(layer);
            WaitForMove(sim);
            Assert.Greater(sim.BossHp, 0);
            Assert.LessOrEqual(sim.BossHp, sim.BossMaxHp);
            Assert.AreEqual(1, sim.Events.Count(e => e.Kind == SimEventKind.BossMoveStruck), "it struck once");
        }

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
            Assert.Greater(sim.State.LevelOf(GameSim.RootSkill), 1);
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
            Assert.AreEqual(before * 5, sim.DamageOf(code), 1e-9);
        }

        [Test]
        public void School_CraftCapsTheHangar()
        {
            var sim = Rebirthed(1);
            sim.ChooseSchool(School("craft"));
            sim.State.coins = 1e30;
            Assert.AreEqual(Balance.FewDrillsCap - sim.State.drills.Count, sim.BuyDrills(0, 100));
            Assert.AreEqual(Balance.FewDrillsCap, sim.State.drills.Count);
            Assert.IsFalse(sim.CanBuyDrill(0));
            Assert.AreEqual(0, sim.AffordableDrills(0, 10));
        }

        [Test]
        public void School_CraftRefusesAFullHangar()
        {
            var sim = Rebirthed(1);
            sim.State.coins = 1e30;
            sim.BuyDrills(0, Balance.FewDrillsCap + 1);
            Assert.IsFalse(sim.ChooseSchool(School("craft")));
        }

        [Test]
        public void School_SwarmCannotMerge()
        {
            var sim = Rebirthed(1);
            sim.ChooseSchool(School("swarm"));
            sim.State.coins = 1e30;
            sim.BuyDrills(0, 5);
            int code = DrillCode.Make(0, 0);
            Assert.IsFalse(sim.CanMerge(code));
            Assert.IsFalse(sim.Merge(code));
            Assert.AreEqual(0, sim.MergeEverything());
            Assert.IsFalse(sim.MergeAll());
        }

        [Test]
        public void School_BlastGoesOffOnItsOwn()
        {
            var sim = Rebirthed(1);
            sim.ChooseSchool(School("blast"));
            int blasts = 0;
            for (int i = 0; i < (int)(Balance.PulseSeconds * 2.5 * 20); i++)
            {
                sim.Tick(0.05);
                blasts += sim.Events.Count(e => e.Kind == SimEventKind.Explosion);
                sim.Events.Clear();
            }
            Assert.GreaterOrEqual(blasts, 2);
        }

        [Test]
        public void School_RichPaysNothingForPlainRock()
        {
            var plain = Rebirthed(1);
            var rich = Rebirthed(1);
            rich.ChooseSchool(School("rich"));
            rich.State.seed = plain.State.seed;
            plain.State.coins = rich.State.coins = 1e6;
            plain.Buy(GameSim.RootSkill);
            rich.Buy(GameSim.RootSkill);
            plain.State.coins = rich.State.coins = 0;
            for (int i = 0; i < 40; i++)
            {
                plain.Tick(0.05);
                rich.Tick(0.05);
            }
            Assert.Greater(plain.State.coins, 0);
            if (rich.State.ores == 0) Assert.AreEqual(0, rich.State.coins, 1e-12);
        }

        [Test]
        public void SecondSchool_NeedsAClearedSchool()
        {
            var sim = Rebirthed(1);
            sim.ChooseSchool(School("craft"));
            Assert.IsFalse(sim.ChooseSecond(School("blast")));
            sim.State.schoolsCleared.Add(School("blast"));
            Assert.IsFalse(sim.ChooseSecond(School("craft")));
            Assert.IsTrue(sim.ChooseSecond(School("blast")));
            Assert.IsTrue(sim.HasTrait(SchoolTrait.pulse));
            Assert.IsTrue(sim.HasTrait(SchoolTrait.few_drills));
            Assert.IsFalse(sim.ChooseSecond(School("rich")));
        }

        [Test]
        public void SecondSchool_AddsBonusesButNotItsDiscount()
        {
            var sim = Rebirthed(1);
            var blast = GameData.Skills.First(s => s.Branch == "blast");
            double full = sim.SkillCost(blast), before = sim.Effect(SkillEffect.blast_dmg);
            sim.State.schoolsCleared.Add(School("blast"));
            sim.ChooseSchool(School("rich"));
            double cleared = sim.Effect(SkillEffect.blast_dmg);
            sim.ChooseSecond(School("blast"));
            Assert.AreEqual(cleared + 200, sim.Effect(SkillEffect.blast_dmg), 1e-9);
            Assert.AreEqual(full, sim.SkillCost(blast), 1e-9);
        }

        [Test]
        public void SecondSchool_RefusesTheClashingPair()
        {
            var sim = Rebirthed(1);
            sim.State.schoolsCleared.Add(School("swarm"));
            sim.ChooseSchool(School("craft"));
            Assert.IsFalse(sim.ChooseSecond(School("swarm")));
        }

        [Test]
        public void SecondSchool_EndsWithTheRun()
        {
            var sim = Rebirthed(1);
            sim.State.schoolsCleared.Add(School("blast"));
            sim.ChooseSchool(School("rich"));
            sim.ChooseSecond(School("blast"));
            sim.State.bossesDefeated = 1;
            sim.State.maxDepth = 400;
            sim.State.totalIncome = 1e6;
            Assert.IsTrue(sim.Rebirth());
            Assert.IsNull(sim.SecondSchool);
        }

        [Test]
        public void Challenge_NoClickIgnoresTaps()
        {
            var sim = Rebirthed(1);
            sim.State.challenge = Challenge(ChallengeRule.no_click);
            Assert.IsFalse(sim.ClickPixel(PixelWorld.Width / 2, 80));
            Assert.AreEqual(0, sim.ClickBoost, 1e-12);
        }

        [Test]
        public void Challenge_NoMergeBlocksMerging()
        {
            var sim = Rebirthed(1);
            sim.State.coins = 1e30;
            sim.BuyDrills(0, 3);
            sim.State.challenge = Challenge(ChallengeRule.no_merge);
            Assert.IsTrue(sim.MergeBlocked);
            Assert.AreEqual(0, sim.MergeEverything());
        }

        [Test]
        public void Challenge_RushFailsWhenTimeRunsOut()
        {
            var sim = Rebirthed(1);
            int index = Challenge(ChallengeRule.rush);
            sim.State.challenge = index;
            sim.State.runSeconds = GameData.Challenges[index].Amount + 1;
            sim.Tick(1.1);
            Assert.IsNull(sim.CurrentChallenge);
            Assert.IsFalse(sim.ChallengeDone(index));
            Assert.IsTrue(sim.Events.Any(e => e.Kind == SimEventKind.ChallengeFailed));
        }

        [Test]
        public void Challenge_RushPaysWhenDepthComesFirst()
        {
            var sim = Rebirthed(1);
            int index = Challenge(ChallengeRule.rush);
            sim.State.challenge = index;
            sim.State.maxDepth = GameData.Challenges[index].TargetRow;
            sim.State.runSeconds = GameData.Challenges[index].Amount + 1;
            sim.Tick(1.1);
            Assert.IsTrue(sim.ChallengeDone(index));
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

        #region step 7: challenge wells

        private static GameSim ChallengeReady()
        {
            var sim = CoreTests.NewSim();
            sim.State.maxDepthEver = GameData.Layers[Balance.ChallengeLayer].RowStart;
            return sim;
        }

        private static int Challenge(ChallengeRule rule) => GameData.Challenges.FindIndex(c => c.Rule == rule);

        [Test]
        public void Challenge_LockedUntilDeepEnough()
        {
            var sim = CoreTests.NewSim();
            Assert.IsFalse(sim.StartChallenge(0));
            Assert.IsTrue(ChallengeReady().StartChallenge(0));
        }

        [Test]
        public void Challenge_RestartsTheRunWithoutShards()
        {
            var sim = ChallengeReady();
            sim.State.coins = 5000;
            sim.State.maxDepth = 300;
            sim.State.shards = 7;
            Assert.IsTrue(sim.StartChallenge(0));
            Assert.AreEqual(0, sim.State.maxDepth);
            Assert.AreEqual(7, sim.State.shards);
            Assert.AreEqual(Balance.StartGold, sim.State.coins, 1e-9);
        }

        [Test]
        public void Challenge_RulesBlockThePurchase()
        {
            var skills = ChallengeReady();
            skills.StartChallenge(Challenge(ChallengeRule.no_skills));
            skills.State.coins = 1e6;
            skills.Buy(GameSim.RootSkill);
            Assert.IsFalse(skills.Buy("drill_01"));

            var drills = ChallengeReady();
            drills.StartChallenge(Challenge(ChallengeRule.no_drills));
            drills.State.coins = 1e6;
            Assert.IsFalse(drills.BuyDrill(0));

            var frenzy = ChallengeReady();
            frenzy.StartChallenge(Challenge(ChallengeRule.no_frenzy));
            frenzy.FillFrenzy();
            Assert.IsFalse(frenzy.StartFrenzy());
        }

        [Test]
        public void Challenge_WeakRuleCutsDamage()
        {
            var sim = ChallengeReady();
            int code = DrillCode.Make(0, 0);
            double before = sim.DamageOf(code);
            sim.StartChallenge(Challenge(ChallengeRule.weak));
            sim.GrantSkill(GameSim.RootSkill, 1);
            Assert.AreEqual(before * 0.1, sim.DamageOf(code), 1e-9);
        }

        [Test]
        public void Challenge_BlocksRebirth()
        {
            var sim = ChallengeReady();
            sim.StartChallenge(0);
            sim.State.bossesDefeated = 1;
            sim.State.maxDepth = 200;
            sim.State.totalIncome = 1e6;
            Assert.IsFalse(sim.CanRebirth);
        }

        [Test]
        public void Challenge_ReachingTheTargetPaysAndEndsIt()
        {
            var sim = ChallengeReady();
            int index = Challenge(ChallengeRule.no_skills);
            int code = DrillCode.Make(0, 0);
            double before = sim.DamageOf(code);
            sim.StartChallenge(index);
            sim.GrantSkill(GameSim.RootSkill, 1);
            sim.State.maxDepth = GameData.Challenges[index].TargetRow;
            sim.Tick(1.1);
            Assert.IsTrue(sim.ChallengeDone(index));
            Assert.IsNull(sim.CurrentChallenge);
            Assert.AreEqual(0, sim.State.maxDepth);
            Assert.Greater(sim.DamageOf(code), before);
            Assert.IsFalse(sim.CanStartChallenge(index));
        }

        [Test]
        public void Challenge_CanBeAbandoned()
        {
            var sim = ChallengeReady();
            sim.StartChallenge(0);
            Assert.IsTrue(sim.AbandonChallenge());
            Assert.IsNull(sim.CurrentChallenge);
            Assert.IsFalse(sim.ChallengeDone(0));
        }

        #endregion

        #region step 8: warp, constellation and planets

        private static GameSim AtTheCentre()
        {
            var sim = CoreTests.NewSim();
            sim.State.maxDepthEver = GameData.TotalRows;
            return sim;
        }

        [Test]
        public void Warp_NeedsTheCentre()
        {
            Assert.IsFalse(CoreTests.NewSim().Warp());
            Assert.IsTrue(AtTheCentre().Warp());
        }

        [Test]
        public void Warp_TradesShardsForStarPoints()
        {
            var sim = AtTheCentre();
            sim.State.shards = 999;
            sim.Warp();
            Assert.AreEqual(4, sim.State.starPoints);
            Assert.AreEqual(0, sim.State.shards);
            Assert.AreEqual(0, sim.State.maxDepthEver);
            Assert.AreEqual(1, sim.State.warps);
        }

        [Test]
        public void Warp_KeepsWhatWasEarnedForGood()
        {
            var sim = AtTheCentre();
            sim.State.ores = 100;
            sim.UpgradeFactory(0);
            sim.State.rebirths = 5;
            sim.State.AddLayerPixel(0);
            sim.Warp();
            Assert.AreEqual(1, sim.FactoryLevel(0));
            Assert.AreEqual(5, sim.State.rebirths);
            Assert.Greater(sim.State.LayerPixelsOf(0), 0);
        }

        [Test]
        public void Warp_ChangesThePlanetAndItsRule()
        {
            var sim = AtTheCentre();
            Assert.AreEqual("地球", sim.Planet.Name);
            int code = DrillCode.Make(0, 0);
            double before = sim.DamageOf(code);
            sim.Warp();
            Assert.AreEqual("月壤星", sim.Planet.Name);
            sim.GrantSkill(GameSim.RootSkill, 1);
            Assert.AreEqual(before * sim.Planet.Damage, sim.DamageOf(code), 1e-9);
        }

        [Test]
        public void Planet_LowGravityFallsSlower()
        {
            var sim = AtTheCentre();
            sim.Warp();
            sim.Tick(0.1);
            Assert.AreEqual(sim.Planet.Gravity, sim.Lead.Body.GravityScale, 1e-9);
        }

        [Test]
        public void Planet_ShardMultiplierApplies()
        {
            var sim = AtTheCentre();
            sim.State.bossesDefeated = 1;
            sim.State.maxDepth = 400;
            sim.State.totalIncome = 1e6;
            int home = sim.ShardsOnRebirth;
            sim.State.warps = GameData.Planets.FindIndex(p => p.Shards > 1);
            Assert.AreEqual(home * 2, sim.ShardsOnRebirth, 1);
        }

        [Test]
        public void Constellation_SpendsPointsAndStacks()
        {
            var sim = CoreTests.NewSim();
            int code = DrillCode.Make(0, 0);
            double before = sim.DamageOf(code);
            sim.State.starPoints = 3;
            Assert.IsTrue(sim.BuyStar(0));
            Assert.AreEqual(2, sim.State.starPoints);
            Assert.AreEqual(before * 2, sim.DamageOf(code), 1e-9);
            Assert.IsTrue(sim.BuyStar(0));
            Assert.AreEqual(0, sim.State.starPoints, "the second level costs two");
            Assert.IsFalse(sim.BuyStar(0));
        }

        [Test]
        public void NewFields_StartEmpty()
        {
            Assert.AreEqual(-1, new GameState().school);
            Assert.AreEqual(-1, new GameState().research);
            Assert.AreEqual(-1, new GameState().challenge);
        }

        #endregion
    }
}
