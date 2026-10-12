using UnityEngine;
using J = WardenZero.GameConfig.Jeep;

namespace WardenZero
{
    // A pure-pursuit driver for a road: steers at a point LookAhead + LookAheadPerSpeed x v metres
    // down the road and brakes for the bends ahead. The tests drive the jeep with it, and the
    // touch DRIVE ASSIST uses its speed rule.
    public static class DriveBot
    {
        public const float LookAhead = 6, LookAheadPerSpeed = 0.5f;
        public const float BendMargin = 0.85f; // of the grip the bends are taken at
        public const float BrakeHorizon = 50; // metres ahead checked for bends

        public static VehicleInput Input(Vehicle v, RoadPath road, float topSpeed = J.TopSpeed)
        {
            var here = new Vector2(v.Position.x, v.Position.z);
            var hit = road.Nearest(here);
            float speed = Mathf.Max(0, v.ForwardSpeed);
            float ahead = LookAhead + LookAheadPerSpeed * speed;
            var target = road.At(Mathf.Min(hit.S + ahead, road.Length)).Position;
            // Pure pursuit: the arc through the target point.
            var to = target - here;
            var right = new Vector2(v.Right.x, v.Right.z);
            float lateral = Vector2.Dot(to, right);
            float dist2 = Mathf.Max(to.sqrMagnitude, 1);
            float curvature = 2 * lateral / dist2;
            float angle = Mathf.Atan(curvature * J.Wheelbase) * Mathf.Rad2Deg;
            float maxAngle = Mathf.Lerp(J.MaxSteer, J.MinSteer, Mathf.InverseLerp(J.FullLockBelow, J.TopSpeed, speed));
            float want = Mathf.Min(topSpeed, SafeSpeed(road, hit.S, speed));
            float throttle = Mathf.Clamp((want - speed) * 0.6f, -1, 1);
            if (hit.S >= road.Length - 1 && speed < 0.5f) throttle = 0;
            return new VehicleInput { Throttle = throttle, Steer = Mathf.Clamp(angle / maxAngle, -1, 1) };
        }

        // The fastest speed from which every bend in the next BrakeHorizon metres can still be
        // taken at BendMargin of the road's grip, braking at most half the brakes.
        public static float SafeSpeed(RoadPath road, float s, float speed)
        {
            float best = J.TopSpeed;
            float grip = Vehicle.Grip(RoadSurface.Road) * BendMargin * J.Gravity;
            for (float d = 0; d <= BrakeHorizon; d += 4)
            {
                float k = Mathf.Abs(road.At(s + d).Curvature);
                if (k < 1e-4f) continue;
                float bend = Mathf.Sqrt(grip / k);
                // v^2 = bend^2 + 2 a d: what can be shed before reaching it.
                best = Mathf.Min(best, Mathf.Sqrt(bend * bend + 2 * (J.Brake * 0.5f) * d));
            }
            // And stop at the road's end.
            float left = road.Length - s;
            best = Mathf.Min(best, Mathf.Sqrt(2 * J.Brake * 0.5f * Mathf.Max(0, left - 4)));
            return best;
        }
    }
}
