using System;

namespace DeepDrill.Core
{
    /// <summary>The twenty drill types. Three of one type merge into one of the next.</summary>
    public static class DrillTiers
    {
        public static readonly string[] Names =
        {
            "木钻头", "石钻头", "铜钻头", "铁钻头", "钢钻头",
            "银钻头", "金钻头", "铂钻头", "钛钻头", "钨钻头",
            "钴钻头", "水晶钻头", "红宝石钻头", "蓝宝石钻头", "金刚石钻头",
            "等离子钻头", "激光钻头", "反物质钻头", "星核钻头", "地心钻头",
        };

        public const double BaseDamage = 4;
        public const double DamageGrowth = 3.5; // above the merge cost, so merging always gains power
        public const double BaseHitsPerSecond = 2;
        public const double BaseCost = 20;
        public const double CostGrowthPerTier = 7;
        public const double CostGrowthPerOwned = 1.13;
        public const int UnlockDepthPerTier = 60;

        public static int Count => Names.Length;

        public static double Damage(int tier) => BaseDamage * Math.Pow(DamageGrowth, tier);

        public static double Cost(int tier, int owned) =>
            BaseCost * Math.Pow(CostGrowthPerTier, tier) * Math.Pow(CostGrowthPerOwned, owned);

        public static int UnlockDepth(int tier) => tier * UnlockDepthPerTier;
    }
}
