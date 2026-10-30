using System;

namespace DeepDrill.Core
{
    /// <summary>The institute: research that takes real time, and rescued explorers sent on expeditions.</summary>
    public sealed partial class GameSim
    {
        public ResearchDef ActiveResearch => State.research >= 0 ? GameData.Research[State.research] : null;

        public bool ResearchDone(int index) => State.researchDone.Contains(index);

        public bool CanResearch(int index) =>
            State.research < 0 && !ResearchDone(index) && Crystals >= GameData.Research[index].Crystals;

        public bool StartResearch(int index)
        {
            if (!CanResearch(index)) return false;
            State.crystals -= GameData.Research[index].Crystals;
            State.research = index;
            State.researchLeft = GameData.Research[index].Seconds;
            return true;
        }

        public bool CanRushResearch => State.research >= 0 && Crystals >= 1;

        /// <summary>A crystal takes a slice off the running research.</summary>
        public bool RushResearch()
        {
            if (!CanRushResearch) return false;
            State.crystals -= 1;
            State.researchLeft -= GameData.ResearchRushSeconds;
            FinishResearchIfDone();
            return true;
        }

        public int ExplorersIdle => State.explorers - State.expeditionTiers.Count;

        public bool CanSendExpedition(int tier) => ExplorersIdle > 0 && tier >= 0 && tier < GameData.Expeditions.Count;

        public bool SendExpedition(int tier)
        {
            if (!CanSendExpedition(tier)) return false;
            State.expeditionTiers.Add(tier);
            State.expeditionLeft.Add(GameData.Expeditions[tier].Seconds);
            return true;
        }

        private void AdvanceLab(double seconds)
        {
            if (State.research >= 0)
            {
                State.researchLeft -= seconds;
                FinishResearchIfDone();
            }
            for (int i = State.expeditionLeft.Count - 1; i >= 0; i--)
            {
                State.expeditionLeft[i] -= seconds;
                if (State.expeditionLeft[i] > 0) continue;
                int tier = State.expeditionTiers[i];
                State.expeditionLeft.RemoveAt(i);
                State.expeditionTiers.RemoveAt(i);
                FinishExpedition(tier);
            }
        }

        private void FinishResearchIfDone()
        {
            if (State.researchLeft > 0) return;
            int index = State.research;
            State.research = -1;
            State.researchDone.Add(index);
            RefreshEffects();
            Events.Add(new SimEvent { Kind = SimEventKind.ResearchDone, Amount = index });
        }

        // The riskier the trip the richer the haul. A failed one comes home with nothing, but it comes home.
        private void FinishExpedition(int tier)
        {
            var trip = GameData.Expeditions[tier];
            bool success = random.NextDouble() < trip.Success;
            if (success)
            {
                State.shards += trip.Shards;
                State.ores += trip.Ores;
                State.crystals = Math.Min(GameData.CrystalCap, State.crystals + trip.Crystals);
            }
            Events.Add(new SimEvent { Kind = SimEventKind.ExpeditionReturned, Amount = tier, Row = success ? 1 : 0 });
        }

        private void RollRescue(int row)
        {
            if (GameData.LayerOfRow(row) != Balance.RuinsLayer || random.NextDouble() >= GameData.RescueChance) return;
            State.explorers++;
            Events.Add(new SimEvent { Kind = SimEventKind.ExplorerRescued });
        }

        private void ApplyResearch()
        {
            foreach (int index in State.researchDone)
                AddPermanent(GameData.Research[index].Effect, GameData.Research[index].Amount);
        }
    }
}
