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

    /// <summary>The holes dug in one chunk of ground, run-length coded then base64.</summary>
    [Serializable]
    public sealed class DugChunk
    {
        public long key;
        public string holes;
    }

    [Serializable]
    public sealed class RareCount
    {
        public string id;
        public int count;
    }

    /// <summary>Everything that goes into the save file. Fields stay public for JsonUtility.</summary>
    [Serializable]
    public sealed class GameState
    {
        public const int CurrentVersion = 6;

        public int version = CurrentVersion;
        public int seed = 1;
        public double coins;
        public double ores;
        public int depth;
        public int maxDepth;
        public double totalIncome;
        public double incomePerSecond;
        public double runSeconds;
        public double peakShardRate;
        public int shards;
        public long lastSaveUnixSeconds;
        public int bossesDefeated;
        public bool drillPlaced;
        public double drillX;
        public double drillY;
        public List<SkillLevel> skills = new List<SkillLevel>();
        public List<int> drills = new List<int>();  // one DrillCode per owned drill
        public List<int> bought = new List<int>();  // per family, drives the price
        public List<DugChunk> dug = new List<DugChunk>();

        // lifetime records: these survive a rebirth
        public int maxDepthEver;
        public int bossesEver;
        public double mergesEver;
        public int bestTier;
        public int bestCombo;
        public int bestChain;
        public int rebirths;
        public List<RareCount> rares = new List<RareCount>();
        public List<string> achievements = new List<string>();
        public List<int> familyBest = new List<int>();     // most of a family bought in one run
        public List<double> layerPixels = new List<double>(); // pixels ever dug per layer
        public int eventsClaimed;

        // the player's automation rules
        public bool autoSkills;
        public bool autoDrills;
        public bool autoDrillsFirst;
        public int autoReservePct;
        public int autoRebirthRow;

        public int school = -1;  // this run's school, picked after a rebirth
        public List<int> schoolsCleared = new List<int>();

        public double crystals;
        public List<int> factory = new List<int>(); // level per building

        public int research = -1;
        public double researchLeft;
        public List<int> researchDone = new List<int>();
        public int explorers;
        public List<int> expeditionTiers = new List<int>();     // one entry per explorer away
        public List<double> expeditionLeft = new List<double>();

        public int challenge = -1;
        public List<int> challengesDone = new List<int>();

        public int warps;
        public int starPoints;
        public List<int> constellation = new List<int>(); // level per star

        public bool AnyBossDown => bossesDefeated > 0;

        public int BoughtOf(int family) => family < bought.Count ? bought[family] : 0;

        public void AddBought(int family)
        {
            while (bought.Count <= family) bought.Add(0);
            bought[family]++;
            while (familyBest.Count <= family) familyBest.Add(0);
            familyBest[family] = Math.Max(familyBest[family], bought[family]);
        }

        public int FamilyBestOf(int family) => family < familyBest.Count ? familyBest[family] : 0;

        public double LayerPixelsOf(int layer) => layer < layerPixels.Count ? layerPixels[layer] : 0;

        public void AddLayerPixel(int layer)
        {
            while (layerPixels.Count <= layer) layerPixels.Add(0);
            layerPixels[layer]++;
        }

        public int RareCountOf(string id)
        {
            foreach (var r in rares)
                if (r.id == id) return r.count;
            return 0;
        }

        public void AddRare(string id)
        {
            foreach (var r in rares)
            {
                if (r.id != id) continue;
                r.count++;
                return;
            }
            rares.Add(new RareCount { id = id, count = 1 });
        }

        /// <summary>Brings a loaded save up to the current version.</summary>
        public void Upgrade()
        {
            if (version < 4)
            {
                // version 4 replaced the skill list and the world: old progress points at nothing
                skills.Clear();
                depth = maxDepth = 0;
                drillPlaced = false;
                bossesDefeated = 0;
            }
            if (version < 6) familyBest = new List<int>(bought);
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

    /// <summary>One owned drill packed into an int: family * 100 + tier (tier from zero).</summary>
    public static class DrillCode
    {
        public static int Make(int family, int tier) => family * 100 + tier;
        public static int Family(int code) => code / 100;
        public static int Tier(int code) => code % 100;
    }
}
