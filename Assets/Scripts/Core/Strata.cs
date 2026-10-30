namespace DeepDrill.Core
{
    public sealed class OreDef
    {
        public string Name;
        public double Value;
    }

    public sealed class StratumDef
    {
        public string Name;
        public string RealDepth;
        public string LooseName;
        public string BossName;
        public int[] Ores;
        public bool HasLoose;
    }

    /// <summary>A journey from the grass to the centre of the Earth, one stratum per 100m of game depth.</summary>
    public static class Strata
    {
        public const int RowsPerStratum = 100;
        public const int TransitionRows = 5;

        public static readonly OreDef[] Ores =
        {
            new OreDef { Name = "煤", Value = 1 },
            new OreDef { Name = "铜", Value = 2 },
            new OreDef { Name = "铁", Value = 5 },
            new OreDef { Name = "银", Value = 10 },
            new OreDef { Name = "铅", Value = 18 },
            new OreDef { Name = "金", Value = 30 },
            new OreDef { Name = "铂", Value = 50 },
            new OreDef { Name = "钛", Value = 80 },
            new OreDef { Name = "钨", Value = 120 },
            new OreDef { Name = "钻石", Value = 200 },
            new OreDef { Name = "橄榄石", Value = 320 },
            new OreDef { Name = "尖晶石", Value = 500 },
            new OreDef { Name = "钙钛矿", Value = 800 },
            new OreDef { Name = "铀", Value = 1300 },
            new OreDef { Name = "镍", Value = 2000 },
            new OreDef { Name = "暗物质晶", Value = 3500 },
        };

        public static readonly StratumDef[] All =
        {
            new StratumDef { Name = "表土层", RealDepth = "0 米", BossName = "巨型木根", Ores = new[] { 0, 1 } },
            new StratumDef { Name = "砂岩层", RealDepth = "1 公里", LooseName = "沙块", HasLoose = true, BossName = "岩甲虫", Ores = new[] { 2, 3, 1 } },
            new StratumDef { Name = "石灰岩层", RealDepth = "3 公里", LooseName = "碎石", HasLoose = true, BossName = "化石巨蟒", Ores = new[] { 3, 4, 2 } },
            new StratumDef { Name = "花岗岩层", RealDepth = "8 公里", BossName = "花岗岩魔像", Ores = new[] { 4, 5, 3 } },
            new StratumDef { Name = "玄武岩层", RealDepth = "20 公里", LooseName = "火山渣", HasLoose = true, BossName = "玄武巨蟹", Ores = new[] { 5, 6, 4 } },
            new StratumDef { Name = "莫霍面", RealDepth = "35 公里", BossName = "断层守卫", Ores = new[] { 6, 7, 5 } },
            new StratumDef { Name = "上地幔", RealDepth = "200 公里", LooseName = "橄榄砂", HasLoose = true, BossName = "橄榄石巨兽", Ores = new[] { 7, 10, 6 } },
            new StratumDef { Name = "过渡带", RealDepth = "520 公里", BossName = "尖晶石水母", Ores = new[] { 8, 11, 10 } },
            new StratumDef { Name = "下地幔", RealDepth = "1800 公里", BossName = "熔岩巨龙", Ores = new[] { 9, 12, 11 } },
            new StratumDef { Name = "D″ 层", RealDepth = "2800 公里", LooseName = "熔渣", HasLoose = true, BossName = "地幔之眼", Ores = new[] { 12, 13, 9 } },
            new StratumDef { Name = "外核", RealDepth = "4000 公里", LooseName = "液态铁", HasLoose = true, BossName = "铁水巨鲸", Ores = new[] { 13, 14 } },
            new StratumDef { Name = "内核", RealDepth = "6000 公里", BossName = "地心之核", Ores = new[] { 14, 15 } },
        };

        public static int Last => All.Length - 1;

        /// <summary>The stratum a depth lies in; the last one runs on forever.</summary>
        public static int OfDepth(int depth)
        {
            if (depth <= 0) return 0;
            int index = depth / RowsPerStratum;
            return index > Last ? Last : index;
        }

        /// <summary>The row a stratum's guardian blocks, at the very bottom of that stratum.</summary>
        public static int BossRow(int stratum) => (stratum + 1) * RowsPerStratum;

        public static double BossHpScale(int stratum) => Balance.BossHpMultiplier;

        /// <summary>Armour appears from the second guardian on and takes a growing share of its health.</summary>
        public static double BossArmorRatio(int stratum) => stratum == 0 ? 0 : 0.2 + 0.02 * stratum;
    }
}
