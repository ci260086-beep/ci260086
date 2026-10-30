using System;
using System.Collections.Generic;

namespace DeepDrill.Core
{
    /// <summary>The effect keys of skills.json; the names match the data exactly.</summary>
    public enum SkillEffect
    {
        dmg_pct, speed_pct, crit_chance, crit_mult, hard_pct, boss_pct, pierce, swarm_pct,
        ore_pct, gold_pct, offline_pct, offline_hours, chest_pct, chest_gold,
        tnt_rate, blast_radius, blast_ore, chain, blast_dmg,
        buy_discount, buy_tier, merge_lucky, auto_merge, auto_buy, hangar_slots,
        frenzy_time, frenzy_charge, frenzy_mult, combo_cap, combo_window, frenzy_quake,
        layer_pct, rare_pct, shard_pct, start_gold, click_pct,
    }

    public sealed class OreDef
    {
        public int Index; // position across all layers; picks the art
        public string Name;
        public double ValueMult;
        public double Rate;
    }

    public sealed class LayerDef
    {
        public int Index; // zero based
        public string Name;
        public double DepthStartM;
        public double DepthEndM;
        public int RowStart;
        public int RowEnd;
        public string Mechanic;
        public string BossName;
        public double BossHpMult;
        public double BossSeconds;
        public List<OreDef> Ores = new List<OreDef>();
    }

    public sealed class FamilyDef
    {
        public int Index;
        public string Id;
        public string Name;
        public double Damage;
        public double HitsPerSecond;
        public string Trait;
        public int UnlockLayer; // zero based
        public double BasePrice;
    }

    public sealed class SkillDef
    {
        public string Id;
        public string Name;
        public string Branch;
        public SkillEffect Effect;
        public double PerLevel;
        public int MaxLevel;
        public double BaseCost;
        public double CostGrowth;
        public List<string> Prereq = new List<string>();
        public double X;
        public double Y;
        public int RequiresLayer; // zero based
        public int ForLayer = -1; // layer_pct only, zero based
        public string Desc;

        public double CostAt(int level) => BaseCost * Math.Pow(CostGrowth, level);
    }

    public sealed class AchievementDef
    {
        public string Id;
        public string Name;
        public string Cond;
        public string Reward;
    }

    public sealed class RareDef
    {
        public string Id;
        public string Name;
        public int Layer; // zero based
        public int Rank;  // 0 rare, 1 epic, 2 legendary: selects the bonus type
        public string Rarity;
        public double DropChance;
        public string Bonus;
    }

    public enum MilestoneKind { damage, speed, crit, trait }

    /// <summary>What a family earns for owning a given number of drills.</summary>
    public sealed class MilestoneDef
    {
        public MilestoneKind Kind;
        public double Amount;
        public string Desc;
    }

    public sealed class BonusDef
    {
        public SkillEffect Effect;
        public double Amount;
        public string Desc;
    }

    public sealed class SchoolDef
    {
        public string Id;
        public string Name;
        public string Branch;
        public double BranchDiscount;
        public string Desc;
        public List<BonusDef> Bonuses = new List<BonusDef>();
        public BonusDef Reward;
    }

    public sealed class FactoryDef
    {
        public string Id;
        public string Name;
        public SkillEffect Effect;
        public double PerLevel;
        public int MaxLevel;
        public double OreCost;
        public double OreGrowth;
        public string Desc;
    }

    public enum RebirthPerk { coins, root, drills, hangar, skills }

    public sealed class RebirthMilestone
    {
        public int Count;
        public RebirthPerk Perk;
        public double Amount;
        public int Tier;
        public string Desc;
    }

    public enum EventKind { coins, frenzy, damage, speed, lose }

    public sealed class EventOutcome
    {
        public EventKind Kind;
        public double Weight;
        public double Amount;
        public double Seconds;
        public string Desc;
    }

    public sealed class EventTable
    {
        public double EveryMin;
        public double EveryMax;
        public double Lifetime;
        public double UnstableShare;
        public int UnstableFromLayer; // zero based
        public List<EventOutcome> Golden = new List<EventOutcome>();
        public List<EventOutcome> Unstable = new List<EventOutcome>();
    }

    /// <summary>
    /// The numbers from tools/Design/gen_design_data.py. The reader callback fetches a file by
    /// name, so Unity and the tests can supply the JSON their own way.
    /// </summary>
    public static class GameData
    {
        private const double OverlapStep = 1.6;

        public static List<LayerDef> Layers;
        public static List<FamilyDef> Families;
        public static List<string> Materials;
        public static double TierDamageMult;
        public static double BuyGrowth;
        public static List<SkillDef> Skills;
        public static List<AchievementDef> Achievements;
        public static List<RareDef> Rares;
        public static int OreCount;
        public static List<int> MilestoneCounts;
        public static List<List<MilestoneDef>> Milestones; // per family, one reward per count
        public static List<double> MasteryBlocks;          // blocks dug in a layer for each level
        public static double MasteryDamagePct;
        public static double MasteryValuePct;
        public static EventTable Events;
        public static List<RebirthMilestone> RebirthMilestones;
        public static List<SchoolDef> Schools;
        public static double CrystalSeconds;
        public static double CrystalCap;
        public static int FactoryCrystalEvery;
        public static List<FactoryDef> Factory;

        private static Dictionary<string, SkillDef> skillsById;

        public static bool Loaded => Layers != null;
        public static int TotalRows => Layers[Layers.Count - 1].RowEnd;
        public static int Tiers => Materials.Count;
        public static LayerDef LastLayer => Layers[Layers.Count - 1];

        public static void Load(Func<string, string> read)
        {
            LoadLayers(read("layers"));
            LoadDrills(read("drills"));
            LoadSkills(read("skills"));
            LoadAchievements(read("achievements"));
            LoadRares(read("rare_items"));
            LoadMilestones(read("milestones"));
            LoadPhase3(read("phase3"));
        }

        public static SkillDef Skill(string id) => skillsById.TryGetValue(id, out var s) ? s : null;

        /// <summary>The layer a row lies in; rows past the centre stay in the last layer.</summary>
        public static int LayerOfRow(int row)
        {
            if (row <= 0) return 0;
            for (int i = 0; i < Layers.Count; i++)
                if (row < Layers[i].RowEnd) return i;
            return Layers.Count - 1;
        }

        /// <summary>Real depth in metres: rows map linearly onto their layer's real span.</summary>
        public static double MetresAt(int row)
        {
            if (row <= 0) return 0;
            var layer = Layers[LayerOfRow(row)];
            double t = (row - layer.RowStart) / (double)(layer.RowEnd - layer.RowStart);
            return layer.DepthStartM + (layer.DepthEndM - layer.DepthStartM) * Math.Min(1, t);
        }

        public static string FormatDepth(int row)
        {
            double m = MetresAt(row);
            if (m < 1000) return $"{m:0} 米";
            return m < 100000 ? $"{m / 1000:0.0} 千米" : $"{m / 1000:0} 千米";
        }

        private static string Str(Dictionary<string, object> o, string k) => (string)o[k];
        private static double Num(Dictionary<string, object> o, string k) => (double)o[k];
        private static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;
        private static List<object> Arr(object o) => (List<object>)o;

        private static void LoadLayers(string json)
        {
            Layers = new List<LayerDef>();
            OreCount = 0;
            foreach (var item in Arr(MiniJson.Parse(json)))
            {
                var o = Obj(item);
                var boss = Obj(o["boss"]);
                var layer = new LayerDef
                {
                    Index = Layers.Count,
                    Name = Str(o, "name"),
                    DepthStartM = Num(o, "depthStartM"),
                    DepthEndM = Num(o, "depthEndM"),
                    RowStart = (int)Num(o, "rowStart"),
                    RowEnd = (int)Num(o, "rowEnd"),
                    Mechanic = Str(o, "mechanic"),
                    BossName = Str(boss, "name"),
                    BossHpMult = Num(boss, "hpMult"),
                    BossSeconds = Num(boss, "timeLimitSec"),
                };
                foreach (var oreItem in Arr(o["ores"]))
                {
                    var ore = Obj(oreItem);
                    layer.Ores.Add(new OreDef
                    {
                        Index = OreCount++, Name = Str(ore, "name"), ValueMult = Num(ore, "valueMult"), Rate = Num(ore, "rate"),
                    });
                }
                Layers.Add(layer);
            }
        }

        private static void LoadDrills(string json)
        {
            var root = Obj(MiniJson.Parse(json));
            TierDamageMult = Num(root, "tierDamageMult");
            BuyGrowth = Num(root, "buyPriceGrowth");
            Materials = new List<string>();
            foreach (var m in Arr(root["materials"])) Materials.Add((string)m);
            Families = new List<FamilyDef>();
            foreach (var item in Arr(root["families"]))
            {
                var o = Obj(item);
                Families.Add(new FamilyDef
                {
                    Index = Families.Count,
                    Id = Str(o, "id"),
                    Name = Str(o, "name"),
                    Damage = Num(o, "damage"),
                    HitsPerSecond = Num(o, "hitsPerSec"),
                    Trait = Str(o, "trait"),
                    UnlockLayer = (int)Num(o, "unlockLayer") - 1,
                    BasePrice = Num(o, "basePrice"),
                });
            }
        }

        private static void LoadSkills(string json)
        {
            Skills = new List<SkillDef>();
            skillsById = new Dictionary<string, SkillDef>();
            foreach (var item in Arr(MiniJson.Parse(json)))
            {
                var o = Obj(item);
                var skill = new SkillDef
                {
                    Id = Str(o, "id"),
                    Name = Str(o, "name"),
                    Branch = Str(o, "branch"),
                    Effect = (SkillEffect)Enum.Parse(typeof(SkillEffect), Str(o, "effect")),
                    PerLevel = Num(o, "perLevel"),
                    MaxLevel = (int)Num(o, "maxLevel"),
                    BaseCost = Num(o, "baseCost"),
                    CostGrowth = Num(o, "costGrowth"),
                    X = Num(o, "x"),
                    Y = Num(o, "y"),
                    RequiresLayer = (int)Num(o, "requiresLayer") - 1,
                    Desc = Str(o, "desc"),
                };
                if (o.TryGetValue("layer", out var forLayer)) skill.ForLayer = (int)(double)forLayer - 1;
                foreach (var p in Arr(o["prereq"])) skill.Prereq.Add((string)p);
                Skills.Add(skill);
                skillsById[skill.Id] = skill;
            }
            SeparateOverlaps();
        }

        // Some nodes share or nearly share a coordinate in the data; push later ones apart vertically.
        private static void SeparateOverlaps()
        {
            for (int i = 1; i < Skills.Count; i++)
            {
                var skill = Skills[i];
                bool moved = true;
                while (moved)
                {
                    moved = false;
                    for (int j = 0; j < i; j++)
                    {
                        var other = Skills[j];
                        if (Math.Abs(skill.X - other.X) >= OverlapStep || Math.Abs(skill.Y - other.Y) >= OverlapStep) continue;
                        skill.Y = other.Y + (skill.Y >= 0 ? OverlapStep : -OverlapStep);
                        moved = true;
                    }
                }
            }
        }

        private static void LoadAchievements(string json)
        {
            Achievements = new List<AchievementDef>();
            foreach (var item in Arr(MiniJson.Parse(json)))
            {
                var o = Obj(item);
                Achievements.Add(new AchievementDef
                {
                    Id = Str(o, "id"), Name = Str(o, "name"), Cond = Str(o, "cond"), Reward = Str(o, "reward"),
                });
            }
        }

        private static void LoadMilestones(string json)
        {
            var root = Obj(MiniJson.Parse(json));
            MilestoneCounts = new List<int>();
            foreach (var count in Arr(root["familyCounts"])) MilestoneCounts.Add((int)(double)count);

            Milestones = new List<List<MilestoneDef>>();
            foreach (var family in Families) Milestones.Add(new List<MilestoneDef>());
            foreach (var item in Arr(root["families"]))
            {
                var o = Obj(item);
                var rewards = Milestones[Families.FindIndex(f => f.Id == Str(o, "id"))];
                foreach (var rewardItem in Arr(o["rewards"]))
                {
                    var reward = Obj(rewardItem);
                    rewards.Add(new MilestoneDef
                    {
                        Kind = (MilestoneKind)Enum.Parse(typeof(MilestoneKind), Str(reward, "kind")),
                        Amount = Num(reward, "amount"),
                        Desc = Str(reward, "desc"),
                    });
                }
            }

            var mastery = Obj(root["mastery"]);
            MasteryBlocks = new List<double>();
            foreach (var blocks in Arr(mastery["blocks"])) MasteryBlocks.Add((double)blocks);
            MasteryDamagePct = Num(mastery, "damagePct");
            MasteryValuePct = Num(mastery, "valuePct");
        }

        // hand-written, unlike the rest: it holds no formulas, so it has no generator
        private static void LoadPhase3(string json)
        {
            var root = Obj(MiniJson.Parse(json));
            LoadEvents(Obj(root["events"]));

            var crystals = Obj(root["crystals"]);
            CrystalSeconds = Num(crystals, "seconds");
            CrystalCap = Num(crystals, "cap");
            FactoryCrystalEvery = (int)Num(crystals, "factoryEvery");
            Factory = new List<FactoryDef>();
            foreach (var item in Arr(root["factory"]))
            {
                var o = Obj(item);
                Factory.Add(new FactoryDef
                {
                    Id = Str(o, "id"),
                    Name = Str(o, "name"),
                    Effect = (SkillEffect)Enum.Parse(typeof(SkillEffect), Str(o, "effect")),
                    PerLevel = Num(o, "perLevel"),
                    MaxLevel = (int)Num(o, "maxLevel"),
                    OreCost = Num(o, "oreCost"),
                    OreGrowth = Num(o, "oreGrowth"),
                    Desc = Str(o, "desc"),
                });
            }

            Schools = new List<SchoolDef>();
            foreach (var item in Arr(root["schools"]))
            {
                var o = Obj(item);
                var school = new SchoolDef
                {
                    Id = Str(o, "id"),
                    Name = Str(o, "name"),
                    Branch = Str(o, "branch"),
                    BranchDiscount = Num(o, "branchDiscount"),
                    Desc = Str(o, "desc"),
                    Reward = LoadBonus(o["reward"]),
                };
                foreach (var bonus in Arr(o["bonuses"])) school.Bonuses.Add(LoadBonus(bonus));
                Schools.Add(school);
            }

            RebirthMilestones = new List<RebirthMilestone>();
            foreach (var item in Arr(root["rebirthMilestones"]))
            {
                var o = Obj(item);
                RebirthMilestones.Add(new RebirthMilestone
                {
                    Count = (int)Num(o, "count"),
                    Perk = (RebirthPerk)Enum.Parse(typeof(RebirthPerk), Str(o, "perk")),
                    Amount = Num(o, "amount"),
                    Tier = (int)Num(o, "tier"),
                    Desc = Str(o, "desc"),
                });
            }
        }

        private static BonusDef LoadBonus(object item)
        {
            var o = Obj(item);
            return new BonusDef
            {
                Effect = (SkillEffect)Enum.Parse(typeof(SkillEffect), Str(o, "effect")),
                Amount = Num(o, "amount"),
                Desc = o.TryGetValue("desc", out var desc) ? (string)desc : "",
            };
        }

        private static void LoadEvents(Dictionary<string, object> o)
        {
            Events = new EventTable
            {
                EveryMin = Num(o, "everyMin"),
                EveryMax = Num(o, "everyMax"),
                Lifetime = Num(o, "lifetime"),
                UnstableShare = Num(o, "unstableShare"),
                UnstableFromLayer = (int)Num(o, "unstableFromLayer") - 1,
            };
            LoadOutcomes(Arr(o["golden"]), Events.Golden);
            LoadOutcomes(Arr(o["unstable"]), Events.Unstable);
        }

        private static void LoadOutcomes(List<object> items, List<EventOutcome> into)
        {
            foreach (var item in items)
            {
                var o = Obj(item);
                into.Add(new EventOutcome
                {
                    Kind = (EventKind)Enum.Parse(typeof(EventKind), Str(o, "kind")),
                    Weight = Num(o, "weight"),
                    Amount = Num(o, "amount"),
                    Seconds = Num(o, "seconds"),
                    Desc = Str(o, "desc"),
                });
            }
        }

        private static void LoadRares(string json)
        {
            Rares = new List<RareDef>();
            foreach (var item in Arr(MiniJson.Parse(json)))
            {
                var o = Obj(item);
                Rares.Add(new RareDef
                {
                    Id = Str(o, "id"),
                    Name = Str(o, "name"),
                    Layer = (int)Num(o, "layer") - 1,
                    Rank = Rares.Count % 3,
                    Rarity = Str(o, "rarity"),
                    DropChance = Num(o, "dropChance"),
                    Bonus = Str(o, "bonus"),
                });
            }
        }
    }
}
