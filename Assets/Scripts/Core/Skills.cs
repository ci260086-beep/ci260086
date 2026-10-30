using System;
using System.Collections.Generic;

namespace DeepDrill.Core
{
    public enum SkillEffect
    {
        Damage, Speed, CritChance, CritDamage, HardDamage,
        OreValue, CoinValue, ChestValue, Offline, DrillDiscount,
        TntRate, BlastRadius, BlastLoot, BlastChain, Pierce,
        FrenzyTime, FrenzyCharge, FrenzyBurst, ComboWindow, ComboCap,
        BossDamage, BossTime, Shards, MergeBonus, DepthValue,
    }

    public sealed class SkillDef
    {
        public string Id;
        public int Tree;
        public int Column;
        public int Rank;
        public SkillEffect Effect;
        public string Name;
        public string EffectText;
        public double PerLevel;
        public int MaxLevel;
        public int UnlockDepth;
        public double BaseCost;
        public double Growth;
        public string RequiresId;

        public double CostAt(int level) => Balance.Cost(BaseCost, Growth, level);
    }

    /// <summary>
    /// 5 trees of 5 effects with 6 ranks each: 150 skills. Every number is derived from the depth
    /// the skill unlocks at, so a skill always costs about what that depth pays.
    /// </summary>
    public static class SkillCatalog
    {
        public const int RanksPerEffect = 6;
        public const int DepthStep = 40;
        public const double CostGrowth = 1.18;
        public const double CostScale = 30;

        private struct Seed
        {
            public SkillEffect Effect;
            public string Name;
            public string Format; // {0} is the per-level amount
            public double PerLevel;
            public int MaxLevel;
            public double CostWeight;
        }

        public static readonly string[] TreeNames = { "钻头", "经济", "爆破", "狂热", "地核" };

        private static readonly Seed[][] Trees =
        {
            new[]
            {
                S(SkillEffect.Damage, "钻头强化", "伤害+{0}%", 10, 10, 1),
                S(SkillEffect.Speed, "涡轮马达", "钻速+{0}%", 4, 10, 1.3),
                S(SkillEffect.CritChance, "精准打击", "暴击率+{0}%", 1, 5, 1.6),
                S(SkillEffect.CritDamage, "致命一击", "暴击伤害+{0}%", 15, 8, 1.9),
                S(SkillEffect.HardDamage, "硬质合金", "硬岩伤害+{0}%", 20, 8, 2.2),
            },
            new[]
            {
                S(SkillEffect.OreValue, "矿物精炼", "矿物价值+{0}%", 8, 10, 1),
                S(SkillEffect.CoinValue, "黄金市场", "全部收入+{0}%", 4, 10, 1.3),
                S(SkillEffect.ChestValue, "寻宝罗盘", "宝箱价值+{0}%", 30, 5, 1.6),
                S(SkillEffect.Offline, "自动矿场", "离线收益+{0}%", 2, 5, 1.9),
                S(SkillEffect.DrillDiscount, "批量采购", "钻头价格-{0}%", 1, 5, 2.2),
            },
            new[]
            {
                S(SkillEffect.TntRate, "多埋炸药", "炸药率+{0}%", 0.2, 5, 1),
                S(SkillEffect.BlastRadius, "加大药量", "爆炸半径+{0}", 0.25, 2, 3),
                S(SkillEffect.BlastLoot, "定向爆破", "爆炸矿物+{0}%", 25, 4, 1.6),
                S(SkillEffect.BlastChain, "连环引爆", "连锁+{0}", 1, 3, 1.9),
                S(SkillEffect.Pierce, "穿透钻头", "下一格伤害+{0}%", 8, 5, 2.2),
            },
            new[]
            {
                S(SkillEffect.FrenzyTime, "持久狂热", "时长+{0}秒", 0.5, 5, 1),
                S(SkillEffect.FrenzyCharge, "快速充能", "充能+{0}%", 8, 5, 1.3),
                S(SkillEffect.FrenzyBurst, "终结爆发", "结束爆炸半径+{0}", 0.5, 2, 3),
                S(SkillEffect.ComboWindow, "连击续航", "连击窗口+{0}秒", 0.1, 5, 1.9),
                S(SkillEffect.ComboCap, "连击上限", "连击上限+{0}%", 20, 5, 2.2),
            },
            new[]
            {
                S(SkillEffect.BossDamage, "破岩专家", "Boss 伤害+{0}%", 15, 8, 1),
                S(SkillEffect.BossTime, "限时延长", "Boss 时限+{0}秒", 1, 5, 1.3),
                S(SkillEffect.Shards, "星核萃取", "星核碎片+{0}%", 5, 5, 1.6),
                S(SkillEffect.MergeBonus, "合成精通", "合成钻头伤害+{0}%", 5, 5, 1.9),
                S(SkillEffect.DepthValue, "深度红利", "每 100 米收入+{0}%", 1, 5, 2.2),
            },
        };

        public static readonly IReadOnlyList<SkillDef> All = Build();

        private static Dictionary<string, SkillDef> byId;

        public static SkillDef Get(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, SkillDef>();
                foreach (var s in All) byId[s.Id] = s;
            }
            return byId.TryGetValue(id, out var skill) ? skill : null;
        }

        public static string IdOf(SkillEffect effect, int rank) => $"{effect}_{rank}";

        /// <summary>Unlock depth runs through a tree rank by rank, reaching the core near 1200m.</summary>
        public static int UnlockDepthFor(int column, int rank) => (rank * Trees[0].Length + column) * DepthStep;

        public static double BaseCostFor(int unlockDepth, double weight) =>
            CostScale * weight * Math.Pow(1.03, unlockDepth);

        private static IReadOnlyList<SkillDef> Build()
        {
            string[] numerals = { "I", "II", "III", "IV", "V", "VI" };
            var list = new List<SkillDef>();
            for (int tree = 0; tree < Trees.Length; tree++)
            {
                for (int column = 0; column < Trees[tree].Length; column++)
                {
                    var seed = Trees[tree][column];
                    for (int rank = 0; rank < RanksPerEffect; rank++)
                    {
                        int depth = UnlockDepthFor(column, rank);
                        double perLevel = seed.PerLevel * (1 + rank * 0.5);
                        list.Add(new SkillDef
                        {
                            Id = IdOf(seed.Effect, rank),
                            Tree = tree,
                            Column = column,
                            Rank = rank,
                            Effect = seed.Effect,
                            Name = $"{seed.Name} {numerals[rank]}",
                            EffectText = string.Format(seed.Format, perLevel.ToString("0.##")),
                            PerLevel = perLevel,
                            MaxLevel = seed.MaxLevel,
                            UnlockDepth = depth,
                            BaseCost = BaseCostFor(depth, seed.CostWeight),
                            Growth = CostGrowth,
                            RequiresId = rank == 0 ? null : IdOf(seed.Effect, rank - 1),
                        });
                    }
                }
            }
            return list;
        }

        private static Seed S(SkillEffect effect, string name, string format, double perLevel, int maxLevel, double weight) =>
            new Seed { Effect = effect, Name = name, Format = format, PerLevel = perLevel, MaxLevel = maxLevel, CostWeight = weight };
    }
}
