using System;

namespace DeepDrill.Core
{
    /// <summary>
    /// Hand-written drill physics in tile units. X grows right, Y grows down, block (row, col) is the
    /// unit square centred on (col, row). Angle is degrees: 0 points straight down, negative tips the
    /// nose left, positive tips it right. <paramref name="solid"/> style callbacks answer (row, col).
    /// </summary>
    public sealed class DrillBody
    {
        public const double Gravity = 40;
        public const double MaxFallSpeed = 16;
        public const double SlideAcceleration = 18;
        public const double MaxSlideSpeed = 3.5;
        public const double GroundFriction = 14;
        public const double MaxTilt = 35;
        public const double TiltSpring = 70;
        public const double TiltDamping = 9;
        public const double FootOffset = 0.75;
        public const double HalfWidth = 0.85;
        public const double HalfHeight = 1;
        public const double TipReach = 1.4;
        public const double StepSeconds = 1.0 / 60;

        private const double FootProbe = 0.01;

        public double X;
        public double Y;
        public double VelocityX;
        public double VelocityY;
        public double Angle;
        public double AngularVelocity;

        public bool SupportedLeft { get; private set; }
        public bool SupportedRight { get; private set; }
        public bool Grounded => SupportedLeft || SupportedRight;

        public double DirectionX => Math.Sin(Angle * Math.PI / 180);
        public double DirectionY => Math.Cos(Angle * Math.PI / 180);
        public double TipX => X + DirectionX * TipReach;
        public double TipY => Y + DirectionY * TipReach;

        /// <summary>The row the feet stand on, which is the drill's depth.</summary>
        public int FeetRow => Round(Y + HalfHeight + 0.5);

        public void Place(double x, double y)
        {
            X = x;
            Y = y;
            VelocityX = VelocityY = Angle = AngularVelocity = 0;
        }

        /// <summary>Advances one fixed step. Returns the landing speed, or 0 if it did not land.</summary>
        public double Step(double dt, Func<int, int, bool> solid)
        {
            SupportedLeft = Foot(-1, solid);
            SupportedRight = Foot(1, solid);

            double targetAngle;
            if (SupportedLeft && SupportedRight)
            {
                targetAngle = 0;
                VelocityY = 0;
                VelocityX = Approach(VelocityX, 0, GroundFriction * dt);
            }
            else if (SupportedLeft || SupportedRight)
            {
                // one foot over a hole: the body tips and slides towards the unsupported side
                double side = SupportedLeft ? 1 : -1;
                targetAngle = side * MaxTilt;
                VelocityY = 0;
                VelocityX += side * SlideAcceleration * dt;
            }
            else
            {
                targetAngle = MaxTilt * Clamp(VelocityX / MaxSlideSpeed, -1, 1);
                VelocityY = Math.Min(VelocityY + Gravity * dt, MaxFallSpeed);
            }

            VelocityX = Clamp(VelocityX, -MaxSlideSpeed, MaxSlideSpeed);
            MoveSideways(dt, solid);
            double impact = MoveDown(dt, solid);

            AngularVelocity += (TiltSpring * (targetAngle - Angle) - TiltDamping * AngularVelocity) * dt;
            Angle += AngularVelocity * dt;
            return impact;
        }

        private bool Foot(int side, Func<int, int, bool> solid) =>
            solid(Round(Y + HalfHeight + FootProbe), Round(X + side * FootOffset));

        private void MoveSideways(double dt, Func<int, int, bool> solid)
        {
            if (VelocityX == 0) return;
            double next = X + VelocityX * dt;
            int col = Round(next + Math.Sign(VelocityX) * HalfWidth);
            if (solid(Round(Y), col) || solid(Round(Y + 0.6), col))
            {
                VelocityX = 0;
                return;
            }
            X = next;
        }

        private double MoveDown(double dt, Func<int, int, bool> solid)
        {
            if (VelocityY <= 0) return 0;
            double next = Y + VelocityY * dt;
            int row = Round(next + HalfHeight);
            bool hit = solid(row, Round(X - FootOffset)) || solid(row, Round(X + FootOffset));
            if (hit && next + HalfHeight > row - 0.5)
            {
                double impact = VelocityY;
                Y = row - 0.5 - HalfHeight;
                VelocityY = 0;
                return impact;
            }
            Y = next;
            return 0;
        }

        private static double Approach(double value, double target, double step)
        {
            if (value < target) return Math.Min(value + step, target);
            return Math.Max(value - step, target);
        }

        private static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));

        private static int Round(double v) => (int)Math.Floor(v + 0.5);
    }
}
