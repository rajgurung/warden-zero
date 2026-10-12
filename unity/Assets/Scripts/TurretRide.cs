using UnityEngine;

namespace WardenZero
{
    // The gun ride's progress along RoadLayout.Ride (plain C#): its speed profile
    // (RoadLayout.RideSpeed), the pause at the fuel dump, and the stop short of the roadblock.
    // The jeep follows it with Vehicle.MoveAlong (ground pose, wheels), steering by the
    // road's curvature.
    public class TurretRide
    {
        public float S { get; private set; }
        public float Speed { get; private set; }
        public float Time { get; private set; }
        public bool Paused => pauseLeft > 0;
        public bool AtEnd { get; private set; }
        public float SinceEnd { get; private set; }

        float pauseLeft;
        bool paused;

        public void Reset()
        {
            S = Speed = Time = SinceEnd = pauseLeft = 0;
            AtEnd = paused = false;
        }

        public void Tick(float dt)
        {
            Time += dt;
            if (AtEnd)
            {
                SinceEnd += dt;
                return;
            }
            if (!paused && S >= RoadLayout.FuelDumpPause)
            {
                paused = true;
                pauseLeft = RoadLayout.PauseTime;
            }
            if (pauseLeft > 0)
            {
                pauseLeft -= dt;
                Speed = 0;
                return;
            }
            Speed = RoadLayout.RideSpeed(S);
            S += Speed * dt;
            if (S >= RoadLayout.RideEnd)
            {
                S = RoadLayout.RideEnd;
                Speed = 0;
                AtEnd = true;
            }
        }

        public RoadPath.Sample Here => RoadLayout.Ride.At(S);

        public float Heading
        {
            get
            {
                var t = Here.Tangent;
                return Mathf.Atan2(t.x, t.y) * Mathf.Rad2Deg;
            }
        }

        // The front wheels' angle for the road's bend here (the road's curvature is positive
        // to the left; the jeep's steering positive to the right).
        public float SteerAngle => -Mathf.Atan(Here.Curvature * GameConfig.Jeep.Wheelbase) * Mathf.Rad2Deg;
    }
}
