namespace DeepDrill.Core
{
    public enum BlockType
    {
        Grass,
        Dirt,
        HardRock,
        Coal,
        Copper,
        Iron,
        Silver,
        Sand,
        Chest,
        Tnt,
        BossRoot,
    }

    public sealed class Block
    {
        public BlockType Type;
        public double Hp;
        public double MaxHp;
        public bool Alive => Hp > 0;
    }

    /// <summary>Deterministic block layout: the same seed always rebuilds the same ground.</summary>
    public static class WorldGen
    {
        private const int SafeRows = 3;
        private const int StratumHashOffset = 1000;

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

        private static double Unit(int seed, int row, int col) => Hash(seed, row, col) / (double)uint.MaxValue;

        /// <summary>Which stratum a cell belongs to. The last rows of one stratum mix in the next.</summary>
        public static int StratumAt(int seed, int row, int col)
        {
            int start = Balance.SandstoneRow - Balance.TransitionRows;
            if (row < start) return 0;
            if (row >= Balance.SandstoneRow) return 1;
            double share = (row - start + 1) / (double)(Balance.TransitionRows + 1);
            return Unit(seed, row, col + StratumHashOffset) < share ? 1 : 0;
        }

        public static BlockType TypeAt(int seed, int row, int col, double tntChance = Balance.TntChance)
        {
            if (row == 0) return BlockType.Grass;
            if (row < SafeRows) return BlockType.Dirt;

            double r = Unit(seed, row, col);
            return StratumAt(seed, row, col) == 0 ? TopsoilType(r, tntChance) : SandstoneType(r, tntChance);
        }

        private static BlockType TopsoilType(double r, double tntChance)
        {
            if (r < 0.005) return BlockType.Chest;
            if (r < 0.085) return BlockType.Coal;
            if (r < 0.145) return BlockType.Copper;
            if (r < 0.245) return BlockType.HardRock;
            if (r < 0.245 + tntChance) return BlockType.Tnt;
            return BlockType.Dirt;
        }

        private static BlockType SandstoneType(double r, double tntChance)
        {
            if (r < 0.005) return BlockType.Chest;
            if (r < 0.065) return BlockType.Iron;
            if (r < 0.095) return BlockType.Silver;
            if (r < 0.105) return BlockType.Coal;
            if (r < 0.115) return BlockType.Copper;
            if (r < 0.215) return BlockType.HardRock;
            if (r < 0.395) return BlockType.Sand;
            if (r < 0.395 + tntChance) return BlockType.Tnt;
            return BlockType.Dirt;
        }

        public static double HpOf(BlockType type, int depth)
        {
            double hp = Balance.BlockHp(depth);
            if (type == BlockType.HardRock) return hp * Balance.HardRockHpMultiplier;
            if (type == BlockType.Sand) return hp * Balance.SandHpMultiplier;
            return hp;
        }

        public static double ValueOf(BlockType type, int depth)
        {
            switch (type)
            {
                case BlockType.Coal: return Balance.OreValue(depth, Balance.CoalValue);
                case BlockType.Copper: return Balance.OreValue(depth, Balance.CopperValue);
                case BlockType.Iron: return Balance.OreValue(depth, Balance.IronValue);
                case BlockType.Silver: return Balance.OreValue(depth, Balance.SilverValue);
                case BlockType.Chest: return Balance.OreValue(depth, Balance.ChestValue);
                default: return Balance.OreValue(depth, Balance.DirtValue);
            }
        }

        public static bool IsOre(BlockType type) =>
            type == BlockType.Coal || type == BlockType.Copper || type == BlockType.Iron || type == BlockType.Silver;
    }
}
