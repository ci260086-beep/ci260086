using System;

namespace DeepDrill.Core
{
    /// <summary>The second prestige: leave a finished planet for star points, a constellation and a new rule.</summary>
    public sealed partial class GameSim
    {
        public int PlanetIndex => State.warps % GameData.Planets.Count;
        public PlanetDef Planet => GameData.Planets[PlanetIndex];

        public bool CanWarp => State.maxDepthEver >= GameData.TotalRows && State.challenge < 0;

        public int StarPointsOnWarp => 1 + (int)Math.Log10(State.shards + 1);

        /// <summary>Trades the shards and the planet for star points. Everything earned for good stays.</summary>
        public bool Warp()
        {
            if (!CanWarp) return false;
            int gained = StarPointsOnWarp;
            State.starPoints += gained;
            State.warps++;
            State.shards = 0;
            State.maxDepthEver = 0;
            ResetRun();
            Events.Add(new SimEvent { Kind = SimEventKind.RunRestarted });
            Events.Add(new SimEvent { Kind = SimEventKind.Warped, Amount = gained });
            return true;
        }

        public int StarLevel(int node) => node < State.constellation.Count ? State.constellation[node] : 0;

        public int StarCost(int node) => GameData.Constellation[node].Cost * (StarLevel(node) + 1);

        public bool CanBuyStar(int node) =>
            StarLevel(node) < GameData.Constellation[node].MaxLevel && State.starPoints >= StarCost(node);

        public bool BuyStar(int node)
        {
            if (!CanBuyStar(node)) return false;
            State.starPoints -= StarCost(node);
            while (State.constellation.Count <= node) State.constellation.Add(0);
            State.constellation[node]++;
            RefreshEffects();
            return true;
        }

        private void ApplyConstellation()
        {
            for (int i = 0; i < GameData.Constellation.Count; i++)
                AddPermanent(GameData.Constellation[i].Effect, GameData.Constellation[i].PerLevel * StarLevel(i));
        }
    }
}
