using System;

namespace DeepDrill.Core
{
    /// <summary>Drilling schools: one is picked per run, and finishing the planet with it pays for good.</summary>
    public sealed partial class GameSim
    {
        public SchoolDef CurrentSchool => State.school >= 0 ? GameData.Schools[State.school] : null;

        public bool SchoolsUnlocked => State.rebirths >= 1;

        public SchoolDef SecondSchool => State.secondSchool >= 0 ? GameData.Schools[State.secondSchool] : null;

        public bool CanChooseSchool(int school) => SchoolsUnlocked && State.school < 0 && school >= 0 && school < GameData.Schools.Count && Fits(school);

        // A school with a drill limit cannot be taken with the hangar already over it.
        private bool Fits(int school) =>
            GameData.Schools[school].Trait != SchoolTrait.few_drills || State.drills.Count <= Balance.FewDrillsCap;

        /// <summary>A cleared school can join the run's first; its bonuses and rule both apply, its branch stays full price.</summary>
        public bool CanChooseSecond(int school)
        {
            if (State.school < 0 || State.secondSchool >= 0 || school == State.school || !SchoolCleared(school) || !Fits(school)) return false;
            var pair = (GameData.Schools[school].Trait, CurrentSchool.Trait);
            return pair != (SchoolTrait.no_merge, SchoolTrait.few_drills) && pair != (SchoolTrait.few_drills, SchoolTrait.no_merge);
        }

        public bool ChooseSecond(int school)
        {
            if (!CanChooseSecond(school)) return false;
            State.secondSchool = school;
            RefreshEffects();
            return true;
        }

        public bool HasTrait(SchoolTrait trait) => CurrentSchool?.Trait == trait || SecondSchool?.Trait == trait;

        /// <summary>How many more drills this run's schools allow.</summary>
        public int DrillRoom => HasTrait(SchoolTrait.few_drills) ? Balance.FewDrillsCap - State.drills.Count : int.MaxValue;

        public bool ChooseSchool(int school)
        {
            if (!CanChooseSchool(school)) return false;
            State.school = school;
            RefreshEffects();
            return true;
        }

        public bool SchoolCleared(int school) => State.schoolsCleared.Contains(school);

        /// <summary>The price of a skill's next level; the school's own branch costs less.</summary>
        public double SkillCost(SkillDef skill)
        {
            double cost = skill.CostAt(State.LevelOf(skill.Id));
            var school = CurrentSchool;
            return school != null && school.Branch == skill.Branch ? cost * (1 - school.BranchDiscount / 100) : cost;
        }

        private void ApplySchools()
        {
            CurrentSchool?.Bonuses.ForEach(b => AddPermanent(b.Effect, b.Amount));
            SecondSchool?.Bonuses.ForEach(b => AddPermanent(b.Effect, b.Amount));
            oreOnly = HasTrait(SchoolTrait.ore_only);
            foreach (int cleared in State.schoolsCleared)
                AddPermanent(GameData.Schools[cleared].Reward.Effect, GameData.Schools[cleared].Reward.Amount);
        }

        // The blast school's charge goes off under the lead drill on a steady beat.
        private void AdvancePulse(double dt)
        {
            if (!HasTrait(SchoolTrait.pulse) || (pulseClock += dt) < Balance.PulseSeconds) return;
            pulseClock = 0;
            Explode(Lead.Body.FeetRow, Lead.Body.Column, Balance.PulseRadius);
        }

        private bool oreOnly;
        private double pulseClock;

        // Reaching the centre with a school counts once for that school.
        private void CheckSchoolCleared()
        {
            if (State.school < 0 || State.maxDepth < GameData.TotalRows || SchoolCleared(State.school)) return;
            State.schoolsCleared.Add(State.school);
            RefreshEffects();
            Events.Add(new SimEvent { Kind = SimEventKind.SchoolCleared, Amount = State.school });
        }
    }
}
