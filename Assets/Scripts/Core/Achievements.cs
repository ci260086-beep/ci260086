using System;
using System.Collections.Generic;

namespace DeepDrill.Core
{
    public sealed class RareItemDef
    {
        public string Id;
        public string Name;
        public int Stratum;
    }

    public sealed class AchievementDef
    {
        public string Id;
        public string Name;
        public string Text;
        public Func<GameState, double> Progress;
        public double Goal;
    }

    /// <summary>Two collectable finds per stratum; each one owned raises all income.</summary>
    public static class RareItems
    {
        private static readonly string[] Names =
        {
            "古老树化石", "青铜箭头", "三叶虫化石", "沙漠玫瑰", "海百合化石", "恐龙牙齿",
            "紫水晶簇", "黑曜石刀", "火山玻璃", "雷击石", "断层镜面", "莫霍碎片",
            "地幔捕虏体", "绿橄榄石", "蓝尖晶晶体", "林伍德石", "布里奇曼石", "下地幔钻石",
            "后钙钛矿", "超低速带样本", "铁陨石", "液态金属滴", "内核铁晶", "地心之心",
        };

        public static readonly IReadOnlyList<RareItemDef> All = Build();

        private static IReadOnlyList<RareItemDef> Build()
        {
            var list = new List<RareItemDef>();
            for (int i = 0; i < Names.Length; i++)
                list.Add(new RareItemDef { Id = $"rare_{i}", Name = Names[i], Stratum = i / 2 });
            return list;
        }
    }

    public static class AchievementCatalog
    {
        public static readonly IReadOnlyList<AchievementDef> All = Build();

        private static IReadOnlyList<AchievementDef> Build()
        {
            var list = new List<AchievementDef>();
            for (int i = 0; i < Strata.All.Length; i++)
            {
                int stratum = i;
                list.Add(Make($"stratum_{i}", $"抵达{Strata.All[i].Name}", $"挖到 {i * Strata.RowsPerStratum} 米",
                    s => s.maxDepthEver, Math.Max(1, i * Strata.RowsPerStratum)));
                list.Add(Make($"boss_{i}", $"击败{Strata.All[i].BossName}", "打穿这一层的守卫",
                    s => s.bossesEver, stratum + 1));
            }
            foreach (double n in new double[] { 10, 50, 200, 1000, 5000 })
                list.Add(Make($"drills_{n}", $"钻头军团 {n}", $"同时拥有 {n} 个钻头", s => s.DrillsOwned, n));
            foreach (double n in new double[] { 10, 100, 1000, 10000 })
                list.Add(Make($"merges_{n}", $"合成大师 {n}", $"累计合成 {n} 次", s => s.mergesEver, n));
            foreach (double n in new double[] { 100, 1000, 10000, 100000, 1000000 })
                list.Add(Make($"ores_{n}", $"矿工 {NumberFormat.Short(n)}", $"累计挖到 {NumberFormat.Short(n)} 块矿", s => s.oresEver, n));
            foreach (double n in new double[] { 1, 6, 12, 24 })
                list.Add(Make($"rare_{n}", $"收藏家 {n}", $"收集 {n} 件稀有物品", s => s.rareItems.Count, n));
            foreach (double n in new double[] { 1, 5, 20 })
                list.Add(Make($"rebirth_{n}", $"地核重铸 {n}", $"重铸 {n} 次", s => s.rebirths, n));
            return list;
        }

        private static AchievementDef Make(string id, string name, string text, Func<GameState, double> progress, double goal) =>
            new AchievementDef { Id = id, Name = name, Text = text, Progress = progress, Goal = goal };
    }
}
