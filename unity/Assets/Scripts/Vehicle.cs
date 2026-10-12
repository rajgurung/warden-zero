using UnityEngine;
using J = WardenZero.GameConfig.Jeep;

namespace WardenZero
{
    // One tick's driver input.
    public struct VehicleInput
    {
        public float Throttle; // -1 (brake, then reverse once stopped) .. 1
        public float Steer; // -1 left .. 1 right
        public bool Handbrake;
    }

    // Where the jeep is, by distance from the nearest road's centreline (not the splat map, so
    // the tests are exact).
    public enum RoadSurface { Road, Shoulder, Forest, Edge }

    // The jeep's arcade handling, without a physics engine (M3 decision 0.1). Plain C#, like
    // Traversal: VehicleController ticks it and VehicleRig draws it.
    // - Ground: four wheel points sample Ground; the chassis sits at their mean, pitched front
    //   against rear and rolled left against right, each clamped to MaxTilt (it never flips).
    //   When the ground falls away faster than gravity it flies, and lands.
    // - Along: throttle eases off toward the surface's top speed; S brakes, then reverses once
    //   stopped; coasting drag; slopes pull back, and traction (a share of the grip) caps the
    //   drive, so steep banks can't be climbed.
    // - Turning: speed-sensitive wheel angle, a bicycle model's yaw rate reached through a short
    //   lag, capped at the surface's grip (understeer past it); sideways slip decays by grip.
    //   The handbrake frees the rear for a slide.
    // - Contact: three footprint circles against trunks, rocks and blocks (not its own); the
    //   push-out bounces the speed into the wall, keeps most of the rest, and turns the jeep by
    //   the contact's lever arm. A hidden corridor clamp (Clamp) keeps it near the road.
    // - Stuck, tilted or far off the road: RESET is offered, and the worst cases reset by
    //   themselves.
    // Movement is sub-stepped (at most MaxStepDistance and MaxStepTime), so nothing tunnels.
    public class Vehicle
    {
        public const float MaxStepDistance = 0.25f, MaxStepTime = 1f / 120;

        // The ground at a point (World.HeightAt in the game); flat at 0 when null.
        public System.Func<Vector3, float> Ground;
        // The road the surfaces are measured from (null: all road).
        public RoadPath Road;
        // The corridor: returns where a circle of this radius may be (null: anywhere).
        public System.Func<Vector3, float, Vector3> Clamp;

        public Vector3 Position { get; private set; } // chassis origin, on the ground between the axles
        public float Yaw { get; private set; } // degrees
        public Vector2 Velocity { get; private set; } // flat, world x and z
        public float YawRate { get; private set; } // rad/s, positive turning right
        public float VerticalSpeed { get; private set; }
        public float SteerInput { get; private set; } // -1..1, slewed
        public float SteerAngle { get; private set; } // degrees at the front wheels
        public bool Airborne { get; private set; }
        public RoadSurface Surface { get; private set; }
        public float RoadDistance { get; private set; } // metres from the centreline
        public float Pitch { get; private set; } // degrees, nose up
        public float Roll { get; private set; } // degrees, right side down
        public float WheelTurn { get; private set; } // radians the wheels have rolled

        // Events and readings of the last Tick.
        public float Impact { get; private set; } // speed into a wall (m/s), the strongest
        public float Landing { get; private set; } // vertical speed at touchdown (m/s)
        public bool ResetOffered { get; private set; }
        public bool AutoReset { get; private set; } // a stuck rule's timeout: reset now
        public float SlowTime { get; private set; } // seconds below 1 m/s

        float stuckTime, tiltTime, offTime;

        public Vector3 Forward => new Vector3(Mathf.Sin(Yaw * Mathf.Deg2Rad), 0, Mathf.Cos(Yaw * Mathf.Deg2Rad));
        public Vector3 Right => new Vector3(Mathf.Cos(Yaw * Mathf.Deg2Rad), 0, -Mathf.Sin(Yaw * Mathf.Deg2Rad));
        // Signed speed along the jeep (negative reversing).
        public float ForwardSpeed => Vector2.Dot(Velocity, Flat(Forward));
        public float Speed => Velocity.magnitude;
        public Quaternion Rotation => Quaternion.Euler(-Pitch, Yaw, -Roll);
        public bool Tilted => Mathf.Abs(Pitch) >= J.TiltLimit || Mathf.Abs(Roll) >= J.TiltLimit;

        // Put it down at rest (a spawn, a reset).
        public void Place(Vector3 position, float yaw)
        {
            Yaw = Mathf.Repeat(yaw, 360);
            Velocity = Vector2.zero;
            YawRate = VerticalSpeed = SteerInput = SteerAngle = 0;
            Airborne = false;
            stuckTime = tiltTime = offTime = SlowTime = 0;
            ResetOffered = AutoReset = false;
            Position = position;
            Settle();
            Position = new Vector3(position.x, GroundY(), position.z);
            Classify();
        }

        // Back onto the road's centre at the nearest point, facing along it (toward B), at rest.
        public void ResetToRoad()
        {
            if (Road == null)
            {
                Place(Position, Yaw);
                return;
            }
            var hit = Road.Nearest(Flat(Position));
            var at = Road.At(Mathf.Clamp(hit.S, 4, Road.Length - 4));
            Place(new Vector3(at.Position.x, 0, at.Position.y), Mathf.Atan2(at.Tangent.x, at.Tangent.y) * Mathf.Rad2Deg);
        }

        // Driven along a path by someone else (the gun ride): put it at (x, z) facing yaw at
        // `speed`, and let the ground pose, the wheels and the suspension follow.
        public void MoveAlong(Vector2 at, float yaw, float speed, float steerAngle, float dt)
        {
            float before = Yaw;
            Yaw = Mathf.Repeat(yaw, 360);
            YawRate = dt > 0 ? Mathf.DeltaAngle(before, Yaw) * Mathf.Deg2Rad / dt : 0;
            Velocity = Flat(Forward) * speed;
            SteerAngle = steerAngle;
            SteerInput = Mathf.Clamp(steerAngle / J.MaxSteer, -1, 1);
            Position = new Vector3(at.x, Position.y, at.y);
            WheelTurn += speed * dt / J.WheelRadius;
            Impact = Landing = 0;
            if (dt > 0) Follow(dt);
            Classify();
        }

        public void Tick(VehicleInput input, float dt, float steerSlew = J.SteerSlew)
        {
            Impact = Landing = 0;
            AutoReset = false;
            Classify();
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Speed * dt / MaxStepDistance, dt / MaxStepTime - 1e-4f)));
            float h = dt / steps;
            for (int i = 0; i < steps; i++) Step(input, h, steerSlew);
            Classify();
            UpdateStuck(input, dt);
        }

        void Step(VehicleInput input, float h, float steerSlew)
        {
            float throttle = Mathf.Clamp(input.Throttle, -1, 1);
            SteerInput = Mathf.MoveTowards(SteerInput, Mathf.Clamp(input.Steer, -1, 1), steerSlew * h);
            Vector3 f3 = Forward, r3 = Right;
            Vector2 f = Flat(f3), r = Flat(r3);
            float vf = Vector2.Dot(Velocity, f), vl = Vector2.Dot(Velocity, r);
            float speed = Mathf.Abs(vf);
            float mu = Grip(Surface);
            float g = J.Gravity;

            // Wheel angle: wide at walking pace, narrow at speed.
            float maxAngle = Mathf.Lerp(J.MaxSteer, J.MinSteer, Mathf.InverseLerp(J.FullLockBelow, J.TopSpeed, speed));
            SteerAngle = SteerInput * maxAngle;

            if (!Airborne)
            {
                // ---- along the jeep
                float cap = TopSpeed(Surface);
                float a = 0;
                if (throttle > 0.01f)
                {
                    if (vf < -0.3f) a = J.Brake * throttle;
                    else a = vf > cap ? -J.OverCapDecel : J.Accel * throttle * (1 - vf * vf / (cap * cap));
                }
                else if (throttle < -0.01f)
                {
                    if (vf > 0.3f) a = -J.Brake * -throttle;
                    else a = vf < -J.ReverseSpeed ? J.OverCapDecel : -J.ReverseAccel * -throttle * (1 - vf * vf / (J.ReverseSpeed * J.ReverseSpeed));
                }
                else
                {
                    // Coasting; at a crawl with no input it holds (the brakes).
                    float drag = speed < J.HoldBelow ? J.Brake : J.CoastDrag + J.CoastDragQuad * vf * vf;
                    a = -Mathf.Sign(vf) * Mathf.Min(drag, speed / h);
                }
                if (input.Handbrake && speed > 0.1f) a -= Mathf.Sign(vf) * Mathf.Min(J.HandbrakeDecel, speed / h);
                // Traction caps the drive (not the brakes): the steep banks can't be climbed.
                float traction = Traction(Surface) * g * Mathf.Cos(Pitch * Mathf.Deg2Rad);
                if (Mathf.Sign(a) == Mathf.Sign(vf) || Mathf.Abs(vf) < 0.3f) a = Mathf.Clamp(a, -traction, traction);
                float slope = -g * Mathf.Sin(Pitch * Mathf.Deg2Rad);
                bool holding = throttle == 0 && speed < J.HoldBelow && Mathf.Abs(Pitch) < J.HoldSlope;
                float before = vf;
                vf += (a + (holding ? 0 : slope)) * h;
                // Brakes and drag stop it; they never push it the other way.
                if (throttle >= 0 && before > 0 && vf < 0 && a <= 0 && slope >= -0.01f) vf = 0;
                if (throttle <= 0 && before < 0 && vf > 0 && a >= 0 && slope <= 0.01f) vf = 0;

                // ---- turning
                float want = vf * Mathf.Tan(SteerAngle * Mathf.Deg2Rad) / J.Wheelbase;
                if (input.Handbrake) want *= J.HandbrakeYaw;
                float limit = mu * g / Mathf.Max(Mathf.Abs(vf), 0.5f);
                want = Mathf.Clamp(want, -limit, limit);
                YawRate += (want - YawRate) * (1 - Mathf.Exp(-h / J.YawLag));

                // ---- sideways: slip decays by grip; side slopes past SideSlide slide downhill
                float grip = input.Handbrake ? J.HandbrakeGrip : LateralGrip(Surface);
                vl *= Mathf.Exp(-grip * h);
                float side = Mathf.Abs(Roll) - J.SideSlide;
                if (side > 0) vl += Mathf.Sign(Roll) * g * Mathf.Sin(side * Mathf.Deg2Rad) * h;
            }
            else
            {
                // In the air: no grip; the spin slows a little.
                YawRate *= Mathf.Exp(-0.5f * h);
            }

            Yaw = Mathf.Repeat(Yaw + YawRate * Mathf.Rad2Deg * h, 360);
            // The velocity stays put in the world as the jeep turns under it: split it again
            // along the new heading next step (that is the slip).
            Velocity = f * vf + r * vl;

            Vector3 p = Position + new Vector3(Velocity.x, 0, Velocity.y) * h;
            p = Collide(p);
            if (Clamp != null)
            {
                Vector3 c = Clamp(p, J.FootprintRadius);
                Vector3 d = c - p;
                d.y = 0;
                if (d.sqrMagnitude > 1e-8f) Respond(d, 0, J.ClampKeep);
                p.x = c.x;
                p.z = c.z;
            }
            Position = p;
            WheelTurn += vf * h / J.WheelRadius;
            Follow(h);
        }

        // ---- contact

        // The footprint's three circles against trunks, rocks and blocks (the jeep's own
        // excluded): each push moves the body out and is answered with a bounce.
        Vector3 Collide(Vector3 p)
        {
            Vector3 f = Forward;
            for (int c = -1; c <= 1; c++)
            {
                Vector3 centre = p + f * (c * J.FootprintSpacing);
                Vector3 pushed = World.PushOutOfTrunks(centre, J.FootprintRadius);
                pushed = PushOutOfRocks(pushed, J.FootprintRadius, p.y);
                pushed = World.PushOutOfBlocks(pushed, J.FootprintRadius, p.y, BlockFlags.Vehicle);
                Vector3 d = pushed - centre;
                d.y = 0;
                if (d.sqrMagnitude < 1e-8f) continue;
                p += d;
                Respond(d, c * J.FootprintSpacing, J.TangentKeep);
            }
            return p;
        }

        // A push d out of something: speed into it bounces back at Restitution, the rest keeps
        // `keep` of itself, and the contact's lever arm (metres ahead of the middle) turns the jeep.
        void Respond(Vector3 d, float lever, float keep)
        {
            Vector2 n = new Vector2(d.x, d.z).normalized;
            float vn = Vector2.Dot(Velocity, n);
            if (vn >= 0) return;
            Vector2 tangent = Velocity - n * vn;
            Velocity = tangent * keep - n * vn * J.Restitution;
            Impact = Mathf.Max(Impact, -vn);
            // A knock on the nose from the right turns it left, and so on: the yaw follows the
            // push's moment about the middle.
            Vector2 f = Flat(Forward);
            float moment = lever * (f.x * n.y - f.y * n.x);
            YawRate -= moment * -vn * J.ContactYaw;
        }

        static Vector3 PushOutOfRocks(Vector3 p, float r, float baseY)
        {
            foreach (var rock in World.Rocks)
            {
                // Only boulders standing well out of the ground are walls to the jeep.
                if (rock.y + rock.w * 0.6f < baseY + 0.6f) continue;
                float reach = r + rock.w * 0.8f;
                float dx = p.x - rock.x, dz = p.z - rock.z, d2 = dx * dx + dz * dz;
                if (d2 >= reach * reach) continue;
                float d = Mathf.Sqrt(d2);
                if (d < 1e-4f) { dx = 1; dz = 0; d = 1; }
                p.x = rock.x + dx / d * reach;
                p.z = rock.z + dz / d * reach;
            }
            return p;
        }

        // ---- ground

        // The wheels' ground: the chassis at their mean, pitch and roll from their differences.
        void Settle()
        {
            Vector3 f = Forward, r = Right, p = Position;
            float hl = J.Wheelbase / 2, ht = J.Track / 2;
            float fl = G(p + f * hl - r * ht), fr = G(p + f * hl + r * ht);
            float rl = G(p - f * hl - r * ht), rr = G(p - f * hl + r * ht);
            groundMean = (fl + fr + rl + rr) / 4;
            float max = J.MaxTilt;
            Pitch = Mathf.Clamp(Mathf.Atan2((fl + fr) / 2 - (rl + rr) / 2, J.Wheelbase) * Mathf.Rad2Deg, -max, max);
            Roll = Mathf.Clamp(Mathf.Atan2((fl + rl) / 2 - (fr + rr) / 2, J.Track) * Mathf.Rad2Deg, -max, max);
        }

        float groundMean;

        float GroundY() => groundMean;

        float G(Vector3 p) => Ground != null ? Ground(p) : 0;

        // Follow the ground, or fly when it drops away faster than gravity pulls.
        void Follow(float h)
        {
            float g = J.Gravity;
            float before = Position.y;
            var tilt = (Pitch, Roll);
            Settle();
            float target = groundMean;
            Vector3 p = Position;
            if (!Airborne)
            {
                float fall = VerticalSpeed - g * h;
                if ((target - before) / h < fall - 0.5f && Speed > 4)
                {
                    Airborne = true;
                    VerticalSpeed = fall;
                    p.y = before + VerticalSpeed * h;
                    (Pitch, Roll) = tilt; // it keeps its attitude in the air
                }
                else
                {
                    VerticalSpeed = (target - before) / h;
                    p.y = target;
                }
            }
            else
            {
                VerticalSpeed -= g * h;
                p.y = before + VerticalSpeed * h;
                if (p.y <= target)
                {
                    Landing = Mathf.Max(Landing, -VerticalSpeed);
                    Airborne = false;
                    p.y = target;
                    VerticalSpeed = 0;
                }
                else (Pitch, Roll) = tilt;
            }
            Position = p;
        }

        // ---- surfaces

        void Classify()
        {
            if (Road == null)
            {
                Surface = RoadSurface.Road;
                RoadDistance = 0;
                return;
            }
            RoadDistance = Road.Nearest(Flat(Position)).Distance;
            Surface = SurfaceAt(RoadDistance);
        }

        public static RoadSurface SurfaceAt(float distance)
        {
            return distance <= J.RoadHalf ? RoadSurface.Road : distance <= J.ShoulderHalf ? RoadSurface.Shoulder : distance <= J.ForestHalf ? RoadSurface.Forest : RoadSurface.Edge;
        }

        public static float TopSpeed(RoadSurface s)
        {
            return s == RoadSurface.Road ? J.TopSpeed : s == RoadSurface.Shoulder ? J.ShoulderSpeed : J.ForestSpeed;
        }

        // Turning grip (the lateral-acceleration cap is this times g).
        public static float Grip(RoadSurface s) => s == RoadSurface.Road ? 0.9f : s == RoadSurface.Shoulder ? 0.75f : s == RoadSurface.Forest ? 0.6f : 0.45f;

        // How fast sideways slip dies away (1/s).
        static float LateralGrip(RoadSurface s) => s == RoadSurface.Road ? 8 : s == RoadSurface.Edge ? 3 : 5;

        // The share of g the wheels can push with: tan of the steepest climb.
        static float Traction(RoadSurface s) => s == RoadSurface.Road || s == RoadSurface.Shoulder ? 0.9f : J.ClimbTraction;

        // ---- stuck, tilted, lost

        void UpdateStuck(VehicleInput input, float dt)
        {
            bool pushing = Mathf.Abs(input.Throttle) > 0.1f;
            stuckTime = pushing && Speed < 0.6f ? stuckTime + dt : 0;
            tiltTime = Tilted ? tiltTime + dt : 0;
            offTime = Road != null && RoadDistance > J.ForestHalf ? offTime + dt : 0;
            SlowTime = Speed < 1 ? SlowTime + dt : 0;
            ResetOffered = stuckTime >= J.StuckOffer || tiltTime >= J.TiltOffer || offTime >= J.OffRoadOffer;
            AutoReset = tiltTime >= J.TiltAuto || offTime >= J.OffRoadAuto;
            if (AutoReset) stuckTime = tiltTime = offTime = 0;
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
    }
}
