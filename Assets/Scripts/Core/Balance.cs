using System;

namespace DeepDrill.Core
{
    /// <summary>Formulas and the handful of feel numbers the design data does not cover.</summary>
    public static class Balance
    {
        public const int Columns = 40;
        public const int StartColumn = 20;

        public const double HpBase = 10;
        public const double HpGrowth = 1.03;
        public const double ValueGrowth = 1.028;

        public const double ShardBonus = 0.02;
        public const double HardRockHpMultiplier = 3;
        public const double LooseHpMultiplier = 0.5;
        public const double DirtValue = 0.5;
        public const double ChestValue = 25;

        public const double ChestChance = 0.005;
        public const double HardShare = 0.10;
        public const double LooseShare = 0.16;
        public const int TransitionRows = 5;

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
        public const int FrenzyQuakeRadius = 6;

        public const double TntChance = 0.02;
        public const int BaseBlastRadius = 1;
        public const int BaseChainLimit = 2;
        public const double BlastDrillSplash = 0.5;

        public const double LooseStepSeconds = 0.12;
        public const int LooseWindowRows = 12;

        public const int SimulatedDrills = 300; // owned count has no cap; the rest add their power to these
        public const int SpawnSpreadColumns = 6;
        public const double StragglerRows = 7;
        public const double StragglerColumns = 11;

        public const int BossRows = 2;
        public const double BossArmorRatio = 0.25;
        public const double BossArmorDamageRatio = 0.5;
        public const double BossRewardValue = 400;
        public const int BossBreachHalfWidth = 3;

        public const int RareMaxDupes = 10;
        public const double RareDupeBonus = 0.5;

        public const double OfflineRate = 0.5;
        public const double OfflineCapHours = 2;
        public const double StartGold = 50;

        public static double BlockHp(int row) => HpBase * Math.Pow(HpGrowth, row);

        public static double OreValue(int row, double multiplier) => Math.Pow(ValueGrowth, row) * multiplier;

        /// <summary>Price of the next drill of a family, per the design formula.</summary>
        public static double DrillPrice(double basePrice, int bought, double discountFraction, int buyTier) =>
            basePrice * Math.Pow(GameData.BuyGrowth, bought) * (1 - discountFraction) * Math.Pow(3, buyTier);

        public static double OfflineEarnings(double incomePerSecond, double secondsAway, double extraRate = 0, double extraHours = 0)
        {
            if (secondsAway <= 0 || incomePerSecond <= 0) return 0;
            double cap = (OfflineCapHours + extraHours) * 3600;
            return incomePerSecond * Math.Min(secondsAway, cap) * (OfflineRate + extraRate);
        }

        /// <summary>The first-version shard formula, counted in rows of progress.</summary>
        public static int ShardsForRebirth(int maxRow, double totalIncome)
        {
            if (maxRow <= 0 || totalIncome <= 1) return 0;
            return (int)Math.Floor(Math.Pow(maxRow / 100.0, 1.5) * Math.Log10(totalIncome));
        }
    }
}
