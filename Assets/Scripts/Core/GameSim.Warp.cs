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

        // Lightning hits somewhere along the lead drill's row and breaks the player's rhythm.
        private void AdvanceStorm(double dt)
        {
            if (Planet.Rule != PlanetRule.storm || (stormClock += dt) < Balance.StormSeconds) return;
            stormClock = 0;
            Combo = 0;
            // near the lead drill, so the strike lands where the player is looking
            int col = Math.Max(0, Math.Min(PixelWorld.Width / Tile - 1, Lead.Body.Column + random.Next(-Balance.StormSpread, Balance.StormSpread + 1)));
            Events.Add(new SimEvent { Kind = SimEventKind.StormStrike, Row = Lead.Body.FeetRow, Col = col });
            Explode(Lead.Body.FeetRow, col, Balance.StormRadius);
        }

        private double stormClock;

        public bool HasTide => Planet.Rule == PlanetRule.tide;

        public bool TideHigh => simTime % (Balance.TideSeconds * 2) < Balance.TideSeconds;

        public double TideSecondsLeft => Balance.TideSeconds - simTime % Balance.TideSeconds;

        private double TideDamage => !HasTide ? 1 : TideHigh ? Balance.TideHigh : Balance.TideLow;

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
