namespace DeepDrill.Core
{
    public enum BlockType
    {
        Grass,
        Dirt,
        HardRock,
        Coal,
        Copper,
        Chest,
        Tnt,
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

        public static BlockType TypeAt(int seed, int row, int col, double tntChance = Balance.TntChance)
        {
            if (row == 0) return BlockType.Grass;
            if (row < SafeRows) return BlockType.Dirt;

            double r = Hash(seed, row, col) / (double)uint.MaxValue;
            if (r < 0.005) return BlockType.Chest;
            if (r < 0.085) return BlockType.Coal;
            if (r < 0.145) return BlockType.Copper;
            if (r < 0.245) return BlockType.HardRock;
            if (r < 0.245 + tntChance) return BlockType.Tnt;
            return BlockType.Dirt;
        }

        public static double HpOf(BlockType type, int depth)
        {
            double hp = Balance.BlockHp(depth);
            return type == BlockType.HardRock ? hp * Balance.HardRockHpMultiplier : hp;
        }

        public static double ValueOf(BlockType type, int depth)
        {
            switch (type)
            {
                case BlockType.Coal: return Balance.OreValue(depth, Balance.CoalValue);
                case BlockType.Copper: return Balance.OreValue(depth, Balance.CopperValue);
                case BlockType.Chest: return Balance.OreValue(depth, Balance.ChestValue);
                default: return Balance.OreValue(depth, Balance.DirtValue);
            }
        }

        public static bool IsOre(BlockType type) => type == BlockType.Coal || type == BlockType.Copper;
    }
}
