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
        public const double HpGrowthDeep = 1.012;
        public const int HpDeepRow = 1050;  // below the crust the ground hardens more slowly, or the last layers cannot be reached
        public const double HpRamp = 4;     // the ground toughens this many times over the first rows, on top of the growth
        public const double HpRampRows = 100;

        public const double ShardBonus = 0.15;
        public const double HardRockHpMultiplier = 3;
        public const double LooseHpMultiplier = 0.5;
        public const double DirtValue = 0.5;
        public const double OreValuePower = 0.7; // ore tiers multiply on top of depth, so left whole they outrun every price
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
        public const int LinkedChainLimit = 24;
        public const int ChainReachRows = 24; // a chain stops here, or one spark runs through the whole planet in a frame
        public const double BlastSwings = 30;
        public const double BlastFloorTiles = 8; // however weak the drills, a charge still clears a small hole
        public const double BlastDrillSplash = 0.5;

        public const double SandPixelStepSeconds = 1.0 / 30;
        public const int LooseWindowRows = 12;

        public const int SimulatedDrills = 300; // owned count has no cap; the rest add their power to these
        public const int SpawnSpreadColumns = 6;
        public const double StragglerRows = 7;
        public const double StragglerColumns = 11;

        public const int BossRows = 2;
        public const double BossHpScale = 0.05;
        public const double BossArmorRatio = 0.25;
        public const double BossArmorDamageRatio = 0.5;
        public const double BossRewardValue = 400;
        public const int BossBreachHalfWidth = 3;
        public const double BossChipShare = 2; // hitting a guardian pays like digging dirt, so a stalled fight still funds upgrades

        public const int RareMaxDupes = 10;
        public const double RareDupeBonus = 0.5;

        public const double EventCoinFloor = 5;
        public const int ChallengeLayer = 5;

        // layer mechanics, indexed from zero as in GameData.Layers
        public const int CrystalLayer = 2;
        public const int WaterLayer = 3;
        public const int RuinsLayer = 4;
        public const int GraniteLayer = 5;
        public const int MohoLayer = 6;
        public const int MantleLayer = 7;
        public const int PhaseLayer = 8;
        public const int ConvectionLayer = 9;
        public const int LiquidLayer = 10;
        public const int CoreLayer = 11;

        public const double CrystalReflectRatio = 0.5;
        public const int CrystalReflectRadius = 2;
        public const double WaterShare = 0.08;
        public const double WaterPushSpeed = 3;
        public const int WaterRadius = 3;
        public const int RoomEveryRows = 25;
        public const int RoomWidth = 7;
        public const int RoomHeight = 3;
        public const double TrapChestChance = 0.25;
        public const double SuperHardShare = 0.15;
        public const double SuperHardHpMultiplier = 6;
        public const double SuperHardWeakDamage = 0.25;
        public const double OverheatPeriodSeconds = 10;
        public const double OverheatSeconds = 3;
        public const double OverheatSpeed = 0.5;
        public const double MagmaShare = 0.06;
        public const int MagmaChainLimit = 12;
        public const int PhaseBandRows = 30;
        public const double PhaseHardHigh = 0.30;
        public const double PhaseHardLow = 0.05;
        public const double ConvectionSeconds = 0.25;
        public const double RefillSeconds = 6;
        public const double HeatDamageRatio = 0.25;

        public const double OfflineRate = 0.5;
        public const double OfflineCapHours = 2;
        public const double StartGold = 50;
        public const double RebirthRateMinSeconds = 60;
        public const double RebirthAdviceRatio = 0.85;
        public const double RebirthAdviceMinSeconds = 900;

        public static double BlockHp(int row) =>
            HpBase * Math.Pow(HpGrowth, Math.Min(row, HpDeepRow)) * Math.Pow(HpGrowthDeep, Math.Max(0, row - HpDeepRow)) * (1 + (HpRamp - 1) * Math.Min(1, Math.Max(0, row) / HpRampRows));

        public static double OreValue(int row, double multiplier) => Math.Pow(ValueGrowth, row) * multiplier;

        /// <summary>Price of the next drill of a family, per the design formula.</summary>
        public static double DrillPrice(double basePrice, int bought, double discountFraction, int buyTier) =>
            basePrice * Math.Pow(GameData.BuyGrowth, bought) * (1 - discountFraction) * Math.Pow(3, buyTier);

        public static double OfflineEarnings(double incomePerSecond, double secondsAway, double extraRate = 0, double extraHours = 0)
        {
            if (secondsAway <= 0 || incomePerSecond <= 0) return 0;
            return incomePerSecond * OfflineSecondsPaid(secondsAway, extraHours) * (OfflineRate + extraRate);
        }

        /// <summary>How much of an absence counts, after the cap.</summary>
        public static double OfflineSecondsPaid(double secondsAway, double extraHours = 0) =>
            Math.Min(Math.Max(0, secondsAway), (OfflineCapHours + extraHours) * 3600);

        /// <summary>The first-version shard formula, counted in rows of progress.</summary>
        public static int ShardsForRebirth(int maxRow, double totalIncome)
        {
            return (int)Math.Floor(ShardProgress(maxRow, totalIncome));
        }

        /// <summary>The same formula before rounding, so a rate built on it moves smoothly.</summary>
        public static double ShardProgress(int maxRow, double totalIncome)
        {
            if (maxRow <= 0 || totalIncome <= 1) return 0;
            return Math.Pow(maxRow / 100.0, 1.5) * Math.Log10(totalIncome);
        }
    }
}
