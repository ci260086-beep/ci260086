using System;
using System.Collections.Generic;

namespace DeepDrill.Core
{
    /// <summary>Everything that touches the pixel ground: digging, blasts, sand, drill footing.</summary>
    public sealed partial class GameSim
    {
        private const int Tile = PixelWorld.TileSize;
        private const int BossPixelsPerSwing = 1;
        private const int ComboPixels = 96;
        private const int OrePixelsPerPickup = 64;
        private const int ChestPixels = 60;
        private const int PixelsPerTile = Tile * Tile;
        private const int ClickRadius = 6;
        private const int NearDrillPixels = 12;
        private const int BaseDigRadius = 7;
        private const int BodyClearance = 4;
        private const int MaxRefills = 64;

        private static readonly Dictionary<int, (int dx, int dy)[]> DigShapes = new Dictionary<int, (int, int)[]>();

        private PixelWorld world;
        private readonly Dictionary<int, int> orePixels = new Dictionary<int, int>();
        private readonly Func<double, double, bool> solidAt;
        private int comboPixels;
        private int chestPixels;
        private double sandClock;
        private double convectionClock;
        private bool reflecting;
        private bool washed;
        private bool splashing;
        private DrillUnit stepper;
        private readonly Func<double, double, bool> solidForStepper;
        private readonly Queue<(int x, int y, int radius, double time)> refills = new Queue<(int, int, int, double)>();

        public PixelWorld World => world;

        private void ResetWorld()
        {
            world = new PixelWorld(State.seed, TntChance, ChestChance, IsBossRow);
            orePixels.Clear();
            comboPixels = chestPixels = 0;
            foreach (var chunk in State.dug) world.ImportHoles(chunk.key, Convert.FromBase64String(chunk.holes));
            world.PrepareBelow(State.depth);
        }

        /// <summary>Copies the dug-out ground into the state, ready to be written to the save.</summary>
        public void PackHoles()
        {
            State.dug.Clear();
            foreach (var pair in world.ExportHoles())
                State.dug.Add(new DugChunk { key = pair.Key, holes = Convert.ToBase64String(pair.Value) });
        }

        private static int ToPixel(double tiles) => (int)Math.Floor((tiles + 0.5) * Tile);

        private bool SolidAtTiles(double x, double y) => world.Solid(ToPixel(x), ToPixel(y));

        public bool IsSolidPixel(int x, int y) => world.Solid(x, y);

        // Offsets inside a circle, nearest first, so a swing eats outward from the tip.
        private static (int dx, int dy)[] ShapeFor(int radius)
        {
            if (DigShapes.TryGetValue(radius, out var shape)) return shape;
            var list = new List<(int dx, int dy)>();
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x * x + y * y <= radius * radius) list.Add((x, y));
            list.Sort((a, b) => (a.dx * a.dx + a.dy * a.dy).CompareTo(b.dx * b.dx + b.dy * b.dy));
            shape = list.ToArray();
            DigShapes[radius] = shape;
            return shape;
        }

        private int DigRadiusOf(DrillUnit unit)
        {
            if (unit.Family == HeavyFamily) return BaseDigRadius + 1;
            if (unit.Family == SwiftFamily) return BaseDigRadius - 1;
            return BaseDigRadius;
        }

        /// <summary>
        /// Spends a swing's damage on the pixels around a point, nearest first. Each pixel holds
        /// 1/256 of its tile's health, so digging a tile's worth costs what a whole block used to.
        /// </summary>
        private int Dig(int cx, int cy, int radius, double damage, bool fromClick, bool crit, DrillUnit by)
        {
            double budget = damage * (crit ? CritMultiplier : 1);
            if (GameData.LayerOfRow(cy / Tile) == Balance.CoreLayer && !InFrenzy) budget *= Balance.HeatDamageRatio;
            double dealt = budget;
            double trait = familyTrait[by.Family];
            bool heavy = by.Family == HeavyFamily;
            int removed = 0, row = cy / Tile, col = cx / Tile;
            byte lastMat = Mat.Dirt;

            foreach (var (dx, dy) in ShapeFor(radius))
            {
                int x = cx + dx, y = cy + dy;
                if (y < 0 || x < 0 || x >= PixelWorld.Width) continue;
                byte mat = world.Get(x, y);
                if (mat == Mat.Air) continue;

                if (mat == Mat.Boss)
                {
                    HitBoss(y / Tile, x / Tile, budget * (heavy ? 1.5 * trait : 1), fromClick, crit);
                    budget = 0;
                    break;
                }

                double rate = 1;
                if (mat == Mat.Hard) rate = (1 + Effect(SkillEffect.hard_pct) / 100) * (heavy ? 3 * trait : 1);
                if (mat == Mat.SuperHard) rate = (1 + Effect(SkillEffect.hard_pct) / 100) * (heavy ? 3 * trait : Balance.SuperHardWeakDamage);
                double cost = world.HpAt(x, y) / rate;
                if (cost > budget)
                {
                    world.SetHp(x, y, (float)((cost - budget) * rate));
                    budget = 0;
                    break;
                }
                budget -= cost;
                lastMat = mat;
                RemovePixel(x, y, mat, false, by);
                removed++;
                if (budget <= 0) break;
            }

            Events.Add(new SimEvent
            {
                Kind = SimEventKind.BlockHit, Row = row, Col = col, Block = BlockTypeOf(lastMat),
                Amount = dealt, FromClick = fromClick, Crit = crit,
            });
            if (removed > 0)
                Events.Add(new SimEvent { Kind = SimEventKind.PixelsDug, Row = cy, Col = cx, Amount = removed, Ore = lastMat });

            if (removed > 0 && GameData.LayerOfRow(row) == Balance.LiquidLayer && refills.Count < MaxRefills)
                refills.Enqueue((cx, cy, radius, simTime + Balance.RefillSeconds));
            if (washed)
            {
                washed = false;
                WashAround(cx, cy, by);
            }

            PassOverflow(cx, cy, budget, by);
            if (removed > 0 && by.Family == BlastFamily && !splashing)
            {
                // a blast drill's break throws part of its swing a tile further out
                splashing = true;
                Dig(cx, cy, radius + Tile, dealt * Balance.BlastDrillSplash * trait, false, false, by);
                splashing = false;
            }
            RunBlasts();
            return removed;
        }

        // pierce: damage left after clearing the circle carries on straight down
        private void PassOverflow(int cx, int cy, double overflow, DrillUnit by)
        {
            int layers = (int)Effect(SkillEffect.pierce);
            for (int i = 1; i <= layers && overflow > 0; i++)
            {
                int y = cy + i * Tile;
                double before = overflow;
                foreach (var (dx, dy) in ShapeFor(BaseDigRadius))
                {
                    int px = cx + dx, py = y + dy;
                    if (py < 0 || px < 0 || px >= PixelWorld.Width) continue;
                    byte mat = world.Get(px, py);
                    if (mat == Mat.Air || mat == Mat.Boss) continue;
                    double hp = world.HpAt(px, py);
                    if (hp > overflow) { world.SetHp(px, py, (float)(hp - overflow)); return; }
                    overflow -= hp;
                    RemovePixel(px, py, mat, false, by);
                }
                if (overflow >= before) return;
            }
        }

        private static BlockType BlockTypeOf(byte mat)
        {
            if (Mat.IsOre(mat)) return BlockType.Ore;
            switch (mat)
            {
                case Mat.Hard: return BlockType.HardRock;
                case Mat.Sand: return BlockType.Sand;
                case Mat.Tnt: return BlockType.Tnt;
                case Mat.Chest: return BlockType.Chest;
                case Mat.Boss: return BlockType.BossRoot;
                case Mat.Grass: return BlockType.Grass;
                case Mat.Water: return BlockType.Water;
                case Mat.Magma: return BlockType.Magma;
                case Mat.SuperHard: return BlockType.SuperHard;
                default: return BlockType.Dirt;
            }
        }

        private void RemovePixel(int x, int y, byte mat, bool byBlast, DrillUnit by)
        {
            world.Clear(x, y);
            int row = y / Tile, col = x / Tile;
            State.AddLayerPixel(GameData.LayerOfRow(row));

            if (Mat.IsOre(mat)) CountOre(row, col, mat, byBlast, by);
            else if (mat == Mat.Chest) CountChest(row, col);
            else
            {
                double value = Balance.OreValue(row, Balance.DirtValue) / PixelsPerTile * ComboMultiplier * IncomeMultiplier * MasteryMultiplier(row);
                Earn(value);
            }

            if (mat == Mat.Tnt && row < State.depth + Balance.ChainReachRows && world.ClaimCharge(row, col) && blastsThisChain < ChainLimit)
            {
                blastsThisChain++;
                State.bestChain = Math.Max(State.bestChain, blastsThisChain);
                pendingBlasts.Enqueue((row, col));
            }
            if (mat == Mat.Magma && world.ClaimCharge(row, col) && blastsThisChain < Balance.MagmaChainLimit)
            {
                blastsThisChain++;
                pendingBlasts.Enqueue((row, col));
            }
            if (mat == Mat.Water) washed = true;

            comboPixels += by.Family == SwiftFamily ? (int)(2 * familyTrait[SwiftFamily]) : 1;
            if (comboPixels < ComboPixels) return;
            comboPixels -= ComboPixels;
            Combo++;
            State.bestCombo = Math.Max(State.bestCombo, Combo);
            comboClock = Balance.ComboWindowSeconds + Effect(SkillEffect.combo_window);
            if (!InFrenzy && Combo >= 2)
            {
                double charge = Balance.FrenzyChargePerBreak * (1 + Effect(SkillEffect.frenzy_charge) / 100);
                FrenzyEnergy = Math.Min(1, FrenzyEnergy + charge);
            }
        }

        private double MasteryMultiplier(int row) => 1 + MasteryValue(GameData.LayerOfRow(row)) / 100;

        private void Earn(double value)
        {
            State.coins += value;
            State.totalIncome += value;
            incomeBuckets[incomeIndex] += value;
        }

        // Ore pays out in pickups: a full lump's worth of pixels is one ore on the way to the top bar.
        private void CountOre(int row, int col, byte mat, bool byBlast, DrillUnit by)
        {
            int ore = mat - Mat.OreBase;
            orePixels.TryGetValue(ore, out int count);
            if (++count < OrePixelsPerPickup)
            {
                orePixels[ore] = count;
                return;
            }
            orePixels[ore] = 0;

            double value = Balance.OreValue(row, OreMultiplier(ore)) * ComboMultiplier * IncomeMultiplier
                * (1 + Effect(SkillEffect.ore_pct) / 100) * (1 + RareBonus(1, 3) / 100) * MasteryMultiplier(row);
            if (byBlast) value *= 1 + Effect(SkillEffect.blast_ore) / 100;
            if (by.Family == MagnetFamily) value *= 2 * familyTrait[MagnetFamily];
            State.ores += 1;
            Earn(value);
            Events.Add(new SimEvent
            {
                Kind = SimEventKind.BlockBroken, Row = row, Col = col, Block = BlockType.Ore, Ore = ore, Amount = value,
            });
            if (GameData.LayerOfRow(row) == Balance.CrystalLayer) ReflectFrom(row, col, by);
        }

        // Crystal throws half a block's strength at everything within two tiles.
        private void ReflectFrom(int row, int col, DrillUnit by)
        {
            if (reflecting) return;
            reflecting = true;
            double damage = Balance.BlockHp(row) * Balance.CrystalReflectRatio;
            Dig(col * Tile + Tile / 2, row * Tile + Tile / 2, Balance.CrystalReflectRadius * Tile, damage, false, false, by);
            reflecting = false;
        }

        // Burst water shoves nearby drills away and carries off loose sand.
        private void WashAround(int cx, int cy, DrillUnit by)
        {
            int reach = Balance.WaterRadius * Tile;
            foreach (var unit in Units)
            {
                double dx = ToPixel(unit.Body.X) - cx;
                if (Math.Abs(dx) > reach || Math.Abs(ToPixel(unit.Body.Y) - cy) > reach) continue;
                unit.Body.VelocityX += (dx >= 0 ? 1 : -1) * Balance.WaterPushSpeed;
            }
            for (int y = cy - reach; y <= cy + reach; y += 2)
                for (int x = cx - reach; x <= cx + reach; x += 2)
                    if (world.Get(x, y) == Mat.Sand) RemovePixel(x, y, Mat.Sand, true, by);
        }

        private static double OreMultiplier(int ore)
        {
            foreach (var layer in GameData.Layers)
                foreach (var def in layer.Ores)
                    if (def.Index == ore) return Math.Pow(def.ValueMult, Balance.OreValuePower);
            return 1;
        }

        private void CountChest(int row, int col)
        {
            if (++chestPixels < ChestPixels) return;
            chestPixels = 0;
            double value = Balance.OreValue(row, Balance.ChestValue) * ComboMultiplier * IncomeMultiplier
                * (1 + Effect(SkillEffect.chest_gold) / 100) * MasteryMultiplier(row);
            Earn(value);
            RollRareDrop(row);
            Events.Add(new SimEvent { Kind = SimEventKind.BlockBroken, Row = row, Col = col, Block = BlockType.Chest, Amount = value });
        }

        private void HitBoss(int row, int col, double amount, bool fromClick, bool crit)
        {
            if (!BossActive) return;
            // chips off the guardian pay like the dirt the swing would have dug; without this a drill
            // that reaches a guardian too weak to beat it earns nothing and can never catch up
            Earn(Balance.OreValue(row, Balance.DirtValue) * Balance.BossChipShare * amount / Balance.BlockHp(row) * IncomeMultiplier);
            amount *= 1 + Effect(SkillEffect.boss_pct) / 100;
            HitBossArmor(ref amount);
            BossHp -= amount;
            Events.Add(new SimEvent
            {
                Kind = SimEventKind.BlockHit, Row = row, Col = col, Block = BlockType.BossRoot,
                Amount = amount, FromClick = fromClick, Crit = crit,
            });
            if (BossHp <= 0) DefeatBoss();
        }

        /// <summary>Player tap at a world pixel. Before auto drilling only tapping a drill does anything.</summary>
        public bool ClickPixel(int x, int y)
        {
            var unit = UnitNear(x, y);
            if (unit != null)
            {
                unit.LastSwing = simTime;
                DigFor(unit, DigRadiusOf(unit), DamageOf(unit.Code) * (1 + Effect(SkillEffect.click_pct) / 100), true, RollCrit(), unit);
                return true;
            }
            if (!AutoDrillUnlocked || !world.Solid(x, y) || x < 0 || x >= PixelWorld.Width) return false;
            Dig(x, y, ClickRadius, ClickDamage, true, RollCrit(), Lead);
            return true;
        }

        private DrillUnit UnitNear(int x, int y)
        {
            DrillUnit best = null;
            int bestDistance = NearDrillPixels * NearDrillPixels;
            foreach (var unit in Units)
            {
                int dx = ToPixel(unit.Body.X) - x;
                int dy = ToPixel(unit.Body.Y) - y;
                if (dy < -NearDrillPixels * 2) continue; // the rock under it counts as tapping it
                int d = dx * dx + Math.Min(dy * dy, Math.Max(0, (dy + NearDrillPixels) * (dy + NearDrillPixels)));
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = unit;
            }
            return best;
        }

        /// <summary>Sets off a charge at a tile; for the demo driver.</summary>
        public void Detonate(int row, int col)
        {
            Explode(row, col, BlastRadius);
            RunBlasts();
        }

        /// <summary>Moves the lead drill to stand on the given row, clearing room for it.</summary>
        public void Teleport(int row, int col)
        {
            int x = ToPixel(col);
            world.CarveRect(x - 24, (row - 3) * Tile, x + 24, row * Tile - 1);
            Lead.Body.Place(col, row - 0.5 - DrillBody.HalfHeight);
            State.depth = row;
            State.maxDepth = Math.Max(State.maxDepth, row);
            State.maxDepthEver = Math.Max(State.maxDepthEver, row);
            State.drillX = Lead.Body.X;
            State.drillY = Lead.Body.Y;
        }

        private void SpawnNearLead(DrillUnit unit)
        {
            int col = EmptiestColumn();
            int floor = Lead.Body.FeetRow - 1;
            int row = floor - SpawnDropRows - unit.Code % 3;
            int x = ToPixel(col);
            world.CarveRect(x - 6, row * Tile, x + 6, (floor + 1) * Tile - 1);
            unit.Body.Place(col, row);
            unit.HitClock = random.NextDouble();
        }

        private void AdvancePhysics(double dt)
        {
            physicsClock += dt;
            while (physicsClock >= DrillBody.StepSeconds)
            {
                physicsClock -= DrillBody.StepSeconds;
                occupied.Clear();
                foreach (var unit in Units) occupied[CellKey(unit.Body.BodyRow, unit.Body.Column)] = unit;

                foreach (var unit in Units)
                {
                    stepper = unit;
                    UpdateFooting(unit);
                    long before = CellKey(unit.Body.BodyRow, unit.Body.Column);
                    double impact = unit.Body.Step(DrillBody.StepSeconds, solidForStepper);
                    long after = CellKey(unit.Body.BodyRow, unit.Body.Column);
                    if (before != after)
                    {
                        occupied.Remove(before);
                        occupied[after] = unit;
                    }
                    if (impact <= 0) continue;
                    Events.Add(new SimEvent
                    {
                        Kind = SimEventKind.Landed, Row = unit.Body.FeetRow, Col = unit.Body.Column,
                        Amount = impact, Lead = unit == Lead,
                    });
                }
            }

            foreach (var unit in Units)
                if (unit.Body.Y > Lead.Body.Y) Lead = unit;

            State.drillX = Lead.Body.X;
            State.drillY = Lead.Body.Y;
            UpdateDepth();
        }

        // Two drills never share a cell: to the one being stepped, the other is ground.
        private bool SolidForStepper(double x, double y)
        {
            if (SolidAtTiles(x, y)) return true;
            long cell = CellKey((int)Math.Floor(y + 0.5), (int)Math.Floor(x + 0.5));
            return occupied.TryGetValue(cell, out var other) && other != stepper;
        }

        // A drill resting on a drill slides to the nearest column it can dig in. With nowhere to go
        // it stays on top as a booster, so no drill is ever idle.
        private void UpdateFooting(DrillUnit unit)
        {
            var body = unit.Body;
            body.Nudge = 0;
            unit.Boosting = false;
            unit.Below = null;
            int feet = body.FeetRow, col = body.Column;
            if (!occupied.TryGetValue(CellKey(feet, col), out var below) || below == unit) return;

            int side = OpenSide(body.BodyRow, feet, col, unit.PrefersLeft);
            if (side != 0)
            {
                body.Nudge = side;
                return;
            }
            unit.Boosting = true;
            unit.Below = below;
        }

        private int OpenSide(int bodyRow, int feet, int col, bool prefersLeft)
        {
            bool leftClear = true, rightClear = true;
            for (int d = 1; d <= SlideSearchColumns; d++)
            {
                leftClear = leftClear && Passable(bodyRow, col - d);
                rightClear = rightClear && Passable(bodyRow, col + d);
                bool left = leftClear && !occupied.ContainsKey(CellKey(feet, col - d));
                bool right = rightClear && !occupied.ContainsKey(CellKey(feet, col + d));
                if (left && right) return prefersLeft ? -1 : 1;
                if (left) return -1;
                if (right) return 1;
                if (!leftClear && !rightClear) return 0;
            }
            return 0;
        }

        private bool Passable(int row, int col) =>
            !SolidAtTiles(col, row) && !occupied.ContainsKey(CellKey(row, col));

        private void Swing(DrillUnit unit)
        {
            unit.LastSwing = simTime;
            bool crit = RollCrit();
            int radius = DigRadiusOf(unit);
            double damage = DamageOf(unit.Code);

            var digger = unit;
            for (int hops = 0; digger.Boosting && digger.Below != null && hops < MaxBoostChain; hops++)
                digger = digger.Below;
            if (digger != unit)
                Events.Add(new SimEvent { Kind = SimEventKind.DrillBoosted, Row = unit.Body.BodyRow, Col = unit.Body.Column });

            DigFor(digger, radius, damage, false, crit, unit);
        }

        // Digs at the nose. If the nose finds nothing, or the body is wedged against rock it is trying
        // to slide past, it clears around the body instead, so a drill never swings at air or stays stuck.
        private void DigFor(DrillUnit digger, int radius, double damage, bool fromClick, bool crit, DrillUnit by)
        {
            if (!digger.Body.Blocked && Dig(ToPixel(digger.Body.TipX), ToPixel(digger.Body.TipY), radius, damage, fromClick, crit, by) > 0) return;
            Dig(ToPixel(digger.Body.X), ToPixel(digger.Body.Y), radius + BodyClearance, damage, fromClick, crit, by);
        }

        private void RunBlasts()
        {
            if (insideBlast) return;
            insideBlast = true;
            while (pendingBlasts.Count > 0)
            {
                var (row, col) = pendingBlasts.Dequeue();
                Explode(row, col, BlastRadius);
            }
            insideBlast = false;
            blastsThisChain = 0;
        }

        private void Explode(int row, int col, int radius)
        {
            Events.Add(new SimEvent { Kind = SimEventKind.Explosion, Row = row, Col = col, Amount = radius });
            int cx = col * Tile + Tile / 2, cy = row * Tile + Tile / 2;
            double budget = BlastDamage(row);
            foreach (var (dx, dy) in ShapeFor(radius * Tile + Tile / 2))
            {
                int x = cx + dx, y = cy + dy;
                if (y < 0 || x < 0 || x >= PixelWorld.Width) continue;
                byte mat = world.Get(x, y);
                if (mat == Mat.Air || mat == Mat.Boss) continue;
                double hp = world.HpAt(x, y);
                if (hp > budget)
                {
                    world.SetHp(x, y, (float)(hp - budget));
                    return;
                }
                budget -= hp;
                RemovePixel(x, y, mat, true, Lead);
            }
        }

        private void AdvanceLoose(double dt)
        {
            sandClock += dt;
            while (sandClock >= Balance.SandPixelStepSeconds)
            {
                sandClock -= Balance.SandPixelStepSeconds;
                world.StepSand(State.depth - Balance.LooseWindowRows, State.depth + Balance.LooseWindowRows);
            }
        }

        private void AdvanceLayerMechanics(double dt)
        {
            simTime += dt;
            AdvanceConvection(dt);
            AdvanceRefill();
            if (overheatLeft > 0) overheatLeft -= dt;
            if (GameData.LayerOfRow(State.depth) != Balance.MohoLayer) return;
            heatClock += dt;
            if (heatClock < Balance.OverheatPeriodSeconds) return;
            heatClock = 0;
            overheatLeft = Balance.OverheatSeconds;
            if (!InFrenzy) Events.Add(new SimEvent { Kind = SimEventKind.Overheated });
        }

        // The rows ahead of the drills creep sideways, alternate bands in opposite directions.
        private void AdvanceConvection(double dt)
        {
            if (GameData.LayerOfRow(State.depth) != Balance.ConvectionLayer) return;
            convectionClock += dt;
            if (convectionClock < Balance.ConvectionSeconds) return;
            convectionClock = 0;
            for (int row = State.depth + 2; row <= State.depth + Balance.LooseWindowRows; row += 2)
            {
                if (IsBossRow(row)) continue;
                world.ShiftBand(row * Tile, (row + 1) * Tile - 1, row / 2 % 2 == 0 ? 1 : -1);
            }
        }

        private void AdvanceRefill()
        {
            while (refills.Count > 0 && refills.Peek().time <= simTime)
            {
                var (x, y, radius, _) = refills.Dequeue();
                bool busy = false;
                foreach (var unit in Units)
                {
                    int dx = ToPixel(unit.Body.X) - x, dy = ToPixel(unit.Body.Y) - y;
                    if (dx * dx + dy * dy < (radius + 8) * (radius + 8)) busy = true;
                }
                if (!busy) world.Refill(x, y, radius);
            }
        }

        private void DefeatBoss()
        {
            var layer = CurrentBoss;
            int bossRow = layer.RowEnd;
            BossActive = false;
            State.bossesDefeated++;
            State.bossesEver = Math.Max(State.bossesEver, State.bossesDefeated);

            Earn(Balance.OreValue(bossRow, Balance.BossRewardValue));

            world.Rebuild(bossRow, Balance.BossRows);
            int cx = ToPixel(Lead.Body.X), cy = (bossRow + 1) * Tile;
            world.CarveCircle(cx, cy, (Balance.BossBreachHalfWidth + 1) * Tile);

            Events.Add(new SimEvent
            {
                Kind = SimEventKind.BossDefeated, Row = bossRow, Col = Lead.Body.Column,
                Amount = Balance.OreValue(bossRow, Balance.BossRewardValue),
            });
        }
    }
}
