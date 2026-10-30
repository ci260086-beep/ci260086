using System.Collections.Generic;

namespace DeepDrill.Core
{
    public enum SkillTree
    {
        Drill,
        Blast,
        Frenzy,
    }

    public sealed class SkillDef
    {
        public string Id;
        public SkillTree Tree;
        public string Name;
        public string Effect;
        public int MaxLevel;
        public double BaseCost;
        public double Growth;
        public string RequiresId;
        public int RequiresLevel;

        public double CostAt(int level) => Balance.Cost(BaseCost, Growth, level);
    }

    public static class SkillCatalog
    {
        public const string DrillDamage = "drill_damage";
        public const string DrillSpeed = "drill_speed";
        public const string DrillHard = "drill_hard";
        public const string DrillTwin = "drill_twin";
        public const string DrillPierce = "drill_pierce";
        public const string BlastRate = "blast_rate";
        public const string BlastRadius = "blast_radius";
        public const string BlastLoot = "blast_loot";
        public const string BlastChain = "blast_chain";
        public const string FrenzyTime = "frenzy_time";
        public const string FrenzyCharge = "frenzy_charge";
        public const string FrenzyBurst = "frenzy_burst";

        public static readonly IReadOnlyList<SkillDef> All = new List<SkillDef>
        {
            Skill(DrillDamage, SkillTree.Drill, "钻头强化", "伤害+1", 100, 8, 1.10),
            Skill(DrillSpeed, SkillTree.Drill, "涡轮马达", "钻速+5%", 50, 25, 1.13, DrillDamage, 3),
            Skill(DrillHard, SkillTree.Drill, "硬质合金", "硬岩伤害+20%", 20, 60, 1.14, DrillSpeed, 2),
            Skill(DrillTwin, SkillTree.Drill, "双钻头", "每次打两轮", 1, 500, 1.15, DrillSpeed, 5),
            Skill(DrillPierce, SkillTree.Drill, "穿透", "下一格+25%", 5, 300, 1.15, DrillHard, 3),

            Skill(BlastRate, SkillTree.Blast, "多埋炸药", "出现率+0.5%", 10, 40, 1.13),
            Skill(BlastRadius, SkillTree.Blast, "加大药量", "爆炸半径+1", 3, 200, 1.15, BlastRate, 3),
            Skill(BlastLoot, SkillTree.Blast, "定向爆破", "爆炸矿物x2", 1, 800, 1.15, BlastRadius, 1),
            Skill(BlastChain, SkillTree.Blast, "连环引爆", "连锁+2", 3, 400, 1.15, BlastRate, 5),

            Skill(FrenzyTime, SkillTree.Frenzy, "持久狂热", "时长+1秒", 10, 50, 1.13),
            Skill(FrenzyCharge, SkillTree.Frenzy, "快速充能", "充能+10%", 10, 80, 1.13),
            Skill(FrenzyBurst, SkillTree.Frenzy, "终结爆发", "结束时大爆炸", 1, 1000, 1.15, FrenzyTime, 3),
        };

        public static SkillDef Get(string id)
        {
            foreach (var s in All)
                if (s.Id == id) return s;
            return null;
        }

        private static SkillDef Skill(string id, SkillTree tree, string name, string effect, int maxLevel,
            double baseCost, double growth, string requiresId = null, int requiresLevel = 0) =>
            new SkillDef
            {
                Id = id, Tree = tree, Name = name, Effect = effect, MaxLevel = maxLevel,
                BaseCost = baseCost, Growth = growth, RequiresId = requiresId, RequiresLevel = requiresLevel,
            };
    }
}
