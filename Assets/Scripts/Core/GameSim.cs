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
        LayerEntered,
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
        public bool Lead;
    }

    /// <summary>One drill on the field. Up to the simulation limit each owned drill gets a body.</summary>
    public sealed class DrillUnit
    {
        public readonly DrillBody Body = new DrillBody();
        public int Code;
        public double HitClock;

        public int Family => DrillCode.Family(Code);
        public int Tier => DrillCode.Tier(Code);
    }

    /// <summary>
    /// The whole game rule set. Knows nothing about Unity: the presentation layer calls Tick and
    /// Click, then drains <see cref="Events"/> to draw what happened. Needs GameData loaded.
    /// </summary>
    public sealed class GameSim
    {
        private const int IncomeWindowSeconds = 10;
        private const int MaxSwingsPerUnitPerTick = 8;
        private const int KeptRowsAbove = 30;
        private const double HousekeepingSeconds = 1;
        private const int SwarmPerDrills = 10;
        private const int AutoBuysPerTick = 5;
        private const int MaxSpawnRowsUp = 40;
        private const int SwiftFamily = 1;
        private const int HeavyFamily = 2;
        private const int BlastFamily = 3;
        private const int MagnetFamily = 4;

        public readonly GameState State;
        public readonly List<DrillUnit> Units = new List<DrillUnit>();
        public readonly List<SimEvent> Events = new List<SimEvent>();

        private readonly Dictionary<int, Block[]> rows = new Dictionary<int, Block[]>();
        private readonly Queue<(int row, int col)> pendingBlasts = new Queue<(int, int)>();
        private readonly Dictionary<long, DrillUnit> occupied = new Dictionary<long, DrillUnit>();
        private readonly double[] incomeBuckets = new double[IncomeWindowSeconds];
        private readonly double[] effects = new double[Enum.GetValues(typeof(SkillEffect)).Length];
        private readonly double[] layerBonus;
        private readonly Func<int, int, bool> solidForStepper;
        private readonly Random random;
        private DrillUnit stepper;
        private int incomeIndex;
        private double incomeClock;
        private int incomeFilled;
        private double physicsClock;
        private double looseClock;
        private double comboClock;
        private double housekeepingClock;
        private double swarmMultiplier = 1;
        private double drillBonus = 1;
        private int blastsThisChain;
        private bool insideBlast;

        public GameSim(GameState state, int? randomSeed = null)
        {
            State = state;
            random = new Random(randomSeed ?? state.seed);
            solidForStepper = SolidForStepper;
            layerBonus = new double[GameData.Layers.Count];
            RefreshEffects();

            if (!state.drillPlaced)
            {
                state.drillPlaced = true;
                state.drillX = Balance.StartColumn;
                state.drillY = -0.5 - DrillBody.HalfHeight;
            }
            if (state.drills.Count == 0) state.drills.Add(DrillCode.Make(0, 0));
            RebuildUnits();
        }

        public DrillUnit Lead { get; private set; }

        public bool BossActive { get; private set; }
        public double BossHp { get; private set; }
        public double BossArmor { get; private set; }
        public double BossTimeLeft { get; private set; }

        /// <summary>The layer whose guardian is next, or null once all of them are down.</summary>
        public LayerDef CurrentBoss =>
            State.bossesDefeated < GameData.Layers.Count ? GameData.Layers[State.bossesDefeated] : null;
        public double BossMaxHp => CurrentBoss == null ? 0 : Balance.BlockHp(CurrentBoss.RowEnd) * CurrentBoss.BossHpMult;
        public double BossMaxArmor => CurrentBoss == null || CurrentBoss.Index == 0 ? 0 : BossMaxHp * Balance.BossArmorRatio;
        public double BossDuration => CurrentBoss?.BossSeconds ?? 0;

        public int ReachedLayer => GameData.LayerOfRow(State.maxDepthEver);

        public int ShardsOnRebirth =>
            (int)Math.Floor(Balance.ShardsForRebirth(State.maxDepth, State.totalIncome)
                * (1 + (Effect(SkillEffect.shard_pct) + 5 * AchievementCount("rebirth_")) / 100));
        public bool CanRebirth => State.AnyBossDown && ShardsOnRebirth > 0;

        public int Combo { get; private set; }
        public double FrenzyEnergy { get; private set; }
        public double FrenzyTimeLeft { get; private set; }
        public bool InFrenzy => FrenzyTimeLeft > 0;
        public bool FrenzyReady => !InFrenzy && FrenzyEnergy >= 1;
        public double FrenzyDuration => Balance.FrenzySeconds + Effect(SkillEffect.frenzy_time);
        public double SwarmMultiplier => swarmMultiplier;

        public double ComboMultiplier =>
            1 + Math.Min(Balance.ComboBonusMax + (Effect(SkillEffect.combo_cap) + 10 * AchievementCount("combo_")) / 100,
                Balance.ComboBonusPerStep * (Combo / Balance.ComboStep));

        public double Effect(SkillEffect effect) => effects[(int)effect];

        private int AchievementCount(string prefix)
        {
            int n = 0;
            foreach (string id in State.achievements)
                if (id.StartsWith(prefix, StringComparison.Ordinal)) n++;
            return n;
        }

        // Each rare's bonus grows 50% per duplicate, up to ten duplicates.
        private double RareBonus(int rank, double perRare)
        {
            double sum = 0;
            foreach (var rare in GameData.Rares)
            {
                if (rare.Rank != rank) continue;
                int count = State.RareCountOf(rare.Id);
                if (count <= 0) continue;
                int dupes = Math.Min(count - 1, Balance.RareMaxDupes);
                sum += perRare * (1 + Balance.RareDupeBonus * dupes);
            }
            return sum;
        }

        private double GlobalDamageMultiplier
        {
            get
            {
                int layer = GameData.LayerOfRow(State.depth);
                double frenzy = InFrenzy
                    ? Balance.FrenzyDamageMultiplier * (1 + Effect(SkillEffect.frenzy_mult) / 100)
                    : 1;
                return (1 + Effect(SkillEffect.dmg_pct) / 100)
                    * (1 + Effect(SkillEffect.swarm_pct) / 100 * (State.drills.Count / SwarmPerDrills))
                    * (1 + layerBonus[layer] / 100)
                    * (1 + Balance.ShardBonus * State.shards)
                    * drillBonus
                    * ComboMultiplier
                    * frenzy;
            }
        }

        public static double KindDamage(int code)
        {
            var family = GameData.Families[DrillCode.Family(code)];
            return family.Damage * Math.Pow(GameData.TierDamageMult, DrillCode.Tier(code));
        }

        /// <summary>What one swing of this kind of drill does right now, swarm share included.</summary>
        public double DamageOf(int code) => KindDamage(code) * GlobalDamageMultiplier * swarmMultiplier;

        public double ClickDamage => DamageOf(Lead.Code) * (1 + Effect(SkillEffect.click_pct) / 100);

        public double SpeedMultiplier =>
            (1 + Effect(SkillEffect.speed_pct) / 100) * (InFrenzy ? Balance.FrenzySpeedMultiplier : 1);

        public double HitsPerSecond(int code) =>
            GameData.Families[DrillCode.Family(code)].HitsPerSecond * SpeedMultiplier;

        /// <summary>Combined damage per second of every owned drill.</summary>
        public double TotalDps
        {
            get
            {
                double sum = 0;
                foreach (int code in State.drills) sum += KindDamage(code) * HitsPerSecond(code);
                return sum * GlobalDamageMultiplier;
            }
        }

        public int BlastRadius => Balance.BaseBlastRadius + (int)Effect(SkillEffect.blast_radius);

        private double TntChance => Balance.TntChance * (1 + Effect(SkillEffect.tnt_rate) / 100);
        private double ChestChance => Balance.ChestChance * (1 + Effect(SkillEffect.chest_pct) / 100);
        private int ChainLimit => Balance.BaseChainLimit + (Effect(SkillEffect.chain) > 0 ? int.MaxValue / 2 : 0);
        private double CritChance => Balance.CritChance + Effect(SkillEffect.crit_chance) / 100;
        private double CritMultiplier => Balance.CritMultiplier + Effect(SkillEffect.crit_mult) / 100;

        private double IncomeMultiplier =>
            (1 + Effect(SkillEffect.gold_pct) / 100)
            * (1 + (2 * AchievementCount("reach_") + RareBonus(2, 5)) / 100);

        public int SimulationLimit => Balance.SimulatedDrills + (int)Effect(SkillEffect.hangar_slots);

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
                    WorldGen.Fill(block, State.seed, row, col, TntChance, ChestChance);
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
            for (int i = State.bossesDefeated; i < GameData.Layers.Count; i++)
            {
                int start = GameData.Layers[i].RowEnd;
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

        // Two drills never share a cell: to the one being stepped, the other is a wall.
        private bool SolidForStepper(int row, int col) =>
            IsSolid(row, col) || (occupied.TryGetValue(CellKey(row, col), out var other) && other != stepper);

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
            Strike(row, col, ClickDamage, true, RollCrit(), Lead);
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

        public bool FamilyUnlocked(int family) => ReachedLayer >= GameData.Families[family].UnlockLayer;

        private double DiscountFraction =>
            Math.Min(0.9, (Effect(SkillEffect.buy_discount) + 2 * AchievementCount("drills_")) / 100);

        public int BuyTier => (int)Effect(SkillEffect.buy_tier);

        public double DrillPrice(int family) =>
            Balance.DrillPrice(GameData.Families[family].BasePrice, State.BoughtOf(family), DiscountFraction, BuyTier);

        public bool CanBuyDrill(int family) => FamilyUnlocked(family) && State.coins >= DrillPrice(family);

        public bool BuyDrill(int family)
        {
            if (!CanBuyDrill(family)) return false;
            State.coins -= DrillPrice(family);
            State.AddBought(family);
            State.drills.Add(DrillCode.Make(family, Math.Min(BuyTier, GameData.Tiers - 1)));
            RebuildUnits();
            Events.Add(new SimEvent { Kind = SimEventKind.DrillsChanged, Amount = family });
            return true;
        }

        public bool CanMerge(int code)
        {
            if (DrillCode.Tier(code) + 1 >= GameData.Tiers) return false;
            int found = 0;
            foreach (int c in State.drills)
                if (c == code && ++found >= 2) return true;
            return false;
        }

        /// <summary>Two drills of one kind become one of the next material, sometimes two steps up.</summary>
        public bool Merge(int code)
        {
            if (!CanMerge(code)) return false;
            MergeOnce(code);
            RebuildUnits();
            return true;
        }

        private void MergeOnce(int code)
        {
            State.drills.Remove(code);
            State.drills.Remove(code);
            double lucky = (Effect(SkillEffect.merge_lucky) + AchievementCount("tier_")) / 100;
            int steps = random.NextDouble() < lucky ? 2 : 1;
            int tier = Math.Min(GameData.Tiers - 1, DrillCode.Tier(code) + steps);
            State.drills.Add(DrillCode.Make(DrillCode.Family(code), tier));
            State.mergesEver++;
            State.bestTier = Math.Max(State.bestTier, tier + 1);
            Events.Add(new SimEvent { Kind = SimEventKind.DrillMerged, Amount = DrillCode.Make(DrillCode.Family(code), tier) });
        }

        public bool StartFrenzy()
        {
            if (!FrenzyReady) return false;
            FrenzyEnergy = 0;
            FrenzyTimeLeft = FrenzyDuration;
            Events.Add(new SimEvent { Kind = SimEventKind.FrenzyStarted });
            return true;
        }

        public bool LayerReached(SkillDef skill) => ReachedLayer >= skill.RequiresLayer;

        public bool IsUnlocked(SkillDef skill)
        {
            if (!LayerReached(skill)) return false;
            foreach (string id in skill.Prereq)
                if (State.LevelOf(id) <= 0) return false;
            return true;
        }

        public bool CanBuy(SkillDef skill)
        {
            int level = State.LevelOf(skill.Id);
            return IsUnlocked(skill) && level < skill.MaxLevel && State.coins >= skill.CostAt(level);
        }

        public bool Buy(string skillId)
        {
            var skill = GameData.Skill(skillId);
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
                Effect(SkillEffect.offline_pct) / 100, Effect(SkillEffect.offline_hours));
            State.coins += earned;
            State.totalIncome += earned;
            return earned;
        }

        /// <summary>Trades the run for star core shards. Shards and lifetime records are all that stay.</summary>
        public bool Rebirth()
        {
            if (!CanRebirth) return false;
            int gained = ShardsOnRebirth;
            double startGold = Balance.StartGold * Math.Max(1, Effect(SkillEffect.start_gold));
            State.shards += gained;
            State.rebirths++;

            State.coins = startGold;
            State.ores = 0;
            State.depth = 0;
            State.maxDepth = 0;
            State.totalIncome = 0;
            State.incomePerSecond = 0;
            State.skills.Clear();
            State.drills.Clear();
            State.bought.Clear();
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
            State.drills.Add(DrillCode.Make(0, 0));
            RebuildUnits();
            Events.Add(new SimEvent { Kind = SimEventKind.Rebirth, Amount = gained });
            return true;
        }

        private void RefreshEffects()
        {
            Array.Clear(effects, 0, effects.Length);
            Array.Clear(layerBonus, 0, layerBonus.Length);
            foreach (var level in State.skills)
            {
                var skill = GameData.Skill(level.id);
                if (skill == null) continue;
                double amount = skill.PerLevel * level.level;
                if (skill.Effect == SkillEffect.layer_pct) layerBonus[skill.ForLayer] += amount;
                effects[(int)skill.Effect] += amount;
            }
            RefreshBonuses();
        }

        private void RefreshBonuses()
        {
            drillBonus = (1 + (3 * AchievementCount("boss_") + 2 * AchievementCount("merge_")) / 100.0)
                * (1 + RareBonus(0, 1) / 100);
        }

        // Keeps the bodies in step with the owned list, strongest first, without moving drills
        // that stay on the field. Owned drills beyond the limit lend their power to the others.
        private void RebuildUnits()
        {
            var sorted = new List<int>(State.drills);
            sorted.Sort((a, b) => KindDamage(b).CompareTo(KindDamage(a)));
            int limit = Math.Min(sorted.Count, SimulationLimit);

            var spare = new Dictionary<int, Stack<DrillUnit>>();
            foreach (var unit in Units)
            {
                if (!spare.TryGetValue(unit.Code, out var stack)) spare[unit.Code] = stack = new Stack<DrillUnit>();
                stack.Push(unit);
            }

            var keep = new List<DrillUnit>(limit);
            var fresh = new List<DrillUnit>();
            for (int i = 0; i < limit; i++)
            {
                if (spare.TryGetValue(sorted[i], out var stack) && stack.Count > 0)
                {
                    keep.Add(stack.Pop());
                    continue;
                }
                var unit = new DrillUnit { Code = sorted[i] };
                keep.Add(unit);
                fresh.Add(unit);
            }

            var leftovers = new List<DrillUnit>();
            foreach (var stack in spare.Values) leftovers.AddRange(stack);

            if (Lead != null && !keep.Contains(Lead))
            {
                // the lead was merged away: hand its place and the camera on
                DrillBody old = Lead.Body;
                if (fresh.Count > 0) fresh[0].Body.Place(old.X, old.Y);
                Lead = fresh.Count > 0 ? fresh[0] : keep[0];
                if (fresh.Count > 0) fresh.RemoveAt(0);
            }

            Units.Clear();
            Units.AddRange(keep);
            foreach (var unit in fresh)
            {
                if (Lead == null)
                {
                    unit.Body.Place(State.drillX, State.drillY);
                    Lead = unit;
                }
                else if (leftovers.Count > 0)
                {
                    var old = leftovers[leftovers.Count - 1];
                    leftovers.RemoveAt(leftovers.Count - 1);
                    unit.Body.Place(old.Body.X, old.Body.Y); // a merged drill takes over a spent one's spot
                }
                else
                {
                    SpawnNearLead(unit);
                }
            }

            double ownedPower = 0, activePower = 0;
            foreach (int code in State.drills) ownedPower += KindDamage(code) * GameData.Families[DrillCode.Family(code)].HitsPerSecond;
            foreach (var unit in Units) activePower += KindDamage(unit.Code) * GameData.Families[unit.Family].HitsPerSecond;
            swarmMultiplier = activePower > 0 ? ownedPower / activePower : 1;
        }

        private void SpawnNearLead(DrillUnit unit)
        {
            // Works upward from the lead's row until it finds a free cell, so a big batch forms a crowd
            // above the digging face instead of piling into one spot. Rows above ground are open air.
            int width = Balance.SpawnSpreadColumns * 2 + 1;
            int start = random.Next(width);
            int leadRow = Lead.Body.FeetRow - 1;
            for (int up = 0; up < MaxSpawnRowsUp; up++)
            {
                int row = leadRow - up;
                for (int i = 0; i < width; i++)
                {
                    int offset = (start + i) % width - Balance.SpawnSpreadColumns;
                    int col = Math.Max(1, Math.Min(Balance.Columns - 2, Lead.Body.Column + offset));
                    if (occupied.ContainsKey(CellKey(row, col))) continue;
                    Carve(row, col); // opens a small pocket; the drill then digs its own way down
                    unit.Body.Place(col, row);
                    occupied[CellKey(row, col)] = unit;
                    unit.HitClock = random.NextDouble();
                    return;
                }
            }
            unit.Body.Place(Lead.Body.X, leadRow - MaxSpawnRowsUp);
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

        private void AdvanceHits(double dt)
        {
            foreach (var unit in Units)
            {
                unit.HitClock += dt * HitsPerSecond(unit.Code);
                int swings = 0;
                while (unit.HitClock >= 1 && swings < MaxSwingsPerUnitPerTick)
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
            Strike(row, col, DamageOf(unit.Code), false, RollCrit(), unit);
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

        private void Strike(int row, int col, double damage, bool fromClick, bool crit, DrillUnit by)
        {
            var block = Row(row)[col];
            double amount = damage * (crit ? CritMultiplier : 1);
            bool heavy = by.Family == HeavyFamily;
            if (block.Type == BlockType.HardRock)
                amount *= (1 + Effect(SkillEffect.hard_pct) / 100) * (heavy ? 3 : 1);

            if (block.Type == BlockType.BossRoot)
            {
                if (!BossActive) return;
                amount *= (1 + Effect(SkillEffect.boss_pct) / 100) * (heavy ? 1.5 : 1);
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

            ApplyDamage(row, col, block, amount, fromClick, crit, by);
            RunBlasts();
        }

        private void ApplyDamage(int row, int col, Block block, double amount, bool fromClick, bool crit, DrillUnit by)
        {
            block.Hp -= amount;
            Events.Add(new SimEvent
            {
                Kind = SimEventKind.BlockHit, Row = row, Col = col, Block = block.Type,
                Amount = amount, FromClick = fromClick, Crit = crit,
            });
            if (block.Hp > 0) return;

            double overflow = -block.Hp;
            Break(row, col, block, fromClick, false, by);
            PassOverflow(row, col, overflow, by);

            if (by.Family != BlastFamily) return;
            double splash = amount * Balance.BlastDrillSplash * (1 + Effect(SkillEffect.blast_dmg) / 100);
            for (int r = row - 1; r <= row + 1; r++)
            {
                for (int c = col - 1; c <= col + 1; c++)
                {
                    if (r < 0 || c < 0 || c >= Balance.Columns || (r == row && c == col)) continue;
                    var near = Row(r)[c];
                    if (!near.Alive || near.Type == BlockType.BossRoot) continue;
                    near.Hp -= splash;
                    if (near.Hp <= 0) Break(r, c, near, false, true, by);
                }
            }
        }

        // pierce: what was left over when a block broke carries on into the next ones down
        private void PassOverflow(int row, int col, double overflow, DrillUnit by)
        {
            int layers = (int)Effect(SkillEffect.pierce);
            for (int i = 1; i <= layers && overflow > 0; i++)
            {
                int next = row + i;
                if (col < 0 || col >= Balance.Columns || !IsSolid(next, col)) continue;
                var block = Row(next)[col];
                if (block.Type == BlockType.BossRoot) return;
                block.Hp -= overflow;
                if (block.Hp > 0) return;
                overflow = -block.Hp;
                Break(next, col, block, false, false, by);
            }
        }

        private void Break(int row, int col, Block block, bool fromClick, bool byBlast, DrillUnit by)
        {
            block.Hp = 0;
            bool isOre = block.Type == BlockType.Ore;
            double value = WorldGen.ValueOf(block, row) * ComboMultiplier * IncomeMultiplier;
            if (isOre)
            {
                value *= (1 + Effect(SkillEffect.ore_pct) / 100) * (1 + RareBonus(1, 3) / 100);
                if (byBlast) value *= 1 + Effect(SkillEffect.blast_ore) / 100;
                if (by.Family == MagnetFamily) value *= 2;
                State.ores += 1;
            }
            if (block.Type == BlockType.Chest)
            {
                value *= 1 + Effect(SkillEffect.chest_gold) / 100;
                RollRareDrop(row);
            }

            State.coins += value;
            State.totalIncome += value;
            incomeBuckets[incomeIndex] += value;

            Combo += by.Family == SwiftFamily ? 2 : 1;
            State.bestCombo = Math.Max(State.bestCombo, Combo);
            comboClock = Balance.ComboWindowSeconds + Effect(SkillEffect.combo_window);
            if (!InFrenzy && Combo >= 2)
            {
                double charge = Balance.FrenzyChargePerBreak * (1 + Effect(SkillEffect.frenzy_charge) / 100);
                FrenzyEnergy = Math.Min(1, FrenzyEnergy + charge);
            }

            Events.Add(new SimEvent
            {
                Kind = SimEventKind.BlockBroken, Row = row, Col = col, Block = block.Type, Ore = block.Ore,
                Amount = value, FromClick = fromClick,
            });

            if (block.Type == BlockType.Tnt && blastsThisChain < ChainLimit)
            {
                blastsThisChain++;
                State.bestChain = Math.Max(State.bestChain, blastsThisChain);
                pendingBlasts.Enqueue((row, col));
            }
        }

        private void RollRareDrop(int row)
        {
            int layer = GameData.LayerOfRow(row);
            double boost = 1 + Effect(SkillEffect.rare_pct) / 100;
            for (int i = 0; i < GameData.Rares.Count; i++)
            {
                var rare = GameData.Rares[i];
                if (rare.Layer != layer || random.NextDouble() >= rare.DropChance * boost) continue;
                State.AddRare(rare.Id);
                RefreshBonuses();
                Events.Add(new SimEvent { Kind = SimEventKind.RareFound, Amount = i });
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
                    if (block.Alive && block.Type != BlockType.BossRoot) Break(r, c, block, false, true, Lead);
                }
            }
        }

        private void UpdateDepth()
        {
            int feet = Lead.Body.FeetRow;
            if (feet <= State.depth) return;
            for (int row = State.depth; row < feet; row++) rows.Remove(row - KeptRowsAbove);
            int layer = GameData.LayerOfRow(feet);
            bool entered = layer != GameData.LayerOfRow(State.depth);
            State.depth = feet;
            State.maxDepth = Math.Max(State.maxDepth, feet);
            State.maxDepthEver = Math.Max(State.maxDepthEver, feet);
            Events.Add(new SimEvent { Kind = SimEventKind.DepthChanged, Row = feet });
            if (entered) Events.Add(new SimEvent { Kind = SimEventKind.LayerEntered, Amount = layer });
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
            int top = Math.Max(0, State.depth - Balance.LooseWindowRows);
            for (int row = State.depth + Balance.LooseWindowRows; row >= top; row--)
            {
                if (!rows.TryGetValue(row, out var upper) || !rows.TryGetValue(row + 1, out var lower)) continue;
                for (int col = 0; col < Balance.Columns; col++)
                {
                    if (upper[col].Type != BlockType.Sand || !upper[col].Alive) continue;
                    if (lower[col].Alive || occupied.ContainsKey(CellKey(row + 1, col))) continue;
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
            AutoHangar();
            CheckAchievements();
        }

        // Drills that fall behind or wander off screen rejoin the lead, so the view stays busy.
        private void BringBackStragglers()
        {
            occupied.Clear();
            foreach (var unit in Units)
            {
                long cell = CellKey(unit.Body.BodyRow, unit.Body.Column);
                bool sharing = occupied.ContainsKey(cell);
                if (!sharing) occupied[cell] = unit;
                if (unit == Lead) continue;
                bool behind = Lead.Body.Y - unit.Body.Y > Balance.StragglerRows;
                bool away = Math.Abs(Lead.Body.X - unit.Body.X) > Balance.StragglerColumns;
                if (behind || away || sharing) SpawnNearLead(unit);
            }
        }

        private void AutoHangar()
        {
            bool changed = false;
            if (Effect(SkillEffect.auto_merge) > 0)
            {
                var counts = new Dictionary<int, int>();
                foreach (int code in State.drills)
                    counts[code] = counts.TryGetValue(code, out int n) ? n + 1 : 1;
                foreach (var pair in counts)
                {
                    if (DrillCode.Tier(pair.Key) + 1 >= GameData.Tiers) continue;
                    for (int i = 0; i < pair.Value / 2; i++)
                    {
                        MergeOnce(pair.Key);
                        changed = true;
                    }
                }
            }

            if (Effect(SkillEffect.auto_buy) > 0)
            {
                for (int i = 0; i < AutoBuysPerTick; i++)
                {
                    int cheapest = -1;
                    for (int f = 0; f < GameData.Families.Count; f++)
                        if (CanBuyDrill(f) && (cheapest < 0 || DrillPrice(f) < DrillPrice(cheapest))) cheapest = f;
                    if (cheapest < 0) break;
                    State.coins -= DrillPrice(cheapest);
                    State.AddBought(cheapest);
                    State.drills.Add(DrillCode.Make(cheapest, Math.Min(BuyTier, GameData.Tiers - 1)));
                    changed = true;
                }
            }
            if (changed) RebuildUnits();
        }

        /// <summary>The condition for an achievement, decoded from its id in the design data.</summary>
        public bool AchievementMet(AchievementDef a)
        {
            string id = a.Id;
            if (id == "center") return State.maxDepthEver >= GameData.TotalRows;
            int number = int.Parse(id.Substring(id.IndexOf('_') + 1));
            if (id.StartsWith("reach_", StringComparison.Ordinal)) return ReachedLayer >= number - 1;
            if (id.StartsWith("boss_", StringComparison.Ordinal)) return State.bossesEver >= number;
            if (id.StartsWith("drills_", StringComparison.Ordinal)) return State.drills.Count >= number;
            if (id.StartsWith("tier_", StringComparison.Ordinal)) return State.bestTier >= number;
            if (id.StartsWith("merge_", StringComparison.Ordinal)) return State.mergesEver >= number;
            if (id.StartsWith("combo_", StringComparison.Ordinal)) return State.bestCombo >= number;
            if (id.StartsWith("rebirth_", StringComparison.Ordinal)) return State.rebirths >= number;
            if (id.StartsWith("chain_", StringComparison.Ordinal)) return State.bestChain >= number;
            return false;
        }

        private void CheckAchievements()
        {
            for (int i = 0; i < GameData.Achievements.Count; i++)
            {
                var a = GameData.Achievements[i];
                if (State.achievements.Contains(a.Id) || !AchievementMet(a)) continue;
                State.achievements.Add(a.Id);
                RefreshBonuses();
                Events.Add(new SimEvent { Kind = SimEventKind.AchievementUnlocked, Amount = i });
            }
        }

        private void AdvanceBoss(double dt)
        {
            var layer = CurrentBoss;
            if (layer == null) return;
            if (!BossActive)
            {
                if (Lead.Body.FeetRow < layer.RowEnd || !Lead.Body.Grounded) return;
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
            var layer = CurrentBoss;
            int bossRow = layer.RowEnd;
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
            if (Effect(SkillEffect.frenzy_quake) > 0)
            {
                Explode(RoundToInt(Lead.Body.TipY), RoundToInt(Lead.Body.TipX), Balance.FrenzyQuakeRadius);
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
