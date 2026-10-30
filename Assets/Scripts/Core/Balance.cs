using System;

namespace DeepDrill.Core
{
    /// <summary>Every tunable number and formula from the design brief.</summary>
    public static class Balance
    {
        public const int Columns = 13;
        public const int ShaftCenter = 6;
        public const int ShaftHalfWidth = 1;

        public const double BaseDamage = 4;
        public const double BaseHitsPerSecond = 6;
        public const double SpeedPerLevel = 0.05;
        public const double ShardBonus = 0.02;

        public const double HardRockHpMultiplier = 3;
        public const double DirtValue = 0.5;
        public const double CoalValue = 1;
        public const double CopperValue = 2;
        public const double ChestValue = 25;

        public const double CritChance = 0.1;
        public const double CritMultiplier = 3;

        public const double ComboWindowSeconds = 3;
        public const int ComboStep = 10;
        public const double ComboBonusPerStep = 0.1;
        public const double ComboBonusMax = 1.0; // without a cap long chains compound into runaway income

        public const double FrenzySeconds = 10;
        public const double FrenzySpeedMultiplier = 5;
        public const double FrenzyDamageMultiplier = 2;
        public const double FrenzyChargePerBreak = 0.02;
        public const double FrenzyChargePerLevel = 0.1;
        public const int FrenzyBurstRadius = 3;

        public const double HardRockBonusPerLevel = 0.2;
        public const double PierceRatioPerLevel = 0.25;

        public const double TntChance = 0.02;
        public const double TntChancePerLevel = 0.005;
        public const int BaseBlastRadius = 1;
        public const int BaseChainLimit = 2;
        public const int ChainLimitPerLevel = 2;

        public const double OfflineRate = 0.5;
        public const double OfflineCapSeconds = 2 * 60 * 60;

        public static double Cost(double baseCost, double growth, int level) =>
            baseCost * Math.Pow(growth, level);

        public static double BlockHp(int depth) => 10 * Math.Pow(1.035, depth);

        public static double OreValue(int depth, double multiplier) => Math.Pow(1.03, depth) * multiplier;

        public static double OfflineEarnings(double incomePerSecond, double secondsAway)
        {
            if (secondsAway <= 0 || incomePerSecond <= 0) return 0;
            return incomePerSecond * Math.Min(secondsAway, OfflineCapSeconds) * OfflineRate;
        }
    }
}
