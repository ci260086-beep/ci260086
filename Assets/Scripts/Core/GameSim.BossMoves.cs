using System;

namespace DeepDrill.Core
{
    /// <summary>Each guardian's move: it warns, strikes, and wears off, again and again through the fight.</summary>
    public sealed partial class GameSim
    {
        private double moveClock;
        private double moveWindup;
        private double moveLeft;

        public BossMoveDef CurrentBossMove => CurrentBoss == null ? null : GameData.BossMoves[CurrentBoss.Index];
        public bool BossMoveWindingUp => BossActive && moveWindup > 0;
        public bool BossMoveRunning => BossActive && moveLeft > 0;

        /// <summary>Drills caught by a guardian stop swinging until it lets go or the player taps them free.</summary>
        public bool DrillsBound => Moving(BossMoveKind.bind);

        private double MoveDamage =>
            Moving(BossMoveKind.immune) ? 0 : Moving(BossMoveKind.weaken) ? CurrentBossMove.Amount : 1;

        private double MoveSpeed => Moving(BossMoveKind.slow) ? CurrentBossMove.Amount : 1;

        private bool Moving(BossMoveKind kind) => BossMoveRunning && CurrentBossMove.Kind == kind;

        /// <summary>Starts the guardian's wind-up now; for the demo driver's screenshot.</summary>
        public void HurryBossMove() => moveClock = 0;

        /// <summary>Ends the fight at once; for the demo driver's screenshot.</summary>
        public void DefeatBossNow()
        {
            if (BossActive) DefeatBoss();
        }

        private void ResetBossMove()
        {
            moveClock = CurrentBossMove?.Every ?? 0;
            moveWindup = moveLeft = 0;
        }

        private void AdvanceBossMove(double dt)
        {
            var move = CurrentBossMove;
            if (moveLeft > 0)
            {
                moveLeft -= dt;
                if (moveLeft <= 0) EndBossMove(move);
                return;
            }
            if (moveWindup > 0)
            {
                moveWindup -= dt;
                if (moveWindup <= 0) StrikeBossMove(move);
                return;
            }
            moveClock -= dt;
            if (moveClock > 0) return;
            moveWindup = move.Windup;
            Events.Add(new SimEvent { Kind = SimEventKind.BossMoveWarned, Amount = CurrentBoss.Index });
        }

        private void StrikeBossMove(BossMoveDef move)
        {
            moveWindup = 0;
            moveLeft = move.Seconds;
            Events.Add(new SimEvent { Kind = SimEventKind.BossMoveStruck, Amount = CurrentBoss.Index });
            switch (move.Kind)
            {
                case BossMoveKind.armor:
                    BossArmor += BossMaxHp * Balance.BossArmorRatio * move.Amount;
                    break;
                case BossMoveKind.heal:
                    BossHp = Math.Min(BossMaxHp, BossHp + BossMaxHp * move.Amount);
                    break;
                case BossMoveKind.push:
                    PushDrills(move.Amount);
                    break;
            }
            if (moveLeft <= 0) EndBossMove(move);
        }

        private void EndBossMove(BossMoveDef move)
        {
            moveLeft = 0;
            moveClock = move.Every;
            Events.Add(new SimEvent { Kind = SimEventKind.BossMoveEnded, Amount = CurrentBoss.Index });
        }

        private void PushDrills(double speed)
        {
            foreach (var unit in Units)
                unit.Body.VelocityX += (random.Next(2) == 0 ? -1 : 1) * speed;
        }

        // Each tap on a caught drill wears the hold down a little.
        private void TapFree()
        {
            if (DrillsBound && moveLeft > Balance.TapFreeFloor)
                moveLeft = Math.Max(Balance.TapFreeFloor, moveLeft - Balance.TapFreeSeconds);
        }
    }
}
