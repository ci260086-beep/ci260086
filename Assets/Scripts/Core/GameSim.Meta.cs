using System;

namespace DeepDrill.Core
{
    /// <summary>Everything that outlives a run: rebirth milestones, automation and the permanent bonuses.</summary>
    public sealed partial class GameSim
    {
        private const double CheapestRing = 60;

        private double metaDamage;
        private double metaIncome;

        public int RebirthMilestonesReached
        {
            get
            {
                int n = 0;
                foreach (var m in GameData.RebirthMilestones)
                    if (State.rebirths >= m.Count) n++;
                return n;
            }
        }

        public bool HasRebirthMilestone(RebirthPerk perk)
        {
            foreach (var m in GameData.RebirthMilestones)
                if (m.Perk == perk && State.rebirths >= m.Count) return true;
            return false;
        }

        public bool AutomationUnlocked => HasRebirthMilestone(RebirthPerk.root);

        // Permanent bonuses go through the same effects as skills, except damage and income, which
        // stack as their own factor so they stay worth having next to thousands of percent of skills.
        private void AddPermanent(SkillEffect effect, double amount)
        {
            if (effect == SkillEffect.dmg_pct) metaDamage += amount;
            else if (effect == SkillEffect.gold_pct) metaIncome += amount;
            else effects[(int)effect] += amount;
        }

        private void RefreshPermanent()
        {
            metaDamage = metaIncome = 0;
            ApplySchools();
            ApplyFactory();
            ApplyResearch();
            ApplyChallenges();
            ApplyConstellation();
        }

        // The gifts a new run starts with, once enough rebirths are behind the player.
        private void GrantRunStart()
        {
            foreach (var m in GameData.RebirthMilestones)
            {
                if (State.rebirths < m.Count) continue;
                switch (m.Perk)
                {
                    case RebirthPerk.coins:
                        State.coins += m.Amount;
                        break;
                    case RebirthPerk.root:
                        State.SetLevel(RootSkill, Math.Max(1, State.LevelOf(RootSkill)));
                        break;
                    case RebirthPerk.hangar:
                        foreach (var skill in GameData.Skills)
                            if (skill.Effect == SkillEffect.auto_merge || skill.Effect == SkillEffect.auto_buy)
                                State.SetLevel(skill.Id, 1);
                        break;
                    case RebirthPerk.skills:
                        foreach (var skill in GameData.Skills)
                            if (skill.Id != RootSkill && skill.BaseCost <= CheapestRing)
                                State.SetLevel(skill.Id, Math.Min(skill.MaxLevel, (int)m.Amount));
                        break;
                }
            }
        }

        private void GrantStartDrills()
        {
            foreach (var m in GameData.RebirthMilestones)
            {
                if (m.Perk != RebirthPerk.drills || State.rebirths < m.Count) continue;
                for (int i = 0; i < m.Amount; i++) State.drills.Add(DrillCode.Make(0, m.Tier));
            }
        }

        // The player's own rules, run once a second. Spending keeps the reserve untouched.
        private void RunAutomation()
        {
            if (!AutomationUnlocked) return;
            if (State.autoRebirthRow > 0 && State.maxDepth >= State.autoRebirthRow && CanRebirth)
            {
                Rebirth();
                return;
            }

            double floor = State.coins * State.autoReservePct / 100;
            if (State.autoDrillsFirst)
            {
                bool changed = AutoBuyDrills(floor);
                AutoBuySkills(floor);
                if (changed) RebuildUnits();
                return;
            }
            AutoBuySkills(floor);
            if (AutoBuyDrills(floor)) RebuildUnits();
        }

        private void AutoBuySkills(double floor)
        {
            if (!State.autoSkills) return;
            for (int i = 0; i < AutoBuysPerTick; i++)
            {
                SkillDef best = null;
                foreach (var skill in GameData.Skills)
                {
                    if (!CanBuy(skill) || State.coins - SkillCost(skill) < floor) continue;
                    if (best == null || SkillCost(skill) < SkillCost(best)) best = skill;
                }
                if (best == null) return;
                Buy(best.Id);
            }
        }

        private bool AutoBuyDrills(double floor)
        {
            if (!State.autoDrills) return false;
            bool changed = false;
            for (int i = 0; i < AutoBuysPerTick; i++)
            {
                int cheapest = -1;
                for (int f = 0; f < GameData.Families.Count; f++)
                    if (CanBuyDrill(f) && State.coins - DrillPrice(f) >= floor && (cheapest < 0 || DrillPrice(f) < DrillPrice(cheapest))) cheapest = f;
                if (cheapest < 0) break;
                State.coins -= DrillPrice(cheapest);
                State.AddBought(cheapest);
                State.drills.Add(DrillCode.Make(cheapest, Math.Min(BuyTier, GameData.Tiers - 1)));
                changed = true;
            }
            return changed;
        }
    }
}
