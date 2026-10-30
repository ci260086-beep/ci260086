using System;

namespace DeepDrill.Core
{
    /// <summary>
    /// Hand-written physics for one small drill, in tile units. X grows right, Y grows down, block
    /// (row, col) is the unit square centred on (col, row). Angle is degrees: 0 points straight down,
    /// negative tips the nose left, positive right. Solid callbacks answer (row, col).
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
        public const double HalfWidth = 0.25;
        public const double HalfHeight = 0.375;
        public const double TipReach = 0.5;
        public const double CentreRate = 6;
        public const double StepSeconds = 1.0 / 30;

        private const double FootProbe = 0.01;

        public double X;
        public double Y;
        public double VelocityX;
        public double VelocityY;
        public double Angle;
        public double AngularVelocity;

        public bool Grounded { get; private set; }

        public double DirectionX => Math.Sin(Angle * Math.PI / 180);
        public double DirectionY => Math.Cos(Angle * Math.PI / 180);
        public double TipX => X + DirectionX * TipReach;
        public double TipY => Y + DirectionY * TipReach;

        /// <summary>The row under the drill, which is its depth.</summary>
        public int FeetRow => Round(Y + HalfHeight + 0.5);
        public int Column => Round(X);
        public int BodyRow => Round(Y);

        public void Place(double x, double y)
        {
            X = x;
            Y = y;
            VelocityX = VelocityY = Angle = AngularVelocity = 0;
        }

        /// <summary>Advances one fixed step. Returns the landing speed, or 0 if it did not land.</summary>
        public double Step(double dt, Func<int, int, bool> solid)
        {
            int row = Round(Y + HalfHeight + FootProbe);
            int col = Round(X);
            Grounded = solid(row, col);

            double targetAngle = 0;
            if (Grounded)
            {
                bool left = solid(row, col - 1);
                bool right = solid(row, col + 1);
                VelocityY = 0;
                if (left == right)
                {
                    VelocityX = Approach(VelocityX, 0, GroundFriction * dt);
                }
                else
                {
                    // standing beside a hole: the body tips and slides towards it
                    double side = left ? 1 : -1;
                    targetAngle = side * MaxTilt;
                    VelocityX += side * SlideAcceleration * dt;
                }
            }
            else
            {
                // falling: settle into the middle of the column so the drill fits the hole below
                VelocityY = Math.Min(VelocityY + Gravity * dt, MaxFallSpeed);
                VelocityX = (col - X) * CentreRate;
                targetAngle = MaxTilt * Clamp(VelocityX / MaxSlideSpeed, -1, 1);
            }

            VelocityX = Clamp(VelocityX, -MaxSlideSpeed, MaxSlideSpeed);
            MoveSideways(dt, solid);
            double impact = MoveDown(dt, solid);

            AngularVelocity += (TiltSpring * (targetAngle - Angle) - TiltDamping * AngularVelocity) * dt;
            Angle += AngularVelocity * dt;
            return impact;
        }

        private void MoveSideways(double dt, Func<int, int, bool> solid)
        {
            if (VelocityX == 0) return;
            double next = X + VelocityX * dt;
            int col = Round(next + Math.Sign(VelocityX) * HalfWidth);
            if (col != Round(X) && solid(Round(Y), col))
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
            if (solid(row, Round(X)) && next + HalfHeight > row - 0.5)
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
