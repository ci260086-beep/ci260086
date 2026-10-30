using System;

namespace DeepDrill.Core
{
    /// <summary>Every tunable number and formula from the design brief.</summary>
    public static class Balance
    {
        public const int Columns = 40;
        public const int StartColumn = 20;

        public const double BaseDamage = 4;
        public const double BaseHitsPerSecond = 3; // each swing hits three blocks
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
        public const double FrenzyChargePerBreak = 0.008;
        public const double FrenzyChargePerLevel = 0.1;
        public const int FrenzyBurstRadius = 3;

        public const double HardRockBonusPerLevel = 0.2;
        public const double PierceRatioPerLevel = 0.25;

        public const double TntChance = 0.02;
        public const double TntChancePerLevel = 0.005;
        public const int BaseBlastRadius = 1;
        public const int BaseChainLimit = 2;
        public const int ChainLimitPerLevel = 2;

        public const int BossRow = 100;
        public const int BossRows = 2;
        public const double BossSeconds = 60;
        public const double BossHpMultiplier = 60;
        public const double BossRewardValue = 400;
        public const int BossBreachHalfWidth = 3;

        public static int ShardsForRebirth(int maxDepth, double totalIncome)
        {
            if (maxDepth <= 0 || totalIncome <= 1) return 0;
            return (int)Math.Floor(Math.Pow(maxDepth / 100.0, 1.5) * Math.Log10(totalIncome));
        }

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
