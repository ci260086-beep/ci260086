using System;

namespace DeepDrill.Core
{
    /// <summary>
    /// Physics for one small drill against pixel ground, in tile units. X grows right, Y grows down.
    /// Angle is degrees: 0 points straight down, negative tips the nose left, positive right. Foot
    /// and side probes sample the ground, so slopes tip and slide the body and pits catch it.
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
        public const double FootSpread = 0.2;
        public const double TipReach = 0.5;
        public const double StepSeconds = 1.0 / 30;

        private const double Pixel = 1.0 / 16;
        private const double FootProbe = 0.02;
        private const int MaxStepUpPixels = 3;

        public double X;
        public double Y;
        public double VelocityX;
        public double VelocityY;
        public double Angle;
        public double AngularVelocity;

        /// <summary>Set by the sim while crowded by another drill: -1 drift left, 1 drift right.</summary>
        public int Nudge;

        /// <summary>The current planet's pull, as a share of the home planet's.</summary>
        public double GravityScale = 1;

        public bool Grounded { get; private set; }

        /// <summary>True when the last step tried to slide and rock stopped the body.</summary>
        public bool Blocked { get; private set; }

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
        public double Step(double dt, Func<double, double, bool> solid)
        {
            Blocked = false;
            StepOutOfGround(solid);

            double feet = Y + HalfHeight + FootProbe;
            bool left = solid(X - FootSpread, feet);
            bool centre = solid(X, feet);
            bool right = solid(X + FootSpread, feet);
            Grounded = left || centre || right;

            double targetAngle = 0;
            if (Grounded)
            {
                VelocityY = 0;
                int side = Nudge != 0 ? Nudge : (left == right ? 0 : (left ? 1 : -1));
                if (side == 0)
                {
                    VelocityX = Approach(VelocityX, 0, GroundFriction * dt);
                }
                else
                {
                    // only one foot on the ground, or crowded: tip towards the open side and slide
                    targetAngle = side * MaxTilt;
                    VelocityX += side * SlideAcceleration * dt;
                }
            }
            else
            {
                VelocityY = Math.Min(VelocityY + Gravity * GravityScale * dt, MaxFallSpeed * GravityScale);
                VelocityX = Approach(VelocityX, 0, GroundFriction * 0.25 * dt);
                targetAngle = MaxTilt * Clamp(VelocityX / MaxSlideSpeed, -1, 1);
            }

            VelocityX = Clamp(VelocityX, -MaxSlideSpeed, MaxSlideSpeed);
            MoveSideways(dt, solid);
            double impact = MoveDown(dt, solid);

            AngularVelocity += (TiltSpring * (targetAngle - Angle) - TiltDamping * AngularVelocity) * dt;
            Angle += AngularVelocity * dt;
            return impact;
        }

        // Sand can bury a drill; it climbs out a few pixels at a time rather than sticking in place.
        private void StepOutOfGround(Func<double, double, bool> solid)
        {
            for (int i = 0; i < MaxStepUpPixels && solid(X, Y + HalfHeight - Pixel); i++) Y -= Pixel;
        }

        private void MoveSideways(double dt, Func<double, double, bool> solid)
        {
            if (VelocityX == 0) return;
            double next = X + VelocityX * dt;
            double edge = next + Math.Sign(VelocityX) * HalfWidth;
            if (solid(edge, Y) || solid(edge, Y - HalfHeight * 0.6))
            {
                VelocityX = 0;
                Blocked = true;
                return;
            }
            X = next;
        }

        private double MoveDown(double dt, Func<double, double, bool> solid)
        {
            if (VelocityY <= 0) return 0;
            double left = VelocityY * dt;
            while (left > 0)
            {
                double step = Math.Min(left, Pixel);
                double feet = Y + step + HalfHeight;
                if (solid(X, feet) || solid(X - FootSpread, feet) || solid(X + FootSpread, feet))
                {
                    // close the last sliver, or the body hovers: too far up to feel the ground, too close to step
                    for (double s = step / 2; s >= FootProbe / 2; s /= 2)
                        if (!solid(X, Y + s + HalfHeight) && !solid(X - FootSpread, Y + s + HalfHeight) && !solid(X + FootSpread, Y + s + HalfHeight))
                            Y += s;
                    double impact = VelocityY;
                    VelocityY = 0;
                    return impact;
                }
                Y += step;
                left -= step;
            }
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
