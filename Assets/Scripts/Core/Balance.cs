using System;

namespace DeepDrill.Core
{
    /// <summary>A timed guardian: a wall of living blocks across the whole world.</summary>
    public sealed class BossDef
    {
        public int Row;
        public string Name;
        public double HpMultiplier;
        public double RewardValue;
        public double ArmorRatio; // armour as a share of max HP; damage is halved until it is gone

        public double MaxHp => Balance.BlockHp(Row) * HpMultiplier;
        public double MaxArmor => MaxHp * ArmorRatio;
    }

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

        public const double IronValue = 5;
        public const double SilverValue = 10;
        public const double SandHpMultiplier = 0.5;
        public const double SandStepSeconds = 0.12;
        public const int SandWindowRows = 12;

        public const int SandstoneRow = 100;
        public const int TransitionRows = 5;
        public static readonly string[] StratumNames = { "表土层", "砂岩层" };

        public const int BossRow = SandstoneRow;
        public const int BeetleRow = 200;
        public const int BossRows = 2;
        public const double BossSeconds = 60;
        public const double BossArmorDamageRatio = 0.5;
        public const int BossBreachHalfWidth = 3;

        public static readonly BossDef[] Bosses =
        {
            new BossDef { Row = BossRow, Name = "巨型木根", HpMultiplier = 60, RewardValue = 400 },
            new BossDef { Row = BeetleRow, Name = "岩甲虫", HpMultiplier = 60, RewardValue = 800, ArmorRatio = 0.25 },
        };

        public static int StratumOfDepth(int depth) => depth < SandstoneRow ? 0 : 1;

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
