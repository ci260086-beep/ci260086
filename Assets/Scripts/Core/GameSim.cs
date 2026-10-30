using System;
using System.Collections.Generic;

namespace DeepDrill.Core
{
    public enum SimEventKind
    {
        BlockHit,
        BlockBroken,
        DepthChanged,
        SkillBought,
        Explosion,
        FrenzyStarted,
        FrenzyEnded,
        Landed,
        BossStarted,
        BossDefeated,
        BossFailed,
        BossArmorBroken,
        Rebirth,
        StratumEntered,
        DrillsChanged,
        DrillMerged,
        RareFound,
        AchievementUnlocked,
    }

    public struct SimEvent
    {
        public SimEventKind Kind;
        public int Row;
        public int Col;
        public BlockType Block;
        public int Ore;
        public double Amount;
        public bool FromClick;
        public bool Crit;
    }

    /// <summary>One drill on the field. Only the strongest few are simulated bodies.</summary>
    public sealed class DrillUnit
    {
        public readonly DrillBody Body = new DrillBody();
        public int Tier;
        public double HitClock;
    }

    /// <summary>
    /// The whole game rule set. Knows nothing about Unity: the presentation layer calls Tick and
    /// Click, then drains <see cref="Events"/> to draw what happened.
    /// </summary>
    public sealed class GameSim
    {
        private const int IncomeWindowSeconds = 10;
        private const int MaxHitsPerUnitPerTick = 8;
        private const int KeptRowsAbove = 30;
        private const double HousekeepingSeconds = 1;

        public readonly GameState State;
        public readonly List<DrillUnit> Units = new List<DrillUnit>();
        public readonly List<SimEvent> Events = new List<SimEvent>();

        private readonly Dictionary<int, Block[]> rows = new Dictionary<int, Block[]>();
        private readonly Queue<(int row, int col)> pendingBlasts = new Queue<(int, int)>();
        private readonly HashSet<long> occupiedCells = new HashSet<long>();
        private readonly double[] incomeBuckets = new double[IncomeWindowSeconds];
        private readonly double[] effects = new double[Enum.GetValues(typeof(SkillEffect)).Length];
        private readonly Func<int, int, bool> solidCheck;
        private readonly Random random;
        private int incomeIndex;
        private double incomeClock;
        private int incomeFilled;
        private double physicsClock;
        private double looseClock;
        private double comboClock;
        private double housekeepingClock;
        private double swarmMultiplier = 1;
        private int blastsThisChain;
        private bool insideBlast;

        public GameSim(GameState state, int? randomSeed = null)
        {
            State = state;
            random = new Random(randomSeed ?? state.seed);
            solidCheck = IsSolid;
            RefreshEffects();

            if (!state.drillPlaced)
            {
                state.drillPlaced = true;
                state.drillX = Balance.StartColumn;
                state.drillY = -0.5 - DrillBody.HalfHeight;
            }
            if (state.DrillsOwned <= 0) state.SetDrillCount(0, 1);
            RebuildUnits();
        }

        public DrillUnit Lead { get; private set; }

        public bool BossActive { get; private set; }
        public double BossHp { get; private set; }
        public double BossArmor { get; private set; }
        public double BossTimeLeft { get; private set; }

        /// <summary>The guardian of the current stratum, or -1 once all of them are down.</summary>
        public int CurrentBossStratum => State.bossesDefeated < Strata.All.Length ? State.bossesDefeated : -1;
        public string BossName => CurrentBossStratum < 0 ? "" : Strata.All[CurrentBossStratum].BossName;
        public int BossRowNow => CurrentBossStratum < 0 ? -1 : Strata.BossRow(CurrentBossStratum);
        public double BossMaxHp
        {
            get
            {
                int s = CurrentBossStratum;
                return s < 0 ? 0 : Balance.BlockHp(Strata.BossRow(s)) * Strata.BossHpScale(s);
            }
        }
        public double BossMaxArmor => CurrentBossStratum < 0 ? 0 : BossMaxHp * Strata.BossArmorRatio(CurrentBossStratum);
        public double BossDuration => Balance.BossSeconds + Effect(SkillEffect.BossTime);

        public int ShardsOnRebirth =>
            (int)Math.Floor(Balance.ShardsForRebirth(State.maxDepth, State.totalIncome) * (1 + Effect(SkillEffect.Shards) / 100));
        public bool CanRebirth => State.AnyBossDown && ShardsOnRebirth > 0;

        public int Combo { get; private set; }
        public double FrenzyEnergy { get; private set; }
        public double FrenzyTimeLeft { get; private set; }
        public bool InFrenzy => FrenzyTimeLeft > 0;
        public bool FrenzyReady => !InFrenzy && FrenzyEnergy >= 1;
        public double FrenzyDuration => Balance.FrenzySeconds + Effect(SkillEffect.FrenzyTime);
        public double SwarmMultiplier => swarmMultiplier;

        public double ComboMultiplier =>
            1 + Math.Min(Balance.ComboBonusMax + Effect(SkillEffect.ComboCap) / 100,
                Balance.ComboBonusPerStep * (Combo / Balance.ComboStep));

        public double Effect(SkillEffect effect) => effects[(int)effect];

        private double GlobalDamageMultiplier =>
            (1 + Effect(SkillEffect.Damage) / 100)
            * (1 + Balance.ShardBonus * State.shards)
            * (1 + Balance.AchievementBonus * State.achievements.Count)
            * ComboMultiplier
            * (InFrenzy ? Balance.FrenzyDamageMultiplier : 1);

        /// <summary>What one swing of a drill of this tier does right now, swarm share included.</summary>
        public double DamageOf(int tier)
        {
            double merge = 1 + Effect(SkillEffect.MergeBonus) / 100 * tier;
            return DrillTiers.Damage(tier) * merge * GlobalDamageMultiplier * swarmMultiplier;
        }

        public double ClickDamage => DamageOf(Lead.Tier);

        public double HitsPerSecond =>
            DrillTiers.BaseHitsPerSecond * (1 + Effect(SkillEffect.Speed) / 100)
            * (InFrenzy ? Balance.FrenzySpeedMultiplier : 1);

        /// <summary>Combined damage per second of every owned drill.</summary>
        public double TotalDps
        {
            get
            {
                double sum = 0;
                for (int t = 0; t < DrillTiers.Count; t++)
                    sum += State.DrillCount(t) * DrillTiers.Damage(t);
                return sum * GlobalDamageMultiplier * HitsPerSecond;
            }
        }

        public int BlastRadius => Balance.BaseBlastRadius + (int)Effect(SkillEffect.BlastRadius);

        private double TntChance => Balance.TntChance + Effect(SkillEffect.TntRate) / 100;
        private int ChainLimit => Balance.BaseChainLimit + (int)Effect(SkillEffect.BlastChain);
        private double CritChance => Balance.CritChance + Effect(SkillEffect.CritChance) / 100;
        private double CritMultiplier => Balance.CritMultiplier + Effect(SkillEffect.CritDamage) / 100;

        private double IncomeMultiplier =>
            (1 + Effect(SkillEffect.CoinValue) / 100)
            * (1 + Balance.RareItemBonus * State.rareItems.Count)
            * (1 + Effect(SkillEffect.DepthValue) / 100 * State.depth / Strata.RowsPerStratum);

        public Block[] Row(int row)
        {
            if (rows.TryGetValue(row, out var blocks)) return blocks;

            blocks = new Block[Balance.Columns];
            int leadCol = Lead != null ? Lead.Body.Column : (int)Math.Round(State.drillX);
            for (int col = 0; col < Balance.Columns; col++)
            {
                var block = new Block();
                if (IsBossRow(row))
                {
                    block.Type = BlockType.BossRoot;
                    block.Hp = block.MaxHp = double.MaxValue;
                }
                else
                {
                    WorldGen.Fill(block, State.seed, row, col, TntChance);
                    // only a reloaded run meets rows above the drill here: reopen the passage it stands in
                    if (row < State.depth && col == leadCol) block.Hp = 0;
                }
                blocks[col] = block;
            }
            rows[row] = blocks;
            return blocks;
        }

        private bool IsBossRow(int row)
        {
            for (int i = State.bossesDefeated; i < Strata.All.Length; i++)
            {
                int start = Strata.BossRow(i);
                if (row >= start && row < start + Balance.BossRows) return true;
            }
            return false;
        }

        /// <summary>The sky is open; the sides of the world are unbreakable walls.</summary>
        public bool IsSolid(int row, int col)
        {
            if (row < 0) return false;
            if (col < 0 || col >= Balance.Columns) return true;
            return Row(row)[col].Alive;
        }

        public void Tick(double dt)
        {
            AdvanceIncomeWindow(dt);
            AdvanceCombo(dt);
            AdvanceFrenzy(dt);
            AdvanceLoose(dt);
            AdvancePhysics(dt);
            AdvanceBoss(dt);
            AdvanceHits(dt);
            AdvanceHousekeeping(dt);
        }

        /// <summary>Player tap on any block. Returns false when nothing was there to hit.</summary>
        public bool Click(int row, int col)
        {
            if (row < 0 || col < 0 || col >= Balance.Columns) return false;
            var block = Row(row)[col];
            if (!block.Alive) return false;
            if (block.Type == BlockType.BossRoot && !BossActive) return false;
            Strike(row, col, ClickDamage, true, RollCrit());
            return true;
        }

        /// <summary>Removes a block with no reward or effects.</summary>
        public void Carve(int row, int col)
        {
            if (row >= 0 && col >= 0 && col < Balance.Columns) Row(row)[col].Hp = 0;
        }

        /// <summary>Moves the lead drill to stand on the given row, clearing room for it.</summary>
        public void Teleport(int row, int col)
        {
            for (int r = row - 3; r < row; r++)
                for (int c = col - 2; c <= col + 2; c++)
                    Carve(r, c);
            Lead.Body.Place(col, row - 0.5 - DrillBody.HalfHeight);
            State.depth = row;
            State.maxDepth = Math.Max(State.maxDepth, row);
            State.maxDepthEver = Math.Max(State.maxDepthEver, row);
            State.drillX = Lead.Body.X;
            State.drillY = Lead.Body.Y;
        }

        public double DrillCost(int tier) =>
            DrillTiers.Cost(tier, State.DrillCount(tier)) * (1 - Math.Min(0.5, Effect(SkillEffect.DrillDiscount) / 100));

        public bool DrillUnlocked(int tier) =>
            State.maxDepthEver >= DrillTiers.UnlockDepth(tier) || State.DrillCount(tier) > 0;

        public bool CanBuyDrill(int tier) => DrillUnlocked(tier) && State.coins >= DrillCost(tier);

        public bool BuyDrill(int tier)
        {
            if (!CanBuyDrill(tier)) return false;
            State.coins -= DrillCost(tier);
            State.SetDrillCount(tier, State.DrillCount(tier) + 1);
            RebuildUnits();
            Events.Add(new SimEvent { Kind = SimEventKind.DrillsChanged, Amount = tier });
            return true;
        }

        public bool CanMerge(int tier) =>
            tier + 1 < DrillTiers.Count && State.DrillCount(tier) >= Balance.MergeCost;

        public bool Merge(int tier)
        {
            if (!CanMerge(tier)) return false;
            State.SetDrillCount(tier, State.DrillCount(tier) - Balance.MergeCost);
            State.SetDrillCount(tier + 1, State.DrillCount(tier + 1) + 1);
            State.mergesEver++;
            RebuildUnits();
            Events.Add(new SimEvent { Kind = SimEventKind.DrillMerged, Amount = tier + 1 });
            return true;
        }

        public bool StartFrenzy()
        {
            if (!FrenzyReady) return false;
            FrenzyEnergy = 0;
            FrenzyTimeLeft = FrenzyDuration;
            Events.Add(new SimEvent { Kind = SimEventKind.FrenzyStarted });
            return true;
        }

        public bool SkillDepthReached(SkillDef skill) => State.maxDepthEver >= skill.UnlockDepth;

        public bool IsUnlocked(SkillDef skill) =>
            SkillDepthReached(skill) && (skill.RequiresId == null || State.LevelOf(skill.RequiresId) > 0);

        public bool CanBuy(SkillDef skill)
        {
            int level = State.LevelOf(skill.Id);
            return IsUnlocked(skill) && level < skill.MaxLevel && State.coins >= skill.CostAt(level);
        }

        public bool Buy(string skillId)
        {
            var skill = SkillCatalog.Get(skillId);
            if (skill == null || !CanBuy(skill)) return false;
            int level = State.LevelOf(skillId);
            State.coins -= skill.CostAt(level);
            State.SetLevel(skillId, level + 1);
            RefreshEffects();
            Events.Add(new SimEvent { Kind = SimEventKind.SkillBought, Amount = level + 1 });
            return true;
        }

        /// <summary>Sets a skill directly and recomputes its effect; for tests and the demo driver.</summary>
        public void GrantSkill(string skillId, int level)
        {
            State.SetLevel(skillId, level);
            RefreshEffects();
        }

        /// <summary>Credits offline income and returns how much was added.</summary>
        public double ApplyOffline(long nowUnixSeconds)
        {
            if (State.lastSaveUnixSeconds <= 0) return 0;
            double earned = Balance.OfflineEarnings(State.incomePerSecond, nowUnixSeconds - State.lastSaveUnixSeconds,
                Effect(SkillEffect.Offline) / 100);
            State.coins += earned;
            State.totalIncome += earned;
            return earned;
        }

        /// <summary>Trades the run for star core shards. Shards and lifetime records are all that stay.</summary>
        public bool Rebirth()
        {
            if (!CanRebirth) return false;
            int gained = ShardsOnRebirth;
            State.shards += gained;
            State.rebirths++;

            State.coins = 0;
            State.ores = 0;
            State.depth = 0;
            State.maxDepth = 0;
            State.totalIncome = 0;
            State.incomePerSecond = 0;
            State.skills.Clear();
            State.drillCounts.Clear();
            State.bossesDefeated = 0;
            State.seed = random.Next(1, int.MaxValue);
            State.drillX = Balance.StartColumn;
            State.drillY = -0.5 - DrillBody.HalfHeight;

            rows.Clear();
            pendingBlasts.Clear();
            Array.Clear(incomeBuckets, 0, incomeBuckets.Length);
            incomeFilled = 0;
            incomeClock = physicsClock = comboClock = looseClock = 0;
            Combo = 0;
            FrenzyEnergy = FrenzyTimeLeft = 0;
            BossActive = false;

            RefreshEffects();
            Units.Clear();
            Lead = null;
            State.SetDrillCount(0, 1);
            RebuildUnits();
            Events.Add(new SimEvent { Kind = SimEventKind.Rebirth, Amount = gained });
            return true;
        }

        private void RefreshEffects()
        {
            Array.Clear(effects, 0, effects.Length);
            foreach (var level in State.skills)
            {
                var skill = SkillCatalog.Get(level.id);
                if (skill != null) effects[(int)skill.Effect] += skill.PerLevel * level.level;
            }
        }

        // Keeps the bodies in step with the owned counts, strongest first, without moving drills
        // that stay on the field. Owned drills beyond the limit lend their power to the others.
        private void RebuildUnits()
        {
            var wanted = new List<int>();
            for (int t = DrillTiers.Count - 1; t >= 0 && wanted.Count < Balance.MaxSimulatedDrills; t--)
            {
                int take = Math.Min(State.DrillCount(t), Balance.MaxSimulatedDrills - wanted.Count);
                for (int i = 0; i < take; i++) wanted.Add(t);
            }

            var spare = new List<DrillUnit>(Units);
            var keep = new List<DrillUnit>();
            foreach (int tier in wanted)
            {
                int found = spare.FindIndex(u => u.Tier == tier);
                if (found >= 0)
                {
                    keep.Add(spare[found]);
                    spare.RemoveAt(found);
                }
                else
                {
                    var unit = new DrillUnit { Tier = tier };
                    keep.Add(unit);
                    if (spare.Count > 0)
                    {
                        // a merged drill takes over a spent one's spot
                        unit.Body.Place(spare[0].Body.X, spare[0].Body.Y);
                        spare.RemoveAt(0);
                    }
                    else if (Lead == null)
                    {
                        unit.Body.Place(State.drillX, State.drillY);
                    }
                    else
                    {
                        SpawnNearLead(unit);
                    }
                }
            }

            Units.Clear();
            Units.AddRange(keep);
            if (Lead == null || !Units.Contains(Lead)) Lead = Units.Count > 0 ? Units[0] : null;

            double ownedPower = 0, activePower = 0;
            for (int t = 0; t < DrillTiers.Count; t++) ownedPower += State.DrillCount(t) * DrillTiers.Damage(t);
            foreach (var unit in Units) activePower += DrillTiers.Damage(unit.Tier);
            swarmMultiplier = activePower > 0 ? ownedPower / activePower : 1;
        }

        private void SpawnNearLead(DrillUnit unit)
        {
            int col = Lead.Body.Column + (int)Math.Round((random.NextDouble() * 2 - 1) * Balance.SpawnSpreadColumns);
            col = Math.Max(1, Math.Min(Balance.Columns - 2, col));
            int row = Math.Max(0, Lead.Body.FeetRow - 1);
            // the drill drops in from above the stand-off line, opening its own small pocket
            if (row > 0) Carve(row - 1, col);
            Carve(row, col);
            unit.Body.Place(col, row - DrillBody.HalfHeight);
            unit.HitClock = random.NextDouble();
        }

        private void AdvancePhysics(double dt)
        {
            physicsClock += dt;
            while (physicsClock >= DrillBody.StepSeconds)
            {
                physicsClock -= DrillBody.StepSeconds;
                foreach (var unit in Units)
                {
                    double impact = unit.Body.Step(DrillBody.StepSeconds, solidCheck);
                    if (impact <= 0) continue;
                    Events.Add(new SimEvent
                    {
                        Kind = SimEventKind.Landed, Row = unit.Body.FeetRow, Col = unit.Body.Column, Amount = impact,
                        Crit = unit == Lead,
                    });
                }
            }

            foreach (var unit in Units)
                if (unit.Body.Y > Lead.Body.Y) Lead = unit;

            State.drillX = Lead.Body.X;
            State.drillY = Lead.Body.Y;
            UpdateDepth();
        }

        private void AdvanceHits(double dt)
        {
            double hits = dt * HitsPerSecond;
            foreach (var unit in Units)
            {
                unit.HitClock += hits;
                int swings = 0;
                while (unit.HitClock >= 1 && swings < MaxHitsPerUnitPerTick)
                {
                    unit.HitClock -= 1;
                    swings++;
                    Swing(unit);
                }
                if (unit.HitClock > 1) unit.HitClock = 1;
            }
        }

        private void Swing(DrillUnit unit)
        {
            if (!FindTarget(unit.Body, out int row, out int col)) return;
            double damage = DamageOf(unit.Tier);
            bool crit = RollCrit();
            Strike(row, col, damage, false, crit);

            double pierce = Effect(SkillEffect.Pierce) / 100;
            if (pierce > 0 && IsSolid(row + 1, col) && col >= 0 && col < Balance.Columns)
                Strike(row + 1, col, damage * pierce, false, crit);
        }

        /// <summary>The block the tip points at, or else the nearest one the drill can reach.</summary>
        public bool FindTarget(DrillBody body, out int row, out int col)
        {
            row = RoundToInt(body.TipY);
            col = RoundToInt(body.TipX);
            if (row >= 0 && col >= 0 && col < Balance.Columns && Row(row)[col].Alive) return true;

            double best = double.MaxValue;
            int foundRow = 0, foundCol = 0;
            for (int r = Math.Max(0, row - 1); r <= row + 1; r++)
            {
                for (int c = Math.Max(0, col - 1); c <= Math.Min(Balance.Columns - 1, col + 1); c++)
                {
                    if (!Row(r)[c].Alive) continue;
                    double dr = r - body.TipY, dc = c - body.TipX;
                    double d = dr * dr + dc * dc;
                    if (d >= best) continue;
                    best = d;
                    foundRow = r;
                    foundCol = c;
                }
            }
            row = foundRow;
            col = foundCol;
            return best < double.MaxValue;
        }

        private bool RollCrit() => random.NextDouble() < CritChance;

        private void Strike(int row, int col, double damage, bool fromClick, bool crit)
        {
            var block = Row(row)[col];
            double amount = damage * (crit ? CritMultiplier : 1);
            if (block.Type == BlockType.HardRock)
                amount *= 1 + Effect(SkillEffect.HardDamage) / 100;

            if (block.Type == BlockType.BossRoot)
            {
                if (!BossActive) return;
                amount *= 1 + Effect(SkillEffect.BossDamage) / 100;
                HitBossArmor(ref amount);
                BossHp -= amount;
                Events.Add(new SimEvent
                {
                    Kind = SimEventKind.BlockHit, Row = row, Col = col, Block = block.Type,
                    Amount = amount, FromClick = fromClick, Crit = crit,
                });
                if (BossHp <= 0) DefeatBoss();
                return;
            }

            block.Hp -= amount;
            Events.Add(new SimEvent
            {
                Kind = SimEventKind.BlockHit, Row = row, Col = col, Block = block.Type,
                Amount = amount, FromClick = fromClick, Crit = crit,
            });
            if (block.Hp > 0) return;

            Break(row, col, block, fromClick, false);
            RunBlasts();
        }

        private void Break(int row, int col, Block block, bool fromClick, bool byBlast)
        {
            block.Hp = 0;
            bool isOre = block.Type == BlockType.Ore;
            double value = WorldGen.ValueOf(block, row) * ComboMultiplier * IncomeMultiplier;
            if (isOre)
            {
                value *= 1 + Effect(SkillEffect.OreValue) / 100;
                if (byBlast) value *= 1 + Effect(SkillEffect.BlastLoot) / 100;
                State.ores += 1;
                State.oresEver += 1;
            }
            if (block.Type == BlockType.Chest) value *= 1 + Effect(SkillEffect.ChestValue) / 100;

            State.coins += value;
            State.totalIncome += value;
            incomeBuckets[incomeIndex] += value;

            Combo++;
            comboClock = Balance.ComboWindowSeconds + Effect(SkillEffect.ComboWindow);
            if (!InFrenzy && Combo >= 2)
            {
                double charge = Balance.FrenzyChargePerBreak * (1 + Effect(SkillEffect.FrenzyCharge) / 100);
                FrenzyEnergy = Math.Min(1, FrenzyEnergy + charge);
            }

            Events.Add(new SimEvent
            {
                Kind = SimEventKind.BlockBroken, Row = row, Col = col, Block = block.Type, Ore = block.Ore,
                Amount = value, FromClick = fromClick,
            });

            MaybeDropRare(row);

            if (block.Type == BlockType.Tnt && blastsThisChain < ChainLimit)
            {
                blastsThisChain++;
                pendingBlasts.Enqueue((row, col));
            }
        }

        private void MaybeDropRare(int row)
        {
            if (random.NextDouble() >= Balance.RareDropChance) return;
            int stratum = Strata.OfDepth(row);
            for (int i = 0; i < RareItems.All.Count; i++)
            {
                var item = RareItems.All[i];
                if (item.Stratum != stratum || State.rareItems.Contains(item.Id)) continue;
                State.rareItems.Add(item.Id);
                Events.Add(new SimEvent { Kind = SimEventKind.RareFound, Amount = i });
                return;
            }
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
            for (int r = Math.Max(0, row - radius); r <= row + radius; r++)
            {
                for (int c = Math.Max(0, col - radius); c <= Math.Min(Balance.Columns - 1, col + radius); c++)
                {
                    int dr = r - row, dc = c - col;
                    if (dr * dr + dc * dc > radius * radius + 1) continue;
                    var block = Row(r)[c];
                    if (block.Alive && block.Type != BlockType.BossRoot) Break(r, c, block, false, true);
                }
            }
        }

        private void UpdateDepth()
        {
            int feet = Lead.Body.FeetRow;
            if (feet <= State.depth) return;
            for (int row = State.depth; row < feet; row++) rows.Remove(row - KeptRowsAbove);
            int stratum = Strata.OfDepth(feet);
            bool entered = stratum != Strata.OfDepth(State.depth);
            State.depth = feet;
            State.maxDepth = Math.Max(State.maxDepth, feet);
            State.maxDepthEver = Math.Max(State.maxDepthEver, feet);
            Events.Add(new SimEvent { Kind = SimEventKind.DepthChanged, Row = feet });
            if (entered) Events.Add(new SimEvent { Kind = SimEventKind.StratumEntered, Amount = stratum });
        }

        // Loose blocks drop one cell per step, bottom row first so a whole column falls together.
        // They settle on the body of a drill rather than through it.
        private void AdvanceLoose(double dt)
        {
            looseClock += dt;
            while (looseClock >= Balance.LooseStepSeconds)
            {
                looseClock -= Balance.LooseStepSeconds;
                StepLoose();
            }
        }

        private void StepLoose()
        {
            occupiedCells.Clear();
            foreach (var unit in Units)
                occupiedCells.Add(CellKey(RoundToInt(unit.Body.Y), unit.Body.Column));

            int top = Math.Max(0, State.depth - Balance.LooseWindowRows);
            for (int row = State.depth + Balance.LooseWindowRows; row >= top; row--)
            {
                if (!rows.TryGetValue(row, out var upper) || !rows.TryGetValue(row + 1, out var lower)) continue;
                for (int col = 0; col < Balance.Columns; col++)
                {
                    if (upper[col].Type != BlockType.Sand || !upper[col].Alive) continue;
                    if (lower[col].Alive || occupiedCells.Contains(CellKey(row + 1, col))) continue;
                    var moved = upper[col];
                    upper[col] = lower[col];
                    lower[col] = moved;
                }
            }
        }

        private static long CellKey(int row, int col) => row * 64L + col;

        private void AdvanceHousekeeping(double dt)
        {
            housekeepingClock += dt;
            if (housekeepingClock < HousekeepingSeconds) return;
            housekeepingClock = 0;
            BringBackStragglers();
            CheckAchievements();
        }

        // Drills that fall behind or wander off screen rejoin the lead, so the view stays busy.
        private void BringBackStragglers()
        {
            foreach (var unit in Units)
            {
                if (unit == Lead) continue;
                bool behind = Lead.Body.Y - unit.Body.Y > Balance.StragglerRows;
                bool away = Math.Abs(Lead.Body.X - unit.Body.X) > Balance.StragglerColumns;
                if (behind || away) SpawnNearLead(unit);
            }
        }

        private void CheckAchievements()
        {
            for (int i = 0; i < AchievementCatalog.All.Count; i++)
            {
                var a = AchievementCatalog.All[i];
                if (State.achievements.Contains(a.Id) || a.Progress(State) < a.Goal) continue;
                State.achievements.Add(a.Id);
                Events.Add(new SimEvent { Kind = SimEventKind.AchievementUnlocked, Amount = i });
            }
        }

        private void AdvanceBoss(double dt)
        {
            int stratum = CurrentBossStratum;
            if (stratum < 0) return;
            if (!BossActive)
            {
                if (Lead.Body.FeetRow < Strata.BossRow(stratum) || !Lead.Body.Grounded) return;
                BossActive = true;
                ResetBoss();
                Events.Add(new SimEvent { Kind = SimEventKind.BossStarted });
                return;
            }

            BossTimeLeft -= dt;
            if (BossTimeLeft > 0) return;
            // it recovers: the player gets another attempt, usually after upgrading
            ResetBoss();
            Events.Add(new SimEvent { Kind = SimEventKind.BossFailed });
        }

        private void ResetBoss()
        {
            BossHp = BossMaxHp;
            BossArmor = BossMaxArmor;
            BossTimeLeft = BossDuration;
        }

        // Armour soaks the full hit and lets a reduced share through until it is gone.
        private void HitBossArmor(ref double amount)
        {
            if (BossArmor <= 0) return;
            BossArmor -= amount;
            amount *= Balance.BossArmorDamageRatio;
            if (BossArmor > 0) return;
            BossArmor = 0;
            Events.Add(new SimEvent { Kind = SimEventKind.BossArmorBroken });
        }

        private void DefeatBoss()
        {
            int stratum = CurrentBossStratum;
            int bossRow = Strata.BossRow(stratum);
            BossActive = false;
            State.bossesDefeated++;
            State.bossesEver = Math.Max(State.bossesEver, State.bossesDefeated);

            double value = Balance.OreValue(bossRow, Balance.BossRewardValue);
            State.coins += value;
            State.totalIncome += value;
            incomeBuckets[incomeIndex] += value;

            int col = Lead.Body.Column;
            for (int row = bossRow; row < bossRow + Balance.BossRows; row++)
            {
                rows.Remove(row); // the rest of the row grows back as ordinary ground
                for (int c = col - Balance.BossBreachHalfWidth; c <= col + Balance.BossBreachHalfWidth; c++)
                    Carve(row, c);
            }

            Events.Add(new SimEvent
            {
                Kind = SimEventKind.BossDefeated, Row = bossRow, Col = col, Amount = value,
            });
        }

        private void AdvanceCombo(double dt)
        {
            if (Combo == 0) return;
            comboClock -= dt;
            if (comboClock <= 0) Combo = 0;
        }

        private void AdvanceFrenzy(double dt)
        {
            if (!InFrenzy) return;
            FrenzyTimeLeft -= dt;
            if (FrenzyTimeLeft > 0) return;

            FrenzyTimeLeft = 0;
            Events.Add(new SimEvent { Kind = SimEventKind.FrenzyEnded });
            int burst = (int)Effect(SkillEffect.FrenzyBurst);
            if (burst > 0)
            {
                Explode(RoundToInt(Lead.Body.TipY), RoundToInt(Lead.Body.TipX), Balance.FrenzyBurstRadius + burst);
                RunBlasts();
            }
        }

        private void AdvanceIncomeWindow(double dt)
        {
            incomeClock += dt;
            while (incomeClock >= 1)
            {
                incomeClock -= 1;
                incomeFilled = Math.Min(incomeFilled + 1, IncomeWindowSeconds - 1);
                incomeIndex = (incomeIndex + 1) % IncomeWindowSeconds;
                incomeBuckets[incomeIndex] = 0;

                double sum = 0;
                foreach (double b in incomeBuckets) sum += b;
                State.incomePerSecond = sum / Math.Max(1, incomeFilled);
            }
        }

        private static int RoundToInt(double v) => (int)Math.Floor(v + 0.5);
    }
}
