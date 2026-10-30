using System;

namespace DeepDrill.Core
{
    /// <summary>Every tunable number and formula from the design brief.</summary>
    public static class Balance
    {
        public const int Columns = 40;
        public const int StartColumn = 20;

        public const double ShardBonus = 0.02;
        public const double HardRockHpMultiplier = 3;
        public const double LooseHpMultiplier = 0.5;
        public const double DirtValue = 0.5;
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
        public const int FrenzyBurstRadius = 3;

        public const double TntChance = 0.02;
        public const int BaseBlastRadius = 1;
        public const int BaseChainLimit = 2;

        public const double LooseStepSeconds = 0.12;
        public const int LooseWindowRows = 12;

        public const int MergeCost = 3;
        public const int MaxSimulatedDrills = 150; // owned count has no cap; the rest add their power to these
        public const double SpawnSpreadColumns = 6;
        public const double StragglerRows = 9;
        public const double StragglerColumns = 11;

        public const int BossRows = 2;
        public const double BossSeconds = 60;
        public const double BossHpMultiplier = 60;
        public const double BossRewardValue = 400;
        public const double BossArmorDamageRatio = 0.5;
        public const int BossBreachHalfWidth = 3;

        public const double RareDropChance = 0.002;
        public const double RareItemBonus = 0.02;
        public const double AchievementBonus = 0.01;

        public const double OfflineRate = 0.5;
        public const double OfflineCapSeconds = 2 * 60 * 60;

        public static double Cost(double baseCost, double growth, int level) =>
            baseCost * Math.Pow(growth, level);

        public static double BlockHp(int depth) => 10 * Math.Pow(1.035, depth);

        public static double OreValue(int depth, double multiplier) => Math.Pow(1.03, depth) * multiplier;

        public static double OfflineEarnings(double incomePerSecond, double secondsAway, double extraRate = 0)
        {
            if (secondsAway <= 0 || incomePerSecond <= 0) return 0;
            return incomePerSecond * Math.Min(secondsAway, OfflineCapSeconds) * (OfflineRate + extraRate);
        }

        public static int ShardsForRebirth(int maxDepth, double totalIncome)
        {
            if (maxDepth <= 0 || totalIncome <= 1) return 0;
            return (int)Math.Floor(Math.Pow(maxDepth / 100.0, 1.5) * Math.Log10(totalIncome));
        }
    }
}
