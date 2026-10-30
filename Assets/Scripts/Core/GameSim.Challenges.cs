namespace DeepDrill.Core
{
    /// <summary>Challenge wells: a run under one harsh rule, with a depth to reach for a permanent reward.</summary>
    public sealed partial class GameSim
    {
        public ChallengeDef CurrentChallenge => State.challenge >= 0 ? GameData.Challenges[State.challenge] : null;

        public bool ChallengesUnlocked => ReachedLayer >= Balance.ChallengeLayer;

        public bool ChallengeDone(int index) => State.challengesDone.Contains(index);

        public bool CanStartChallenge(int index) => ChallengesUnlocked && State.challenge < 0 && !ChallengeDone(index);

        private bool Under(ChallengeRule rule) => CurrentChallenge != null && CurrentChallenge.Rule == rule;

        private double ChallengeDamage => Under(ChallengeRule.weak) ? CurrentChallenge.Amount : 1;

        private double ChallengePay => Under(ChallengeRule.poor) ? CurrentChallenge.Amount : 1;

        /// <summary>Gives up this run and starts the well from the top; nothing is paid for what is left behind.</summary>
        public bool StartChallenge(int index)
        {
            if (!CanStartChallenge(index)) return false;
            ResetRun();
            State.challenge = index;
            return true;
        }

        public bool AbandonChallenge()
        {
            if (State.challenge < 0) return false;
            State.challenge = -1;
            ResetRun();
            return true;
        }

        private void CheckChallenge()
        {
            var challenge = CurrentChallenge;
            if (challenge == null || State.maxDepth < challenge.TargetRow) return;
            int index = State.challenge;
            State.challengesDone.Add(index);
            State.challenge = -1;
            ResetRun();
            Events.Add(new SimEvent { Kind = SimEventKind.ChallengeDone, Amount = index });
        }

        private void ApplyChallenges()
        {
            foreach (int index in State.challengesDone)
                AddPermanent(GameData.Challenges[index].Reward.Effect, GameData.Challenges[index].Reward.Amount);
        }
    }
}
