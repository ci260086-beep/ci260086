namespace DeepDrill.Core
{
    public enum BlockType
    {
        Grass,
        Dirt,
        HardRock,
        Ore,
        Sand,
        Chest,
        Tnt,
        BossRoot,
    }

    public sealed class Block
    {
        public BlockType Type;
        public int Ore; // index into Strata.Ores when Type is Ore
        public double Hp;
        public double MaxHp;
        public bool Alive => Hp > 0;
    }

    /// <summary>Deterministic block layout: the same seed always rebuilds the same ground.</summary>
    public static class WorldGen
    {
        private const int SafeRows = 3;
        private const int StratumHashOffset = 1000;
        private const int OreHashOffset = 2000;
        private const double ChestShare = 0.005;
        private const double OreShare = 0.11;
        private const double HardShare = 0.10;
        private const double LooseShare = 0.16;

        public static uint Hash(int seed, int row, int col)
        {
            unchecked
            {
                uint h = Mix((uint)seed + 0x9E3779B9u);
                h = Mix(h + (uint)row * 2654435761u);
                return Mix(h + (uint)col * 2246822519u);
            }
        }

        // MurmurHash3 finalizer
        private static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }

        public static double Unit(int seed, int row, int col) => Hash(seed, row, col) / (double)uint.MaxValue;

        /// <summary>Which stratum a cell belongs to. The last rows of one stratum mix in the next.</summary>
        public static int StratumAt(int seed, int row, int col)
        {
            int stratum = Strata.OfDepth(row);
            if (stratum >= Strata.Last || row < 0) return stratum;
            int intoEnd = row % Strata.RowsPerStratum - (Strata.RowsPerStratum - Strata.TransitionRows);
            if (intoEnd < 0) return stratum;
            double share = (intoEnd + 1) / (double)(Strata.TransitionRows + 1);
            return Unit(seed, row, col + StratumHashOffset) < share ? stratum + 1 : stratum;
        }

        public static void Fill(Block block, int seed, int row, int col, double tntChance)
        {
            block.Ore = 0;
            block.Type = TypeAt(seed, row, col, tntChance);
            if (block.Type == BlockType.Ore) block.Ore = OreAt(seed, row, col);
            double hp = HpOf(block.Type, row);
            block.Hp = hp;
            block.MaxHp = hp;
        }

        public static BlockType TypeAt(int seed, int row, int col, double tntChance = Balance.TntChance)
        {
            if (row == 0) return BlockType.Grass;
            if (row < SafeRows) return BlockType.Dirt;

            double r = Unit(seed, row, col);
            var stratum = Strata.All[StratumAt(seed, row, col)];
            if (r < ChestShare) return BlockType.Chest;
            r -= ChestShare;
            if (r < OreShare) return BlockType.Ore;
            r -= OreShare;
            if (r < HardShare) return BlockType.HardRock;
            r -= HardShare;
            if (stratum.HasLoose)
            {
                if (r < LooseShare) return BlockType.Sand;
                r -= LooseShare;
            }
            return r < tntChance ? BlockType.Tnt : BlockType.Dirt;
        }

        /// <summary>The first ore listed for a stratum is the common one; each next is half as likely.</summary>
        public static int OreAt(int seed, int row, int col)
        {
            int[] ores = Strata.All[StratumAt(seed, row, col)].Ores;
            double r = Unit(seed, row, col + OreHashOffset);
            double weight = 0.5;
            for (int i = 0; i < ores.Length - 1; i++)
            {
                if (r < weight) return ores[i];
                r -= weight;
                weight /= 2;
            }
            return ores[ores.Length - 1];
        }

        public static double HpOf(BlockType type, int depth)
        {
            double hp = Balance.BlockHp(depth);
            if (type == BlockType.HardRock) return hp * Balance.HardRockHpMultiplier;
            if (type == BlockType.Sand) return hp * Balance.LooseHpMultiplier;
            return hp;
        }

        public static double ValueOf(Block block, int depth)
        {
            switch (block.Type)
            {
                case BlockType.Ore: return Balance.OreValue(depth, Strata.Ores[block.Ore].Value);
                case BlockType.Chest: return Balance.OreValue(depth, Balance.ChestValue);
                default: return Balance.OreValue(depth, Balance.DirtValue);
            }
        }
    }
}
