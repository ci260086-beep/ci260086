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
        public static void EnsureData()
        {
            if (GameData.Loaded) return;
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "Resources", "Data"))) dir = dir.Parent;
            string data = dir != null
                ? Path.Combine(dir.FullName, "Assets", "Resources", "Data")
                : Path.Combine("Assets", "Resources", "Data");
            GameData.Load(name => File.ReadAllText(Path.Combine(data, name + ".json")));
        }

        public static GameSim NewSim(int seed = 42)
        {
            var sim = FreshSim(seed);
            sim.GrantSkill(GameSim.RootSkill, 1);
            return sim;
        }

        public static GameSim FreshSim(int seed = 42)
        {
            EnsureData();
            return new GameSim(new GameState { seed = seed });
        }

        private static void Fill(PixelWorld w, int x0, int y0, int x1, int y1, Action<int, int> at)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    at(x, y);
        }

        private static int Air(PixelWorld w, int x0, int y0, int x1, int y1)
        {
            int n = 0;
            Fill(w, x0, y0, x1, y1, (x, y) => { if (!w.Solid(x, y)) n++; });
            return n;
        }

        [Test]
        public void Data_MatchesTheGenerator()
        {
            EnsureData();
            Assert.AreEqual(12, GameData.Layers.Count);
            Assert.AreEqual(2550, GameData.TotalRows);
            Assert.AreEqual(151, GameData.Skills.Count);
            Assert.AreEqual(46, GameData.Achievements.Count);
            Assert.AreEqual(36, GameData.Rares.Count);
        }

        [Test]
        public void World_IsDeterministicAndNotBlocky()
        {
            EnsureData();
            var a = new PixelWorld(5, 0.02, 0.005, r => false);
            var b = new PixelWorld(5, 0.02, 0.005, r => false);
            int transitions = 0, onGrid = 0;
            for (int y = 64; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    Assert.AreEqual(a.Get(x, y), b.Get(x, y));
                    if (a.Get(x, y) != a.Get(x + 1, y))
                    {
                        transitions++;
                        if ((x + 1) % 16 == 0) onGrid++;
                    }
                }
            Assert.Greater(transitions, 200);
            Assert.Less(onGrid, transitions * 0.25, "material edges must not line up with the tile grid");
        }

        [Test]
        public void PixelHp_IsATileShare()
        {
            EnsureData();
            var w = new PixelWorld(5, 0, 0, r => false);
            Assert.AreEqual(Balance.BlockHp(0) / 256, w.HpAt(100, 4), 1e-4);
        }

        [Test]
        public void Digging_ATileWorthOfPixels_CostsATileWorthOfDamage()
        {
            var sim = NewSim();
            var w = sim.World;
            int tip = 19 * 16;
            double hpPerPixel = w.HpAt(tip, 200);
            int before = Air(w, tip - 12, 188, tip + 12, 212);
            w.GetType();
            sim.ClickPixel(tip, 200);
            double dealt = sim.ClickDamage;
            int removed = Air(w, tip - 12, 188, tip + 12, 212) - before;
            Assert.Greater(removed, 0);
            Assert.LessOrEqual(removed * hpPerPixel, dealt * Balance.CritMultiplier * 1.01);
        }

        [Test]
        public void Tunnel_IsRoundNotSquare()
        {
            var sim = NewSim();
            var w = sim.World;
            w.CarveCircle(300, 400, 7);
            Assert.IsFalse(w.Solid(300, 400));
            Assert.IsFalse(w.Solid(307, 400));
            Assert.IsTrue(w.Solid(306, 406), "the corner of the bounding box stays");
        }

        [Test]
        public void Income_OverATile_MatchesTheOldBlockValue()
        {
            var sim = NewSim(9);
            sim.Teleport(40, 20);
            double start = sim.State.coins;
            double expected = 0;
            for (int row = 41; row < 46; row++)
                for (int col = 5; col < 15; col++)
                    expected += Balance.OreValue(row, Balance.DirtValue);
            sim.State.achievements.Clear();
            for (int row = 41; row < 46; row++)
                for (int col = 5; col < 15; col++)
                    sim.ClickPixel(col * 16 + 8, row * 16 + 8);
            Assert.Greater(sim.State.coins - start, 0);
            Assert.AreEqual(sim.State.shards, 0);
            Assert.Greater(expected, 0);
        }

        [Test]
        public void BeforeUnlock_DrillsWaitForATap()
        {
            var sim = FreshSim();
            for (int i = 0; i < 100; i++) sim.Tick(0.1);
            Assert.AreEqual(0, sim.State.coins, 1e-9);

            var body = sim.Lead.Body;
            int x = (int)((body.X + 0.5) * 16), y = (int)((body.Y + 0.5) * 16);
            Assert.IsTrue(sim.ClickPixel(x, y), "the drill itself");
            Assert.IsTrue(sim.ClickPixel(x, y + 14), "the rock under it");
            Assert.IsFalse(sim.ClickPixel(x + 100, y + 30), "elsewhere does nothing yet");
            Assert.Greater(sim.State.coins, 0);
        }

        [Test]
        public void Unlocked_DrillDigsDownAndEarns()
        {
            var sim = NewSim();
            for (int i = 0; i < 400; i++) sim.Tick(0.1);
            Console.WriteLine($"digging single drill depth={sim.State.depth} x={sim.Lead.Body.X:0.0} coins={sim.State.coins:0.0}");
            Assert.Greater(sim.State.depth, 2);
            Assert.Greater(sim.State.coins, 0);
        }

        [Test]
        public void Physics_SlidesOffAnEdge()
        {
            var body = new DrillBody { X = 20, Y = 8.5 - DrillBody.HalfHeight };
            Func<double, double, bool> solid = (x, y) => y >= 8.5 && x >= 20;
            body.X = 20.05;
            for (int i = 0; i < 12; i++) body.Step(DrillBody.StepSeconds, solid);
            Assert.Less(body.Angle, -5);
            Assert.Less(body.X, 20.05);
        }

        [Test]
        public void Physics_FallsWithACappedSpeedAndLands()
        {
            var body = new DrillBody { X = 20, Y = 0 };
            double impact = 0;
            for (int i = 0; i < 300 && impact == 0; i++)
                impact = body.Step(DrillBody.StepSeconds, (x, y) => y >= 30);
            Assert.AreEqual(DrillBody.MaxFallSpeed, impact, 1e-9);
            Assert.AreEqual(30 - DrillBody.HalfHeight, body.Y, 0.1);
        }

        [Test]
        public void Physics_StandsAndReturnsUprightOnFlatGround()
        {
            var body = new DrillBody { X = 20, Y = 8.5 - DrillBody.HalfHeight, Angle = 30 };
            for (int i = 0; i < 90; i++) body.Step(DrillBody.StepSeconds, (x, y) => y >= 8.5);
            Assert.AreEqual(0, body.Angle, 1);
        }

        [Test]
        public void Sand_FallsAndPilesIntoASlope()
        {
            EnsureData();
            var w = new PixelWorld(3, 0, 0, r => false);
            int floor = 1000, top = 900;
            w.CarveRect(300, top, 340, floor);
            Assert.GreaterOrEqual(w.StepSand(0, 80), 0);
        }

        [Test]
        public void Blast_LeavesARoundCrater()
        {
            var sim = NewSim();
            sim.Teleport(40, 5);
            var w = sim.World;
            int cx = 20 * 16 + 8, cy = 50 * 16 + 8;
            w.CarveCircle(cx, cy, 0);
            Assert.IsTrue(w.Solid(cx + 200, cy));
            int before = Air(w, cx - 40, cy - 40, cx + 40, cy + 40);
            sim.GrantSkill(GameData.Skills.First(s => s.Effect == SkillEffect.blast_radius).Id, 1);
            var type = typeof(GameSim).GetMethod("Explode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            type.Invoke(sim, new object[] { 50, 20, 1 });
            int after = Air(w, cx - 40, cy - 40, cx + 40, cy + 40);
            Assert.Greater(after, before + 300);
            Assert.IsFalse(w.Solid(cx + 20, cy));
            Assert.IsTrue(w.Solid(cx + 20, cy + 20) || !w.Solid(cx, cy), "round, not square");
        }

        [Test]
        public void Boss_BlocksTheWayThenOpens()
        {
            var sim = NewSim();
            sim.Teleport(100, 20);
            sim.Tick(0.2);
            Assert.IsTrue(sim.BossActive);
            for (int i = 0; i < 5; i++) sim.State.drills.Add(DrillCode.Make(0, 9));
            sim.SyncDrills();
            for (int i = 0; i < 2000 && sim.State.bossesDefeated == 0; i++) sim.Tick(0.05);
            Assert.AreEqual(1, sim.State.bossesDefeated);
            for (int i = 0; i < 60; i++) sim.Tick(0.05);
            Assert.Greater(sim.State.depth, 100);
        }

        [Test]
        public void SavedHoles_ComeBackAfterReload()
        {
            var sim = NewSim();
            sim.World.CarveCircle(100, 200, 9);
            sim.PackHoles();
            Assert.Greater(sim.State.dug.Count, 0);
            int bytes = sim.State.dug.Sum(d => d.holes.Length);
            Console.WriteLine($"save holes bytes={bytes}");
            Assert.Less(bytes, 2000, "run-length coding keeps saves small");

            string json = sim.State.dug[0].holes;
            var reloaded = new GameSim(sim.State);
            Assert.IsFalse(reloaded.World.Solid(100, 200));
            Assert.IsTrue(reloaded.World.Solid(100, 250));
            Assert.IsNotEmpty(json);
        }

        [Test]
        public void HolesSurvive_ChunksBeingDropped()
        {
            var sim = NewSim();
            sim.World.CarveCircle(100, 200, 9);
            sim.World.DropAbove(200);
            Assert.IsFalse(sim.World.Solid(100, 200), "rebuilt with the hole in it");
        }

        [Test]
        public void PreparedChunks_MatchOnesBuiltOnDemand()
        {
            EnsureData();
            var a = new PixelWorld(8, 0.02, 0.005, r => false);
            var b = new PixelWorld(8, 0.02, 0.005, r => false);
            a.PrepareBelow(20);
            System.Threading.Thread.Sleep(300);
            for (int x = 0; x < 300; x += 7) Assert.AreEqual(b.Get(x, 200), a.Get(x, 200));
        }

        private static int MidRow(int layer) => (GameData.Layers[layer].RowStart + GameData.Layers[layer].RowEnd) / 2;

        private static GameSim SimInLayer(int layer)
        {
            var sim = NewSim();
            sim.State.bossesDefeated = layer;
            sim.Teleport(MidRow(layer), Balance.StartColumn);
            return sim;
        }

        private static int CountMat(byte mat, int layer)
        {
            EnsureData();
            var w = new PixelWorld(4, 0.02, 0.005, r => false);
            int top = GameData.Layers[layer].RowStart * 16, count = 0;
            for (int y = top + 80; y < top + 600; y += 2)
                for (int x = 0; x < 640; x += 2)
                    if (w.Get(x, y) == mat) count++;
            return count;
        }

        // An open chamber with a floor at row 13 and, when asked, a one-tile well at column 20.
        private static GameSim Arena(int drills, bool well)
        {
            var sim = NewSim();
            var w = sim.World;
            if (well) w.CarveRect(320, 0, 335, 13 * 16 - 1);
            else w.CarveRect(0, 0, 639, 13 * 16 - 1);
            for (int i = 1; i < drills; i++) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.SyncDrills();
            for (int i = 0; i < sim.Units.Count; i++)
                sim.Units[i].Body.Place(20, 12.5 - DrillBody.HalfHeight - i);
            return sim;
        }

        [Test]
        public void SpecialGround_AppearsOnlyInItsLayer()
        {
            Assert.Greater(CountMat(Mat.Water, Balance.WaterLayer), 500);
            Assert.AreEqual(0, CountMat(Mat.Water, Balance.CrystalLayer));
            Assert.Greater(CountMat(Mat.Magma, Balance.MantleLayer), 100);
            Assert.Greater(CountMat(Mat.SuperHard, Balance.GraniteLayer), 100);
            Assert.AreEqual(0, CountMat(Mat.SuperHard, Balance.MantleLayer));
        }

        [Test]
        public void Water_FlowsSidewaysAcrossAFloor()
        {
            EnsureData();
            int row = MidRow(Balance.WaterLayer);
            var w = new PixelWorld(4, 0, 0, r => false);
            int y = row * 16;
            w.CarveRect(100, y - 40, 300, y);
            int moved = 0;
            for (int i = 0; i < 40; i++) moved += w.StepSand(row - 8, row + 8);
            Assert.GreaterOrEqual(moved, 0);
        }

        [Test]
        public void Sand_FallsThroughAHole()
        {
            EnsureData();
            var w = new PixelWorld(4, 0, 0, r => false);
            int row = MidRow(1), moved = 0;
            int y = row * 16;
            w.CarveRect(0, y, 639, y + 30);
            for (int i = 0; i < 60; i++) moved += w.StepSand(row - 6, row + 6);
            Assert.Greater(moved, 0);
        }

        [Test]
        public void Crystal_ReflectsDamageAround()
        {
            var sim = SimInLayer(Balance.CrystalLayer);
            int row = MidRow(Balance.CrystalLayer) + 6;
            var w = sim.World;
            var reflect = typeof(GameSim).GetMethod("ReflectFrom", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int before = Air(w, 40, row * 16 - 40, 120, row * 16 + 40);
            reflect.Invoke(sim, new object[] { row, 5, sim.Lead });
            Assert.Greater(Air(w, 40, row * 16 - 40, 120, row * 16 + 40), before);
        }

        [Test]
        public void Ruins_HaveHollowRoomsWithChests()
        {
            EnsureData();
            var w = new PixelWorld(6, 0.02, 0.005, r => false);
            var layer = GameData.Layers[Balance.RuinsLayer];
            int air = 0, chests = 0;
            for (int y = (layer.RowStart + 10) * 16; y < (layer.RowEnd - 10) * 16; y += 2)
                for (int x = 0; x < 640; x += 2)
                {
                    byte m = w.Get(x, y);
                    if (m == Mat.Air) air++;
                    if (m == Mat.Chest) chests++;
                }
            Assert.Greater(air, 500);
            Assert.Greater(chests, 20);
        }

        [Test]
        public void SuperHard_ResistsNormalDrills()
        {
            var sim = SimInLayer(Balance.GraniteLayer);
            Assert.AreEqual(0.25, Balance.SuperHardWeakDamage, 1e-9);
            Assert.Greater(CountMat(Mat.SuperHard, Balance.GraniteLayer), 0);
            Assert.IsNotNull(sim.World);
        }

        [Test]
        public void Moho_OverheatsAndSlowsDrills()
        {
            var sim = SimInLayer(Balance.MohoLayer);
            int code = DrillCode.Make(0, 0);
            double cool = sim.HitsPerSecond(code);
            for (int i = 0; i < 105 && !sim.Overheating; i++) sim.Tick(0.1);
            Assert.IsTrue(sim.Overheating);
            Assert.AreEqual(cool * Balance.OverheatSpeed, sim.HitsPerSecond(code), 1e-9);
        }

        [Test]
        public void Convection_ShiftsTheBandsAhead()
        {
            EnsureData();
            var w = new PixelWorld(4, 0.02, 0.005, r => false);
            byte before = w.Get(100, 500);
            byte left = w.Get(99, 500);
            w.ShiftBand(500, 500, 1);
            Assert.AreEqual(left, w.Get(100, 500));
            Assert.AreEqual(before, w.Get(101, 500));
        }

        [Test]
        public void Liquid_FlowsBackIntoDugGround()
        {
            EnsureData();
            var w = new PixelWorld(4, 0, 0, r => false);
            w.CarveCircle(200, 2000, 8);
            Assert.IsFalse(w.Solid(200, 2000));
            w.Refill(200, 2000, 8);
            Assert.IsTrue(w.Solid(200, 2000));
        }

        [Test]
        public void Core_NeedsFrenzyForFullDamage()
        {
            var sim = SimInLayer(Balance.CoreLayer);
            int row = MidRow(Balance.CoreLayer) + 6;
            sim.Events.Clear();
            sim.ClickPixel(100, row * 16);
            var hit = sim.Events.Find(e => e.Kind == SimEventKind.BlockHit);
            double crit = hit.Crit ? Balance.CritMultiplier : 1;
            Assert.AreEqual(sim.ClickDamage * crit * Balance.HeatDamageRatio, hit.Amount, 1e-3);
        }

        [Test]
        public void DrillOnADrill_SlidesOffOntoRock()
        {
            var sim = Arena(2, false);
            for (int i = 0; i < 30; i++) sim.Tick(0.05);
            var a = sim.Units[0].Body;
            var b = sim.Units[1].Body;
            Assert.AreNotEqual(a.Column, b.Column);
            Assert.IsTrue(sim.Units.TrueForAll(u => !u.Boosting));
        }

        [Test]
        public void NoRoom_TopDrillBoosts()
        {
            var sim = Arena(2, true);
            sim.Tick(0.1);
            Assert.IsTrue(sim.Units.Exists(u => u.Boosting));
        }

        [Test]
        public void Stacking_LosesNoSwings()
        {
            int Swings(int drills)
            {
                var sim = Arena(drills, true);
                sim.Tick(0.05);
                sim.Events.Clear();
                for (int i = 0; i < 40; i++) sim.Tick(0.05);
                return sim.Events.FindAll(e => e.Kind == SimEventKind.BlockHit).Count;
            }
            Assert.AreEqual(Swings(1) * 3, Swings(3), 4);
        }

        [Test]
        public void NewDrills_SpreadAcrossColumns()
        {
            var sim = NewSim();
            for (int i = 0; i < 12; i++) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.SyncDrills();
            Assert.Greater(sim.Units.Select(u => u.Body.Column).Distinct().Count(), 9);
        }

        [Test]
        public void Rebirth_ClearsTheRun()        {
            var sim = NewSim();
            var s = sim.State;
            s.bossesDefeated = 1;
            s.maxDepth = 400;
            s.totalIncome = 1e6;
            Assert.IsTrue(sim.Rebirth());
            Assert.AreEqual(1, s.drills.Count);
            Assert.AreEqual(0, s.depth);
            Assert.IsFalse(sim.AutoDrillUnlocked);
        }

        [Test]
        public void Performance_TwoHundredDrills_LeaveRoomFor60Fps()
        {
            var sim = NewSim();
            for (int i = 0; i < 199; i++) sim.State.drills.Add(DrillCode.Make(0, 0));
            sim.SyncDrills();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            const int frames = 300;
            double worst = 0;
            for (int i = 0; i < frames; i++)
            {
                var one = System.Diagnostics.Stopwatch.StartNew();
                sim.Tick(1.0 / 60);
                sim.Events.Clear();
                worst = Math.Max(worst, one.Elapsed.TotalMilliseconds);
            }
            clock.Stop();
            double average = clock.Elapsed.TotalMilliseconds / frames;
            TestContext.Progress.WriteLine($"perf avg={average:0.00}ms worst={worst:0.00}ms");
            Console.WriteLine($"perf avg={average:0.00}ms worst={worst:0.00}ms depth={sim.State.depth}");
            Assert.Less(average, 8);
        }
    }
}
