namespace DeepDrill.Core
{
    /// <summary>Drilling schools: one is picked per run, and finishing the planet with it pays for good.</summary>
    public sealed partial class GameSim
    {
        public SchoolDef CurrentSchool => State.school >= 0 ? GameData.Schools[State.school] : null;

        public bool SchoolsUnlocked => State.rebirths >= 1;

        public bool CanChooseSchool(int school) => SchoolsUnlocked && State.school < 0 && school >= 0 && school < GameData.Schools.Count;

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
            foreach (int cleared in State.schoolsCleared)
                AddPermanent(GameData.Schools[cleared].Reward.Effect, GameData.Schools[cleared].Reward.Amount);
        }

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
