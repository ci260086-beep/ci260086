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

        private static readonly Dictionary<int, (int dx, int dy)[]> DigShapes = new Dictionary<int, (int, int)[]>();

        private PixelWorld world;
        private readonly Dictionary<int, int> orePixels = new Dictionary<int, int>();
        private readonly Func<double, double, bool> solidAt;
        private int comboPixels;
        private int chestPixels;
        private double sandClock;

        public PixelWorld World => world;

        private void ResetWorld()
        {
            world = new PixelWorld(State.seed, TntChance, ChestChance, IsBossRow);
            orePixels.Clear();
            comboPixels = chestPixels = 0;
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
                    HitBoss(y / Tile, x / Tile, budget * (heavy ? 1.5 : 1), fromClick, crit);
                    budget = 0;
                    break;
                }

                double rate = mat == Mat.Hard ? (1 + Effect(SkillEffect.hard_pct) / 100) * (heavy ? 3 : 1) : 1;
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

            PassOverflow(cx, cy, budget, by);
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
                default: return BlockType.Dirt;
            }
        }

        private void RemovePixel(int x, int y, byte mat, bool byBlast, DrillUnit by)
        {
            world.Clear(x, y);
            int row = y / Tile, col = x / Tile;

            if (Mat.IsOre(mat)) CountOre(row, col, mat, byBlast, by);
            else if (mat == Mat.Chest) CountChest(row, col);
            else
            {
                double value = Balance.OreValue(row, Balance.DirtValue) / PixelsPerTile * ComboMultiplier * IncomeMultiplier;
                Earn(value);
            }

            if (mat == Mat.Tnt && world.ClaimCharge(row, col) && blastsThisChain < ChainLimit)
            {
                blastsThisChain++;
                State.bestChain = Math.Max(State.bestChain, blastsThisChain);
                pendingBlasts.Enqueue((row, col));
            }

            comboPixels += by.Family == SwiftFamily ? 2 : 1;
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
                * (1 + Effect(SkillEffect.ore_pct) / 100) * (1 + RareBonus(1, 3) / 100);
            if (byBlast) value *= 1 + Effect(SkillEffect.blast_ore) / 100;
            if (by.Family == MagnetFamily) value *= 2;
            State.ores += 1;
            Earn(value);
            Events.Add(new SimEvent
            {
                Kind = SimEventKind.BlockBroken, Row = row, Col = col, Block = BlockType.Ore, Ore = ore, Amount = value,
            });
        }

        private static double OreMultiplier(int ore)
        {
            foreach (var layer in GameData.Layers)
                foreach (var def in layer.Ores)
                    if (def.Index == ore) return def.ValueMult;
            return 1;
        }

        private void CountChest(int row, int col)
        {
            if (++chestPixels < ChestPixels) return;
            chestPixels = 0;
            double value = Balance.OreValue(row, Balance.ChestValue) * ComboMultiplier * IncomeMultiplier
                * (1 + Effect(SkillEffect.chest_gold) / 100);
            Earn(value);
            RollRareDrop(row);
            Events.Add(new SimEvent { Kind = SimEventKind.BlockBroken, Row = row, Col = col, Block = BlockType.Chest, Amount = value });
        }

        private void HitBoss(int row, int col, double amount, bool fromClick, bool crit)
        {
            if (!BossActive) return;
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
                Dig(ToPixel(unit.Body.TipX), ToPixel(unit.Body.TipY), DigRadiusOf(unit),
                    DamageOf(unit.Code) * (1 + Effect(SkillEffect.click_pct) / 100), true, RollCrit(), unit);
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
                foreach (var unit in Units)
                {
                    long cell = CellKey(unit.Body.BodyRow, unit.Body.Column);
                    unit.Body.Nudge = 0;
                    if (occupied.ContainsKey(cell))
                        unit.Body.Nudge = unit.PrefersLeft ? -1 : 1; // drills share rock but drift apart
                    else
                        occupied[cell] = unit;
                }

                foreach (var unit in Units)
                {
                    double impact = unit.Body.Step(DrillBody.StepSeconds, solidAt);
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

        private void Swing(DrillUnit unit)
        {
            unit.LastSwing = simTime;
            bool crit = RollCrit();
            int radius = DigRadiusOf(unit);
            double damage = DamageOf(unit.Code);
            if (Dig(ToPixel(unit.Body.TipX), ToPixel(unit.Body.TipY), radius, damage, false, crit, unit) > 0) return;
            // the nose found nothing: clear whatever is holding the body so no drill swings at air
            Dig(ToPixel(unit.Body.X), ToPixel(unit.Body.Y), radius + BodyClearance, damage, false, crit, unit);
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
            int r = radius * Tile + Tile / 2;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r) continue;
                    if (y < 0 || x < 0 || x >= PixelWorld.Width) continue;
                    byte mat = world.Get(x, y);
                    if (mat == Mat.Air || mat == Mat.Boss) continue;
                    RemovePixel(x, y, mat, true, Lead);
                }
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
            if (overheatLeft > 0) overheatLeft -= dt;
            if (GameData.LayerOfRow(State.depth) != Balance.MohoLayer) return;
            heatClock += dt;
            if (heatClock < Balance.OverheatPeriodSeconds) return;
            heatClock = 0;
            overheatLeft = Balance.OverheatSeconds;
            if (!InFrenzy) Events.Add(new SimEvent { Kind = SimEventKind.Overheated });
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
