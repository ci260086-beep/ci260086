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
        public const int CurrentVersion = 3;

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
        public bool bossDefeated; // version 2 saves only; Upgrade() moves it into bossesDefeated
        public bool drillPlaced;
        public double drillX;
        public double drillY;
        public List<SkillLevel> skills = new List<SkillLevel>();

        public bool AnyBossDown => bossesDefeated > 0;

        /// <summary>Brings a loaded save up to the current version.</summary>
        public void Upgrade()
        {
            if (version < 3 && bossDefeated) bossesDefeated = 1;
            bossDefeated = false;
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
