using UnityEngine;

namespace WardenZero
{
    // The jump's flight model, kept free of scene objects so it can be tested on its own.
    // Freefall toward terminal speed, then a canopy that takes time to open: pulled high it
    // slows him to a gentle sink, pulled low he hits the ground fast and gets hurt.
    // A/D turn (freefall and canopy alike), W dives / tracks faster, S brakes.
    // An automatic opener fires at AutoDeployAltitude, so a missed pull is a hard landing,
    // never a death. A soft funnel keeps him over the jungle patch whatever he steers.
    public class Skydive
    {
        public enum State { Freefall, Opening, Canopy, Landed }

        public const float Gravity = 9.81f;
        public const float Terminal = 55; // belly-to-earth, m/s
        public const float TrackSpeed = 26; // horizontal, W held in freefall
        public const float DriftSpeed = 6; // horizontal, hands off in freefall
        public const float FreefallTurn = 70; // degrees per second
        public const float DeployPrompt = 300; // the HUD asks for the pull below this
        public const float SafeDeploy = 120; // pulled at or above this: a soft landing
        public const float AutoDeployAltitude = 100;
        public const float SnatchDelay = 0.6f; // pull to the canopy starting to bite
        public const float InflateTime = 1.8f;
        public const float CanopyGrip = 2.2f; // how fast a full canopy pulls the speed in, 1/s
        public const float SinkRate = 9.5f;
        public const float CanopySpeed = 10;
        public const float CanopyTurn = 55;
        public const float FlareHeight = 12; // flare works below this
        public const float FlareTime = 3.5f; // then the wing stalls: flare too high and he drops
        public const float SafeTouchdown = 13; // m/s (sink plus a quarter of the ground speed); faster hurts
        public const float FunnelBase = 45; // the funnel's radius at ground level, metres
        public const float FunnelSlope = 0.9f; // extra radius per metre of altitude

        public State Current { get; private set; } = State.Freefall;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Heading; // degrees, 0 = +Z
        public Vector3 Target; // the landing zone (the funnel's centre)
        public System.Func<Vector3, float> Ground = _ => 0;

        public float DeployAltitude { get; private set; } = -1;
        public bool AutoDeployed { get; private set; }
        public float TouchdownSpeed { get; private set; }
        public bool Flared { get; private set; }
        public float SinceDeploy { get; private set; }
        float flareLeft = -1; // < 0: not flared yet; 0: flare spent (stalled)

        public float Altitude => Position.y - Ground(Position);
        public Vector3 Forward => Quaternion.Euler(0, Heading, 0) * Vector3.forward;

        // Hard-landing damage for a touchdown speed (m/s).
        public static float LandingDamage(float speed) => Mathf.Max(0, (speed - SafeTouchdown) * 2.2f);

        public void Deploy(bool automatic = false)
        {
            if (Current != State.Freefall) return;
            Current = State.Opening;
            DeployAltitude = Altitude;
            AutoDeployed = automatic;
            SinceDeploy = 0;
        }

        public void Flare()
        {
            if (Current != State.Canopy || Altitude > FlareHeight || Flared) return;
            flareLeft = FlareTime;
            Flared = true;
        }

        // steer: x = turn (A/D), y = W (+1) / S (-1).
        public void Step(Vector2 steer, float dt)
        {
            if (Current == State.Landed || dt <= 0) return;
            steer = Vector2.ClampMagnitude(steer, 1);
            if (Current == State.Freefall)
            {
                Heading += steer.x * FreefallTurn * dt;
                float vy = -Velocity.y;
                vy += Gravity * (1 - vy * Mathf.Abs(vy) / (Terminal * Terminal)) * dt;
                float speed = steer.y >= 0 ? Mathf.Lerp(DriftSpeed, TrackSpeed, steer.y) : DriftSpeed * (1 + steer.y);
                Vector3 h = Approach(Flat(Velocity), Forward * speed, 1.2f, dt);
                Velocity = new Vector3(h.x, -vy, h.z);
                if (Altitude <= AutoDeployAltitude) Deploy(true);
            }
            else
            {
                SinceDeploy += dt;
                float grip = CanopyGrip;
                if (Current == State.Opening)
                {
                    float t = SinceDeploy - SnatchDelay;
                    grip = t <= 0 ? 0 : CanopyGrip * Mathf.Pow(Mathf.Clamp01(t / InflateTime), 1.5f);
                    if (t >= InflateTime) Current = State.Canopy;
                }
                else
                {
                    Heading += steer.x * CanopyTurn * dt;
                }
                float sink = SinkRate + Mathf.Max(0, steer.y) * 6 - Mathf.Max(0, -steer.y) * 3.5f;
                float forward = CanopySpeed + steer.y * 4;
                if (flareLeft > 0)
                {
                    flareLeft = Mathf.Max(0, flareLeft - dt);
                    sink = 1.5f;
                    forward = 3;
                    grip = 5;
                }
                else if (Flared)
                {
                    sink = 11; // stalled
                    forward = 2;
                }
                float vy = -Velocity.y;
                vy += (sink - vy) * (1 - Mathf.Exp(-grip * dt));
                Vector3 h = Approach(Flat(Velocity), Forward * forward, grip, dt);
                Velocity = new Vector3(h.x, -vy, h.z);
            }

            Position += Velocity * dt;
            // The funnel narrows toward the ground so he always lands in the patch.
            Vector3 off = Flat(Position - Target);
            float limit = FunnelBase + Mathf.Max(0, Altitude) * FunnelSlope;
            if (off.magnitude > limit)
            {
                Vector3 p = Target + off.normalized * limit;
                Position = new Vector3(p.x, Position.y, p.z);
            }

            if (Altitude <= 0)
            {
                TouchdownSpeed = -Velocity.y + Flat(Velocity).magnitude * 0.25f;
                Position.y = Ground(Position);
                Current = State.Landed;
            }
        }

        static Vector3 Approach(Vector3 from, Vector3 to, float rate, float dt) =>
            from + (to - from) * (1 - Mathf.Exp(-rate * dt));

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
