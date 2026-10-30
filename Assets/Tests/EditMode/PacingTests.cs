using DeepDrill.Core;
using NUnit.Framework;

namespace DeepDrill.Tests
{
    public class PacingTests
    {
        [Test]
        public void Offline_ReportsWhatTheCapCut()
        {
            var sim = CoreTests.NewSim();
            sim.State.incomePerSecond = 10;
            sim.State.lastSaveUnixSeconds = 1000;

            double earned = sim.ApplyOffline(1000 + 3 * 3600);

            var report = sim.LastOffline;
            Assert.AreEqual(3 * 3600, report.SecondsAway, 1e-9);
            Assert.AreEqual(2 * 3600, report.SecondsPaid, 1e-9);
            Assert.AreEqual(0.5, report.Rate, 1e-9);
            Assert.AreEqual(earned, report.Coins, 1e-9);
            Assert.IsTrue(report.Capped);
        }

        [Test]
        public void ShardRate_WaitsForTheRunToSettle()
        {
            var sim = CoreTests.NewSim();
            sim.State.maxDepth = 400;
            sim.State.totalIncome = 1e6;
            sim.State.runSeconds = Balance.RebirthRateMinSeconds - 1;
            Assert.AreEqual(0, sim.ShardsPerMinute, 1e-9);

            sim.State.runSeconds = 600;
            Assert.AreEqual(Balance.ShardProgress(400, 1e6) / 10, sim.ShardsPerMinute, 1e-9);
        }

        [Test]
        public void RebirthAdvice_ComesWhenTheRateSlips()
        {
            var sim = CoreTests.NewSim();
            sim.State.bossesDefeated = 1;
            sim.State.maxDepth = 400;
            sim.State.totalIncome = 1e6;
            sim.State.runSeconds = 600;
            sim.State.peakShardRate = sim.ShardsPerMinute;
            Assert.IsFalse(sim.RebirthAdvised);

            sim.State.runSeconds = 900;
            Assert.IsTrue(sim.RebirthAdvised);
        }

        [Test]
        public void Boss_PaysWhileItHoldsTheWay()
        {
            var sim = CoreTests.NewSim();
            sim.Teleport(100, 20);
            sim.Tick(0.2);
            Assert.IsTrue(sim.BossActive);
            double before = sim.State.coins;
            for (int i = 0; i < 200; i++) sim.Tick(0.05);
            Assert.AreEqual(0, sim.State.bossesDefeated, "one wooden drill cannot win");
            Assert.Greater(sim.State.coins, before, "but its swings still earn something to upgrade with");
        }

        [Test]
        public void HandDigging_NeverWedgesTheDrill()
        {
            // seed 7 drops the first drill beside a cave where its body catches on the overhang
            var sim = CoreTests.FreshSim(7);
            for (int i = 0; i < 1600; i++)
            {
                sim.Tick(0.05);
                if (i % 5 != 0) continue;
                var body = sim.Lead.Body;
                sim.ClickPixel((int)System.Math.Floor((body.X + 0.5) * 16), (int)System.Math.Floor((body.Y + 0.5) * 16) + 14);
            }
            Assert.Greater(sim.State.maxDepth, 20);
        }

        [Test]
        public void Rebirth_RestartsTheRunClock()
        {
            var sim = CoreTests.NewSim();
            sim.State.bossesDefeated = 1;
            sim.State.maxDepth = 400;
            sim.State.totalIncome = 1e6;
            sim.State.runSeconds = 600;
            sim.State.peakShardRate = 3;

            Assert.IsTrue(sim.Rebirth());

            Assert.AreEqual(0, sim.State.runSeconds, 1e-9);
            Assert.AreEqual(0, sim.State.peakShardRate, 1e-9);
        }
    }
}
