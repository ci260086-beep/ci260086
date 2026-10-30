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
    }

    public struct SimEvent
    {
        public SimEventKind Kind;
        public int Row;
        public int Col;
        public BlockType Block;
        public double Amount;
        public bool FromClick;
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
        private readonly double[] incomeBuckets = new double[IncomeWindowSeconds];
        private int incomeIndex;
        private double incomeClock;
        private int incomeFilled;
        private double hitClock;

        public GameSim(GameState state)
        {
            State = state;
        }

        public double DamagePerHit =>
            (Balance.BaseDamage + State.LevelOf(SkillCatalog.DrillDamage))
            * (1 + Balance.ShardBonus * State.shards);

        public double HitsPerSecond =>
            Balance.BaseHitsPerSecond * (1 + Balance.SpeedPerLevel * State.LevelOf(SkillCatalog.DrillSpeed));

        public static bool InShaft(int col) => Math.Abs(col - Balance.ShaftCenter) <= Balance.ShaftHalfWidth;

        public Block[] Row(int row)
        {
            if (rows.TryGetValue(row, out var blocks)) return blocks;

            blocks = new Block[Balance.Columns];
            for (int col = 0; col < Balance.Columns; col++)
            {
                var type = WorldGen.TypeAt(State.seed, row, col);
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
            Damage(row, col, DamagePerHit, true);
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
            int col = FrontBlockColumn();
            if (col >= 0) Damage(State.depth, col, DamagePerHit, false);
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

        private void Damage(int row, int col, double amount, bool fromClick)
        {
            var block = Row(row)[col];
            block.Hp -= amount;
            Events.Add(new SimEvent
            {
                Kind = SimEventKind.BlockHit, Row = row, Col = col, Block = block.Type,
                Amount = amount, FromClick = fromClick,
            });
            if (block.Hp <= 0) Break(row, col, block, fromClick);
        }

        private void Break(int row, int col, Block block, bool fromClick)
        {
            block.Hp = 0;
            double value = WorldGen.ValueOf(block.Type, row);
            State.coins += value;
            State.totalIncome += value;
            incomeBuckets[incomeIndex] += value;
            if (WorldGen.IsOre(block.Type)) State.ores += 1;

            Events.Add(new SimEvent
            {
                Kind = SimEventKind.BlockBroken, Row = row, Col = col, Block = block.Type,
                Amount = value, FromClick = fromClick,
            });

            AdvanceDepth();
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
