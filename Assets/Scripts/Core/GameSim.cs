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
        private const int FallbackReach = 2;
        private const double SideTargetSpacing = 1;

        public readonly GameState State;
        public readonly DrillBody Drill = new DrillBody();
        public readonly List<SimEvent> Events = new List<SimEvent>();

        private readonly Dictionary<int, Block[]> rows = new Dictionary<int, Block[]>();
        private readonly Queue<(int row, int col)> pendingBlasts = new Queue<(int, int)>();
        private readonly List<(int row, int col)> swingCells = new List<(int, int)>(3);
        private readonly double[] incomeBuckets = new double[IncomeWindowSeconds];
        private readonly Func<int, int, bool> solidCheck;
        private readonly Random random;
        private int incomeIndex;
        private double incomeClock;
        private int incomeFilled;
        private double hitClock;
        private double physicsClock;
        private double comboClock;
        private int blastsThisChain;
        private bool insideBlast;

        public GameSim(GameState state, int? randomSeed = null)
        {
            State = state;
            random = new Random(randomSeed ?? state.seed);
            solidCheck = IsSolid;

            if (!state.drillPlaced)
            {
                state.drillPlaced = true;
                state.drillX = Balance.StartColumn;
                state.drillY = -0.5 - DrillBody.HalfHeight;
            }
            Drill.X = state.drillX;
            Drill.Y = state.drillY;
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

        public Block[] Row(int row)
        {
            if (rows.TryGetValue(row, out var blocks)) return blocks;

            int drillCol = RoundToInt(Drill.X);
            blocks = new Block[Balance.Columns];
            for (int col = 0; col < Balance.Columns; col++)
            {
                var type = WorldGen.TypeAt(State.seed, row, col, TntChance);
                double hp = WorldGen.HpOf(type, row);
                // only a reloaded run meets rows above the drill here: reopen the passage it stands in
                bool alreadyDug = row < State.depth && Math.Abs(col - drillCol) <= 1;
                blocks[col] = new Block { Type = type, MaxHp = hp, Hp = alreadyDug ? 0 : hp };
            }
            rows[row] = blocks;
            return blocks;
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
            AdvancePhysics(dt);

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
            Strike(row, col, 1, true, RollCrit());
            return true;
        }

        /// <summary>Removes a block with no reward or effects.</summary>
        public void Carve(int row, int col)
        {
            Row(row)[col].Hp = 0;
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

        private void AdvancePhysics(double dt)
        {
            physicsClock += dt;
            while (physicsClock >= DrillBody.StepSeconds)
            {
                physicsClock -= DrillBody.StepSeconds;
                double impact = Drill.Step(DrillBody.StepSeconds, solidCheck);
                if (impact > 0)
                {
                    Events.Add(new SimEvent
                    {
                        Kind = SimEventKind.Landed, Row = Drill.FeetRow, Col = RoundToInt(Drill.X), Amount = impact,
                    });
                }
            }
            State.drillX = Drill.X;
            State.drillY = Drill.Y;
            UpdateDepth();
        }

        private void DrillHit()
        {
            int strikes = Level(SkillCatalog.DrillTwin) > 0 ? 2 : 1;
            for (int i = 0; i < strikes; i++) Swing();
        }

        // The bit is as wide as the body, so one swing hits the block under the tip and the ones
        // beside it together. Even ground therefore clears level and the drill drops straight;
        // only a tougher block on one side leaves a foot over a hole, which is what bends a tunnel.
        private void Swing()
        {
            swingCells.Clear();
            for (int sign = -1; sign <= 1; sign++)
            {
                if (CellAcrossNose(sign, out int row, out int col) && Row(row)[col].Alive)
                    swingCells.Add((row, col));
            }
            if (swingCells.Count == 0)
            {
                if (!NearestBlock(out int row, out int col)) return;
                swingCells.Add((row, col));
            }

            // one crit roll per swing: a crit on only one side would tip the drill for no visible reason
            bool crit = RollCrit();
            foreach (var (row, col) in swingCells)
            {
                if (!Row(row)[col].Alive) continue; // an explosion may have got there first
                Strike(row, col, 1, false, crit);

                int pierce = Level(SkillCatalog.DrillPierce);
                if (pierce > 0 && IsSolid(row + 1, col))
                    Strike(row + 1, col, Balance.PierceRatioPerLevel * pierce, false, crit);
            }
        }

        private bool RollCrit() => random.NextDouble() < Balance.CritChance;

        /// <summary>The block the tip points at, or else the nearest one the drill can reach.</summary>
        public bool FindTarget(out int row, out int col)
        {
            if (CellAcrossNose(0, out row, out col) && Row(row)[col].Alive) return true;
            return NearestBlock(out row, out col);
        }

        private bool CellAcrossNose(int sign, out int row, out int col)
        {
            row = RoundToInt(Drill.TipY - Drill.DirectionX * SideTargetSpacing * sign);
            col = RoundToInt(Drill.TipX + Drill.DirectionY * SideTargetSpacing * sign);
            return row >= 0 && col >= 0 && col < Balance.Columns;
        }

        private bool NearestBlock(out int row, out int col)
        {
            row = col = 0;
            double best = double.MaxValue;
            int centerRow = RoundToInt(Drill.TipY), centerCol = RoundToInt(Drill.TipX);
            for (int r = Math.Max(0, centerRow - FallbackReach); r <= centerRow + FallbackReach; r++)
            {
                for (int c = Math.Max(0, centerCol - FallbackReach);
                     c <= Math.Min(Balance.Columns - 1, centerCol + FallbackReach); c++)
                {
                    if (!Row(r)[c].Alive) continue;
                    double dr = r - Drill.TipY, dc = c - Drill.TipX;
                    double d = dr * dr + dc * dc;
                    if (d >= best) continue;
                    best = d;
                    row = r;
                    col = c;
                }
            }
            return best < double.MaxValue;
        }

        private void Strike(int row, int col, double ratio, bool fromClick, bool crit)
        {
            var block = Row(row)[col];
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

        private void UpdateDepth()
        {
            int feet = Drill.FeetRow;
            if (feet <= State.depth) return;
            for (int row = State.depth; row < feet; row++) rows.Remove(row - KeptRowsAbove);
            State.depth = feet;
            State.maxDepth = Math.Max(State.maxDepth, feet);
            Events.Add(new SimEvent { Kind = SimEventKind.DepthChanged, Row = feet });
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
                Explode(RoundToInt(Drill.TipY), RoundToInt(Drill.TipX), Balance.FrenzyBurstRadius + BlastRadius);
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
