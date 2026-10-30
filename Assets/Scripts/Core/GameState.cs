using System;
using System.Collections.Generic;

namespace DeepDrill.Core
{
    [Serializable]
    public sealed class SkillLevel
    {
        public string id;
        public int level;
    }

    /// <summary>Everything that goes into the save file. Fields stay public for JsonUtility.</summary>
    [Serializable]
    public sealed class GameState
    {
        public const int CurrentVersion = 4;

        public int version = CurrentVersion;
        public int seed = 1;
        public double coins;
        public double ores;
        public int depth;
        public int maxDepth;
        public double totalIncome;
        public double incomePerSecond;
        public int shards;
        public long lastSaveUnixSeconds;
        public int bossesDefeated;
        public bool drillPlaced;
        public double drillX;
        public double drillY;
        public List<SkillLevel> skills = new List<SkillLevel>();
        public List<int> drillCounts = new List<int>();

        // lifetime records: these survive a rebirth
        public int maxDepthEver;
        public int bossesEver;
        public double mergesEver;
        public double oresEver;
        public int rebirths;
        public List<string> rareItems = new List<string>();
        public List<string> achievements = new List<string>();

        public bool AnyBossDown => bossesDefeated > 0;

        public double DrillsOwned
        {
            get
            {
                double sum = 0;
                foreach (int n in drillCounts) sum += n;
                return sum;
            }
        }

        public int DrillCount(int tier) => tier < drillCounts.Count ? drillCounts[tier] : 0;

        public void SetDrillCount(int tier, int count)
        {
            while (drillCounts.Count <= tier) drillCounts.Add(0);
            drillCounts[tier] = count;
        }

        /// <summary>Brings a loaded save up to the current version.</summary>
        public void Upgrade()
        {
            // the skill list was replaced in version 4, so old levels point at nothing
            if (version < 4) skills.Clear();
            version = CurrentVersion;
        }

        public int LevelOf(string id)
        {
            foreach (var s in skills)
                if (s.id == id) return s.level;
            return 0;
        }

        public void SetLevel(string id, int level)
        {
            foreach (var s in skills)
            {
                if (s.id != id) continue;
                s.level = level;
                return;
            }
            skills.Add(new SkillLevel { id = id, level = level });
        }
    }
}
