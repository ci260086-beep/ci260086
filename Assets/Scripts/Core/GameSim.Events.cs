using System;

namespace DeepDrill.Core
{
    public enum SurfaceEvent { None, Golden, Unstable }

    /// <summary>The glowing things that surface now and then: golden luck and the risky red core.</summary>
    public sealed partial class GameSim
    {
        private double eventClock = -1;
        private double damageBuffLeft;
        private double damageBuffMult = 1;
        private double speedBuffLeft;
        private double speedBuffMult = 1;

        public SurfaceEvent ActiveEvent { get; private set; }
        public double EventTimeLeft { get; private set; }
        public double DamageBuffLeft => damageBuffLeft;
        public double DamageBuffMult => damageBuffLeft > 0 ? damageBuffMult : 1;
        public double SpeedBuffLeft => speedBuffLeft;
        public double SpeedBuffMult => speedBuffLeft > 0 ? speedBuffMult : 1;

        private void AdvanceEvents(double dt)
        {
            if (damageBuffLeft > 0) damageBuffLeft -= dt;
            if (speedBuffLeft > 0) speedBuffLeft -= dt;
            if (!AutoDrillUnlocked) return;

            if (ActiveEvent != SurfaceEvent.None)
            {
                EventTimeLeft -= dt;
                if (EventTimeLeft <= 0) ActiveEvent = SurfaceEvent.None;
                return;
            }

            if (eventClock < 0) eventClock = NextEventDelay();
            eventClock -= dt;
            if (eventClock > 0) return;
            eventClock = -1;
            SpawnEvent();
        }

        private double NextEventDelay()
        {
            var e = GameData.Events;
            return e.EveryMin + random.NextDouble() * (e.EveryMax - e.EveryMin);
        }

        private void SpawnEvent()
        {
            var e = GameData.Events;
            bool unstable = ReachedLayer >= e.UnstableFromLayer && random.NextDouble() < e.UnstableShare;
            ForceEvent(unstable ? SurfaceEvent.Unstable : SurfaceEvent.Golden);
        }

        /// <summary>Puts an event on screen; for tests and the demo driver.</summary>
        public void ForceEvent(SurfaceEvent kind)
        {
            ActiveEvent = kind;
            EventTimeLeft = GameData.Events.Lifetime;
            Events.Add(new SimEvent { Kind = SimEventKind.SurfaceEventAppeared, Amount = (int)kind });
        }

        /// <summary>The player taps it: rolls one outcome from its table and applies it.</summary>
        public EventOutcome ClaimEvent()
        {
            if (ActiveEvent == SurfaceEvent.None) return null;
            var table = ActiveEvent == SurfaceEvent.Golden ? GameData.Events.Golden : GameData.Events.Unstable;
            ActiveEvent = SurfaceEvent.None;

            double total = 0;
            foreach (var o in table) total += o.Weight;
            double roll = random.NextDouble() * total;
            var outcome = table[table.Count - 1];
            foreach (var o in table)
            {
                roll -= o.Weight;
                if (roll >= 0) continue;
                outcome = o;
                break;
            }
            Apply(outcome);
            return outcome;
        }

        /// <summary>Applies one outcome directly; the rolled path and the tests both land here.</summary>
        public void Apply(EventOutcome outcome)
        {
            switch (outcome.Kind)
            {
                case EventKind.coins:
                    Earn(Math.Max(State.incomePerSecond, Balance.EventCoinFloor) * outcome.Amount);
                    break;
                case EventKind.frenzy:
                    FrenzyEnergy = 1;
                    break;
                case EventKind.damage:
                    damageBuffMult = outcome.Amount;
                    damageBuffLeft = outcome.Seconds;
                    break;
                case EventKind.speed:
                    speedBuffMult = outcome.Amount;
                    speedBuffLeft = outcome.Seconds;
                    break;
                case EventKind.lose:
                    State.coins *= 1 - outcome.Amount;
                    break;
            }
            State.eventsClaimed++;
            Events.Add(new SimEvent { Kind = SimEventKind.SurfaceEventClaimed });
        }
    }
}
