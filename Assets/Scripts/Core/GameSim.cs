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
    }

    public struct SimEvent
    {
        public SimEventKind Kind;
        public int Row;
        public int Col;
        public BlockType Block;
        public double Amount;
        public bool FromClick;
        public bool Crit;
    }

    /// <summary>
    /// The whole game rule set. Knows nothing about Unity: the presentation layer calls Tick and
    /// Click, then drains <see cref="Events"/> to draw what happened.
    /// </summary>
    public sealed class GameSim
    {
        private const int IncomeWindowSeconds = 10;
        private const int MaxHitsPerTick = 64;
        private const int KeptRowsAbove = 24;

        public readonly GameState State;
        public readonly List<SimEvent> Events = new List<SimEvent>();

        private readonly Dictionary<int, Block[]> rows = new Dictionary<int, Block[]>();
        private readonly Queue<(int row, int col)> pendingBlasts = new Queue<(int, int)>();
        private readonly double[] incomeBuckets = new double[IncomeWindowSeconds];
        private readonly Random random;
        private int incomeIndex;
        private double incomeClock;
        private int incomeFilled;
        private double hitClock;
        private double comboClock;
        private int blastsThisChain;
        private bool insideBlast;

        public GameSim(GameState state, int? randomSeed = null)
        {
            State = state;
            random = new Random(randomSeed ?? state.seed);
        }

        public int Combo { get; private set; }
        public double FrenzyEnergy { get; private set; }
        public double FrenzyTimeLeft { get; private set; }
        public bool InFrenzy => FrenzyTimeLeft > 0;
        public bool FrenzyReady => !InFrenzy && FrenzyEnergy >= 1;

        public double ComboMultiplier =>
            1 + Math.Min(Balance.ComboBonusMax, Balance.ComboBonusPerStep * (Combo / Balance.ComboStep));

        public double DamagePerHit =>
            (Balance.BaseDamage + Level(SkillCatalog.DrillDamage))
            * (1 + Balance.ShardBonus * State.shards)
            * ComboMultiplier
            * (InFrenzy ? Balance.FrenzyDamageMultiplier : 1);

        public double HitsPerSecond =>
            Balance.BaseHitsPerSecond * (1 + Balance.SpeedPerLevel * Level(SkillCatalog.DrillSpeed))
            * (InFrenzy ? Balance.FrenzySpeedMultiplier : 1);

        public double FrenzyDuration => Balance.FrenzySeconds + Level(SkillCatalog.FrenzyTime);

        public int BlastRadius => Balance.BaseBlastRadius + Level(SkillCatalog.BlastRadius);

        private double TntChance => Balance.TntChance + Balance.TntChancePerLevel * Level(SkillCatalog.BlastRate);
        private int ChainLimit => Balance.BaseChainLimit + Balance.ChainLimitPerLevel * Level(SkillCatalog.BlastChain);
        private int Level(string id) => State.LevelOf(id);

        public static bool InShaft(int col) => Math.Abs(col - Balance.ShaftCenter) <= Balance.ShaftHalfWidth;

        public Block[] Row(int row)
        {
            if (rows.TryGetValue(row, out var blocks)) return blocks;

            blocks = new Block[Balance.Columns];
            for (int col = 0; col < Balance.Columns; col++)
            {
                var type = WorldGen.TypeAt(State.seed, row, col, TntChance);
                double hp = WorldGen.HpOf(type, row);
                bool alreadyDug = row < State.depth && InShaft(col);
                blocks[col] = new Block { Type = type, MaxHp = hp, Hp = alreadyDug ? 0 : hp };
            }
            rows[row] = blocks;
            return blocks;
        }

        public void Tick(double dt)
        {
            AdvanceIncomeWindow(dt);
            AdvanceCombo(dt);
            AdvanceFrenzy(dt);

            hitClock += dt * HitsPerSecond;
            int hits = 0;
            while (hitClock >= 1 && hits < MaxHitsPerTick)
            {
                hitClock -= 1;
                hits++;
                DrillHit();
            }
            if (hitClock > 1) hitClock = 1;
        }

        /// <summary>Player tap on any block. Returns false when nothing was there to hit.</summary>
        public bool Click(int row, int col)
        {
            if (row < 0 || col < 0 || col >= Balance.Columns) return false;
            var block = Row(row)[col];
            if (!block.Alive) return false;
            Strike(row, col, 1, true);
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

        public bool CanBuy(SkillDef skill)
        {
            int level = State.LevelOf(skill.Id);
            return IsUnlocked(skill) && level < skill.MaxLevel && State.coins >= skill.CostAt(level);
        }

        public bool IsUnlocked(SkillDef skill) =>
            skill.RequiresId == null || State.LevelOf(skill.RequiresId) >= skill.RequiresLevel;

        public bool Buy(string skillId)
        {
            var skill = SkillCatalog.Get(skillId);
            if (skill == null || !CanBuy(skill)) return false;
            int level = State.LevelOf(skillId);
            State.coins -= skill.CostAt(level);
            State.SetLevel(skillId, level + 1);
            Events.Add(new SimEvent { Kind = SimEventKind.SkillBought, Amount = level + 1 });
            return true;
        }

        /// <summary>Credits offline income and returns how much was added.</summary>
        public double ApplyOffline(long nowUnixSeconds)
        {
            if (State.lastSaveUnixSeconds <= 0) return 0;
            double earned = Balance.OfflineEarnings(State.incomePerSecond, nowUnixSeconds - State.lastSaveUnixSeconds);
            State.coins += earned;
            State.totalIncome += earned;
            return earned;
        }

        private void DrillHit()
        {
            int strikes = Level(SkillCatalog.DrillTwin) > 0 ? 2 : 1;
            for (int i = 0; i < strikes; i++)
            {
                int col = FrontBlockColumn();
                if (col < 0) return;
                int row = State.depth;
                Strike(row, col, 1, false);

                int pierce = Level(SkillCatalog.DrillPierce);
                if (pierce > 0 && Row(row + 1)[col].Alive)
                    Strike(row + 1, col, Balance.PierceRatioPerLevel * pierce, false);
            }
        }

        // Centre first, then the sides, so the bit visibly chews the middle of the shaft.
        private int FrontBlockColumn()
        {
            var front = Row(State.depth);
            for (int offset = 0; offset <= Balance.ShaftHalfWidth; offset++)
            {
                int left = Balance.ShaftCenter - offset;
                int right = Balance.ShaftCenter + offset;
                if (front[left].Alive) return left;
                if (front[right].Alive) return right;
            }
            return -1;
        }

        private void Strike(int row, int col, double ratio, bool fromClick)
        {
            var block = Row(row)[col];
            bool crit = random.NextDouble() < Balance.CritChance;
            double amount = DamagePerHit * ratio * (crit ? Balance.CritMultiplier : 1);
            if (block.Type == BlockType.HardRock)
                amount *= 1 + Balance.HardRockBonusPerLevel * Level(SkillCatalog.DrillHard);

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
            bool isOre = WorldGen.IsOre(block.Type);
            bool doubled = byBlast && isOre && Level(SkillCatalog.BlastLoot) > 0;
            double value = WorldGen.ValueOf(block.Type, row) * ComboMultiplier * (doubled ? 2 : 1);
            State.coins += value;
            State.totalIncome += value;
            incomeBuckets[incomeIndex] += value;
            if (isOre) State.ores += doubled ? 2 : 1;

            Combo++;
            comboClock = Balance.ComboWindowSeconds;
            if (!InFrenzy && Combo >= 2)
            {
                double charge = Balance.FrenzyChargePerBreak
                    * (1 + Balance.FrenzyChargePerLevel * Level(SkillCatalog.FrenzyCharge));
                FrenzyEnergy = Math.Min(1, FrenzyEnergy + charge);
            }

            Events.Add(new SimEvent
            {
                Kind = SimEventKind.BlockBroken, Row = row, Col = col, Block = block.Type,
                Amount = value, FromClick = fromClick,
            });

            if (block.Type == BlockType.Tnt && blastsThisChain < ChainLimit)
            {
                blastsThisChain++;
                pendingBlasts.Enqueue((row, col));
            }

            AdvanceDepth();
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
                    if (block.Alive) Break(r, c, block, false, true);
                }
            }
        }

        private void AdvanceDepth()
        {
            bool moved = false;
            while (FrontBlockColumn() < 0)
            {
                State.depth++;
                moved = true;
            }
            if (!moved) return;

            State.maxDepth = Math.Max(State.maxDepth, State.depth);
            rows.Remove(State.depth - KeptRowsAbove);
            Events.Add(new SimEvent { Kind = SimEventKind.DepthChanged, Row = State.depth });
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
            if (Level(SkillCatalog.FrenzyBurst) > 0)
            {
                Explode(State.depth, Balance.ShaftCenter, Balance.FrenzyBurstRadius + BlastRadius);
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
    }
}
