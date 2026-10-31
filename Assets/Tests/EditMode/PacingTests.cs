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
            sim.State.maxDepth = 800;
            sim.State.totalIncome = 1e6;
            sim.State.runSeconds = 600;
            sim.State.peakShardRate = sim.ShardsPerMinute;
            Assert.IsFalse(sim.RebirthAdvised);

            sim.State.runSeconds = 900;
            Assert.IsTrue(sim.RebirthAdvised);
        }

        [Test]
        public void RebirthAdvice_SkipsARunStuckAtAnEarlyGuardian()
        {
            var sim = CoreTests.NewSim();
            sim.State.bossesDefeated = 1;
            sim.State.maxDepth = 250;
            sim.State.totalIncome = 1e4;
            sim.State.runSeconds = 600;
            sim.State.peakShardRate = sim.ShardsPerMinute;
            sim.State.runSeconds = 1800;

            Assert.IsTrue(sim.CanRebirth);
            Assert.Less(sim.ShardsOnRebirth, Balance.RebirthAdviceMinShards);
            Assert.IsFalse(sim.RebirthAdvised);
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
        public void Perf_TwoHundredDrillsStayWithinAFrame()
        {
            var sim = CoreTests.NewSim();
            for (int i = 0; i < 199; i++) sim.State.drills.Add(DrillCode.Make(i % 3, 6));
            sim.SyncDrills();
            sim.Teleport(300, 20);
            for (int i = 0; i < 40; i++) sim.Tick(1.0 / 60); // let them settle first

            long allocatedBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            double worst = 0;
            const int Frames = 300;
            for (int i = 0; i < Frames; i++)
            {
                double before = clock.Elapsed.TotalMilliseconds;
                sim.Tick(1.0 / 60);
                sim.Events.Clear();
                worst = System.Math.Max(worst, clock.Elapsed.TotalMilliseconds - before);
            }
            double average = clock.Elapsed.TotalMilliseconds / Frames;
            double kilobytes = (System.GC.GetAllocatedBytesForCurrentThread() - allocatedBefore) / 1024.0 / Frames;
            System.Console.WriteLine($"perf200 avg={average:0.00}ms worst={worst:0.00}ms units={sim.Units.Count} alloc={kilobytes:0.0}KB");
            Assert.Less(average, 8, "rules must leave most of a 60 FPS frame for drawing");
        }

        [Test]
        public void Magnet_PullsOreWithinReachOnly()
        {
            var sim = CoreTests.NewSim();
            sim.State.drills.Clear();
            sim.State.drills.Add(DrillCode.Make(MagnetFamily, 0));
            sim.SyncDrills();
            sim.Teleport(30, 20);

            int x = (int)System.Math.Floor((sim.Lead.Body.X + 0.5) * PixelWorld.TileSize);
            int y = (int)System.Math.Floor((sim.Lead.Body.Y + 0.5) * PixelWorld.TileSize);
            int reach = Balance.MagnetReachTiles * PixelWorld.TileSize;
            ClearOre(sim, x, y, reach * 2);
            PutOre(sim, x + reach - 10, y);
            PutOre(sim, x + reach + 12, y);

            for (int i = 0; i < 20; i++) sim.Tick(0.05);

            Assert.IsFalse(Mat.IsOre(sim.World.Get(x + reach - 10, y)), "ore in reach is drawn in");
            Assert.IsTrue(Mat.IsOre(sim.World.Get(x + reach + 12, y)), "ore out of reach stays");
        }

        // natural ore nearer the drill would use up the pull before it reached the test's own
        private static void ClearOre(GameSim sim, int cx, int cy, int radius)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                    if (Mat.IsOre(sim.World.Get(x, y)))
                        sim.World.ChunkAt(x / PixelChunk.Size, y / PixelChunk.Size).Mats[y % PixelChunk.Size * PixelChunk.Size + x % PixelChunk.Size] = Mat.Dirt;
        }

        private const int MagnetFamily = 4;

        private static void PutOre(GameSim sim, int x, int y)
        {
            var chunk = sim.World.ChunkAt(x / PixelChunk.Size, y / PixelChunk.Size);
            chunk.Mats[y % PixelChunk.Size * PixelChunk.Size + x % PixelChunk.Size] = Mat.OreBase;
        }

        [Test]
        public void Opening_CostsFiveThenSix()
        {
            var sim = CoreTests.FreshSim();
            Assert.AreEqual(5, sim.SkillCost(GameData.Skill(GameSim.RootSkill)), 1e-9);
            Assert.AreEqual(6, sim.DrillPrice(0), 1e-9);
            sim.State.coins = 5;
            Assert.IsTrue(sim.Buy(GameSim.RootSkill));
            Assert.IsTrue(sim.AutoDrillUnlocked);
            Assert.IsFalse(sim.CanBuyDrill(0));
            sim.State.coins = 6;
            Assert.IsTrue(sim.BuyDrill(0));
        }

        [Test]
        public void ClickBoost_StacksToItsCapAndFades()
        {
            var sim = CoreTests.NewSim();
            double set = sim.SpeedMultiplier;
            sim.ClickPixel(0, 0);
            Assert.AreEqual(Balance.ClickBoostPerTap, sim.ClickBoost, 1e-9);
            Assert.AreEqual(set * (1 + Balance.ClickBoostPerTap), sim.SpeedMultiplier, 1e-9);

            for (int i = 0; i < 100; i++) sim.ClickPixel(0, 0);
            Assert.AreEqual(Balance.ClickBoostMax, sim.ClickBoost, 1e-9);

            for (int i = 0; i < 40; i++) sim.Tick(0.1);
            Assert.AreEqual(0, sim.ClickBoost, 1e-9);
            Assert.AreEqual(set, sim.SpeedMultiplier, 1e-9, "without tapping the drills go back to their set speed");
        }

        [Test]
        public void MergeEverything_CascadesAndSendsOneEvent()
        {
            var sim = CoreTests.NewSim();
            sim.State.drills.Clear();
            for (int i = 0; i < 8; i++) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.State.drills.Add(DrillCode.Make(1, 0));
            sim.SyncDrills();
            sim.Events.Clear();

            Assert.AreEqual(7, sim.MergeEverything());

            Assert.AreEqual(2, sim.State.drills.Count);
            Assert.Contains(DrillCode.Make(0, 3), sim.State.drills);
            Assert.Contains(DrillCode.Make(1, 0), sim.State.drills);
            Assert.AreEqual(7, sim.State.mergesEver, 1e-9);
            Assert.AreEqual(1, sim.Events.FindAll(e => e.Kind == SimEventKind.DrillsMergedAll).Count);
            Assert.AreEqual(0, sim.Events.FindAll(e => e.Kind == SimEventKind.DrillMerged).Count);
            Assert.AreEqual(0, sim.MergeEverything(), "nothing is left to merge");
        }

        [Test]
        public void MergeEverything_HandlesAHugeHangarQuickly()
        {
            var sim = CoreTests.NewSim();
            sim.State.drills.Clear();
            for (int i = 0; i < 20000; i++) sim.State.drills.Add(DrillCode.Make(i % 3, 0));
            sim.SyncDrills();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            sim.MergeEverything();
            Assert.Less(clock.ElapsedMilliseconds, 100);
        }

        [Test]
        public void BulkBuy_StopsWhenTheCoinsRunOut()
        {
            var sim = CoreTests.NewSim();
            double price = sim.DrillsCost(0, 3);
            sim.State.coins = price + 1;
            Assert.AreEqual(3, sim.AffordableDrills(0, 10));

            int before = sim.State.drills.Count;
            Assert.AreEqual(3, sim.BuyDrills(0, 10));
            Assert.AreEqual(before + 3, sim.State.drills.Count);
            Assert.AreEqual(1, sim.State.coins, 1e-6);
            Assert.AreEqual(before + 3, sim.Units.Count);
        }

        [Test]
        public void Bedrock_HoldsUnderTheCentre()
        {
            var sim = CoreTests.NewSim();
            sim.State.drills.Clear();
            sim.State.drills.Add(DrillCode.Make(3, GameData.Tiers - 1));
            sim.SyncDrills();
            sim.State.bossesDefeated = GameData.Layers.Count;
            sim.Teleport(PixelWorld.FloorRow - 2, 20);

            for (int i = 0; i < 400; i++) sim.Tick(0.05);

            Assert.LessOrEqual(sim.State.maxDepth, PixelWorld.FloorRow + 1);
            int x = 20 * PixelWorld.TileSize, y = (PixelWorld.FloorRow + 1) * PixelWorld.TileSize;
            Assert.AreEqual(Mat.Wall, sim.World.Get(x, y));
            sim.World.CarveCircle(x, y, 4 * PixelWorld.TileSize);
            Assert.AreEqual(Mat.Wall, sim.World.Get(x, y), "the last guardian's breach stops at the bedrock too");
        }

        [Test]
        public void Chunks_StayWhileADrillStillStandsOnThem()
        {
            var sim = CoreTests.NewSim();
            for (int i = 0; i < 20; i++) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.SyncDrills();
            var straggler = sim.Units[sim.Units.Count - 1];
            sim.Teleport(120, 20);
            straggler.Body.Place(5, 2);
            int topChunks = 0;
            for (int i = 0; i < 40; i++)
            {
                straggler.Body.Place(5, 2);
                sim.Tick(1.0 / 60);
            }
            sim.Teleport(200, 20);
            sim.Tick(1.0 / 60);
            foreach (var pair in sim.World.Chunks)
                if (pair.Key / 4096 == 0) topChunks++;
            Assert.Greater(topChunks, 0, "the ground under a drill left behind must not be dropped");
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
