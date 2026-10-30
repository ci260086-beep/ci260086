using System.Collections.Generic;

namespace DeepDrill.Core
{
    public enum SkillTree
    {
        Drill,
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

        public static readonly IReadOnlyList<SkillDef> All = new List<SkillDef>
        {
            new SkillDef
            {
                Id = DrillDamage, Tree = SkillTree.Drill, Name = "钻头强化", Effect = "伤害+1",
                MaxLevel = 100, BaseCost = 8, Growth = 1.10,
            },
            new SkillDef
            {
                Id = DrillSpeed, Tree = SkillTree.Drill, Name = "涡轮马达", Effect = "钻速+5%",
                MaxLevel = 50, BaseCost = 25, Growth = 1.13,
                RequiresId = DrillDamage, RequiresLevel = 3,
            },
        };

        public static SkillDef Get(string id)
        {
            foreach (var s in All)
                if (s.Id == id) return s;
            return null;
        }
    }
}
