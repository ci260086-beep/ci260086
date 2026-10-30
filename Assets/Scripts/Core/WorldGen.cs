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
        public int Ore; // OreDef.Index when Type is Ore
        public double OreValue;
        public double Hp;
        public double MaxHp;
        public bool Alive => Hp > 0;
    }

    /// <summary>Deterministic block layout: the same seed always rebuilds the same ground.</summary>
    public static class WorldGen
    {
        private const int SafeRows = 3;
        private const int LayerHashOffset = 1000;
        private const int SandLayer = 1;

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

        /// <summary>Which layer a cell belongs to. The last rows of one layer mix in the next.</summary>
        public static int LayerAt(int seed, int row, int col)
        {
            int layer = GameData.LayerOfRow(row);
            if (layer >= GameData.Layers.Count - 1 || row < 0) return layer;
            int end = GameData.Layers[layer].RowEnd;
            int intoEnd = row - (end - Balance.TransitionRows);
            if (intoEnd < 0) return layer;
            double share = (intoEnd + 1) / (double)(Balance.TransitionRows + 1);
            return Unit(seed, row, col + LayerHashOffset) < share ? layer + 1 : layer;
        }

        public static void Fill(Block block, int seed, int row, int col, double tntChance, double chestChance)
        {
            block.Type = BlockType.Dirt;
            block.Ore = 0;
            block.OreValue = 0;

            if (row == 0) block.Type = BlockType.Grass;
            else if (row >= SafeRows) Roll(block, seed, row, col, tntChance, chestChance);

            double hp = HpOf(block.Type, row);
            block.Hp = hp;
            block.MaxHp = hp;
        }

        private static void Roll(Block block, int seed, int row, int col, double tntChance, double chestChance)
        {
            var layer = GameData.Layers[LayerAt(seed, row, col)];
            double r = Unit(seed, row, col);

            if (r < chestChance) { block.Type = BlockType.Chest; return; }
            r -= chestChance;

            foreach (var ore in layer.Ores)
            {
                if (r < ore.Rate)
                {
                    block.Type = BlockType.Ore;
                    block.Ore = ore.Index;
                    block.OreValue = ore.ValueMult;
                    return;
                }
                r -= ore.Rate;
            }

            if (r < Balance.HardShare) { block.Type = BlockType.HardRock; return; }
            r -= Balance.HardShare;

            if (layer.Index == SandLayer)
            {
                if (r < Balance.LooseShare) { block.Type = BlockType.Sand; return; }
                r -= Balance.LooseShare;
            }

            if (r < tntChance) block.Type = BlockType.Tnt;
        }

        public static double HpOf(BlockType type, int row)
        {
            double hp = Balance.BlockHp(row);
            if (type == BlockType.HardRock) return hp * Balance.HardRockHpMultiplier;
            if (type == BlockType.Sand) return hp * Balance.LooseHpMultiplier;
            return hp;
        }

        public static double ValueOf(Block block, int row)
        {
            switch (block.Type)
            {
                case BlockType.Ore: return Balance.OreValue(row, block.OreValue);
                case BlockType.Chest: return Balance.OreValue(row, Balance.ChestValue);
                default: return Balance.OreValue(row, Balance.DirtValue);
            }
        }
    }
}
