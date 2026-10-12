using NUnit.Framework;
using UnityEngine;
using J = WardenZero.GameConfig.Jeep;

namespace WardenZero.Tests
{
    // The jeep's handling (Vehicle, plain C#) on synthetic ground: the numbers in GameConfig.Jeep
    // and each rule of M3 section 3.
    public class VehicleTests
    {
        const float Dt = 1 / 60f;

        [SetUp]
        public void Open() => World.Use(1000, 1000, new WallRect[0], new Vector3[0]);

        [TearDown]
        public void Reset() => World.UseArena();

        static Vehicle Jeep(System.Func<Vector3, float> ground = null, RoadPath road = null, float yaw = 0, Vector3 at = default)
        {
            var v = new Vehicle { Ground = ground, Road = road };
            v.Place(at, yaw);
            return v;
        }

        static void Run(Vehicle v, VehicleInput input, float seconds, float dt = Dt)
        {
            for (float t = 0; t < seconds - 1e-4f; t += dt) v.Tick(input, dt);
        }

        static readonly VehicleInput Gas = new VehicleInput { Throttle = 1 };

        // A straight road far away along x = 5000: everything near the origin is off-road (Edge).
        static readonly RoadPath FarRoad = new RoadPath(new[] { new Vector2(5000, -100), new Vector2(5000, 100) });

        // A straight road through the origin along +z.
        static readonly RoadPath Straight = new RoadPath(new[] { new Vector2(0, -500), new Vector2(0, 0), new Vector2(0, 500) });

        [Test]
        public void ZeroTo60_InAbout4s_TopSpeed22()
        {
            var v = Jeep();
            float t = 0;
            while (v.Speed < 60 / 3.6f && t < 10)
            {
                v.Tick(Gas, Dt);
                t += Dt;
            }
            Assert.That(t, Is.InRange(3.6f, 4.4f), "0-60 km/h");
            Run(v, Gas, 12);
            Assert.That(v.Speed, Is.InRange(21.5f, 22.01f), "top speed on the road");
        }

        [Test]
        public void OffRoad_SpeedCaps()
        {
            var v = Jeep(road: FarRoad);
            Assert.AreEqual(RoadSurface.Edge, v.Surface);
            Run(v, Gas, 15);
            Assert.LessOrEqual(v.Speed, J.ForestSpeed + 0.05f, "forest floor and beyond");
            // On the shoulder: 5 m off the centreline, along it.
            var s = Jeep(road: Straight, at: new Vector3(5, 0, -400));
            Assert.AreEqual(RoadSurface.Shoulder, s.Surface);
            Run(s, Gas, 15);
            Assert.That(s.Speed, Is.InRange(J.ShoulderSpeed - 0.6f, J.ShoulderSpeed + 0.05f));
        }

        [Test]
        public void Braking_From20_StopsIn15To19m_ThenReverses()
        {
            var v = Jeep();
            while (v.Speed < 20) v.Tick(Gas, Dt);
            float from = v.Position.z;
            var brake = new VehicleInput { Throttle = -1 };
            float min = float.MaxValue;
            bool stopped = false;
            for (float t = 0; t < 6; t += Dt)
            {
                v.Tick(brake, Dt);
                if (!stopped && v.ForwardSpeed <= 0.01f)
                {
                    stopped = true;
                    Assert.That(v.Position.z - from, Is.InRange(15, 19), "stopping distance");
                }
                min = Mathf.Min(min, v.ForwardSpeed);
            }
            Assert.IsTrue(stopped);
            Assert.Less(min, -5, "S held after the stop reverses");
            Assert.GreaterOrEqual(min, -J.ReverseSpeed - 0.05f, "up to the reverse speed");
        }

        [Test]
        public void Brake_NeverReversesBeforeAStop()
        {
            var v = Jeep();
            Run(v, Gas, 3);
            var brake = new VehicleInput { Throttle = -1 };
            float last = v.ForwardSpeed;
            while (v.ForwardSpeed > 0.3f)
            {
                v.Tick(brake, Dt);
                Assert.LessOrEqual(v.ForwardSpeed, last + 1e-4f, "only slows");
                last = v.ForwardSpeed;
            }
        }

        [Test]
        public void Turning_TightAtWalkingPace_GripCappedAtSpeed()
        {
            var v = Jeep();
            // Walking pace, full lock: a radius of about 4-6 m (the 2.65 m Warden's jeep).
            while (v.Speed < 4) v.Tick(new VehicleInput { Throttle = 1 }, Dt);
            var crawl = new VehicleInput { Throttle = 0.25f, Steer = 1 };
            Run(v, crawl, 2);
            float radius = v.Speed / Mathf.Abs(v.YawRate);
            Assert.That(radius, Is.InRange(4, 6.5f), "turning radius at walking pace");
            // At 20 m/s, full lock: the sideways acceleration stays under the road's grip.
            var fast = Jeep();
            while (fast.Speed < 20) fast.Tick(Gas, Dt);
            for (float t = 0; t < 2; t += Dt)
            {
                fast.Tick(new VehicleInput { Throttle = 1, Steer = 1 }, Dt);
                Assert.LessOrEqual(fast.Speed * Mathf.Abs(fast.YawRate), Vehicle.Grip(RoadSurface.Road) * J.Gravity + 0.05f);
            }
        }

        static float Slip(Vehicle v)
        {
            var f = new Vector2(v.Forward.x, v.Forward.z);
            return v.Speed < 0.5f ? 0 : Vector2.Angle(f, v.Velocity);
        }

        [Test]
        public void Handbrake_SlidesTheRear()
        {
            float worst(bool handbrake)
            {
                var v = Jeep();
                while (v.Speed < 12) v.Tick(Gas, Dt);
                float most = 0;
                for (float t = 0; t < 1; t += Dt)
                {
                    v.Tick(new VehicleInput { Steer = 1, Handbrake = handbrake }, Dt);
                    most = Mathf.Max(most, Slip(v));
                }
                return most;
            }
            Assert.Less(worst(false), 8, "grip without it");
            Assert.Greater(worst(true), 12, "a slide with it");
        }

        [Test]
        public void SteepBanks_CantBeClimbed_GentleOnesCan()
        {
            float Climb(float degrees)
            {
                float k = Mathf.Tan(degrees * Mathf.Deg2Rad);
                var v = Jeep(p => Mathf.Max(0, p.z) * k, FarRoad, at: new Vector3(0, 0, 2));
                Run(v, Gas, 10);
                return v.Position.z - 2;
            }
            Assert.Less(Climb(27), 1, "27 degrees: no headway");
            Assert.Less(Climb(32), 1, "32 degrees");
            Assert.Greater(Climb(18), 10, "18 degrees climbs");
        }

        [Test]
        public void SideSlopes_Over22_SlideDownhill()
        {
            float Drift(float degrees)
            {
                float k = Mathf.Tan(degrees * Mathf.Deg2Rad);
                var v = Jeep(p => -p.x * k, FarRoad); // falls away to the right (+x)
                Run(v, new VehicleInput(), 3);
                return v.Position.x;
            }
            Assert.Greater(Drift(26), 0.3f, "26 degrees across: slides downhill");
            Assert.Less(Mathf.Abs(Drift(15)), 0.05f, "15 degrees across: stays");
        }

        [Test]
        public void Trunk_Bounces_LosesSpeed_TurnsIt()
        {
            World.Use(1000, 1000, new WallRect[0], new[] { new Vector3(0.9f, 0.5f, 40) });
            var v = Jeep();
            while (v.Speed < 10) v.Tick(Gas, Dt);
            float before = v.Speed;
            float impact = 0;
            for (float t = 0; t < 4; t += Dt)
            {
                v.Tick(new VehicleInput(), Dt);
                impact = Mathf.Max(impact, v.Impact);
                var c = new Vector2(v.Position.x, v.Position.z);
                for (int k = -1; k <= 1; k++)
                {
                    var circle = c + new Vector2(v.Forward.x, v.Forward.z) * (k * J.FootprintSpacing);
                    Assert.GreaterOrEqual(Vector2.Distance(circle, new Vector2(0.9f, 40)), J.FootprintRadius + 0.5f - 0.01f, "never inside the trunk");
                }
            }
            Assert.Greater(impact, 1, "a knock");
            Assert.Less(v.Speed, before * 0.9f, "speed lost");
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0, v.Yaw)), 2, "turned by the hit");
        }

        static Block Wall(float z, float depth = 0.2f, float top = 3) => new Block
        {
            Center = new Vector2(0, z), Half = new Vector2(8, depth / 2), Bottom = -1, Top = top,
        };

        [Test]
        public void Block_PushesItOut()
        {
            World.Blocks = new[] { Wall(1, 2) };
            var v = Jeep();
            v.Tick(new VehicleInput(), Dt);
            // The whole footprint sits clear of the box (its near face at z = 0).
            Assert.LessOrEqual(v.Position.z + J.FootprintSpacing + J.FootprintRadius, 0.01f, "out of the block");
        }

        [Test]
        public void NoTunnelling_ThroughAThinWall_AtDt01()
        {
            World.Blocks = new[] { Wall(20) };
            // Up to speed far from the wall, then big steps toward it.
            var w = Jeep(at: new Vector3(0, 0, -300));
            while (w.Speed < 21) w.Tick(Gas, Dt);
            for (int i = 0; i < 60; i++)
            {
                w.Tick(Gas, 0.1f);
                Assert.Less(w.Position.z + J.FootprintSpacing, 20, "stopped at the wall");
            }
        }

        [Test]
        public void Crest_AtSpeed_FliesAndLands()
        {
            // Up at 15%, then a sharp drop away at 40%.
            float Ground(Vector3 p) => p.z < 40 ? Mathf.Max(0, p.z) * 0.15f : 6 - (p.z - 40) * 0.4f;
            var v = Jeep(p => Mathf.Max(Ground(p), -20), at: new Vector3(0, 0, -70));
            bool flew = false, landed = false;
            for (float t = 0; t < 12; t += Dt)
            {
                v.Tick(Gas, Dt);
                flew |= v.Airborne;
                if (flew && v.Landing > 0) landed = true;
                Assert.GreaterOrEqual(v.Position.y, Ground(v.Position) - 0.05f, "never under the ground");
            }
            Assert.IsTrue(flew, "airborne over the crest");
            Assert.IsTrue(landed, "and lands");
            Assert.IsFalse(v.Airborne);
        }

        [Test]
        public void Tilt_NeverPastTheClamp()
        {
            var v = Jeep(p => 4 * Mathf.Sin(p.x * 0.7f) * Mathf.Cos(p.z * 0.5f) + p.x * 0.6f);
            for (float t = 0; t < 20; t += Dt)
            {
                v.Tick(new VehicleInput { Throttle = 1, Steer = Mathf.Sin(t) }, Dt);
                Assert.LessOrEqual(Mathf.Abs(v.Pitch), J.MaxTilt + 1e-3f);
                Assert.LessOrEqual(Mathf.Abs(v.Roll), J.MaxTilt + 1e-3f);
            }
        }

        [Test]
        public void CorridorClamp_Holds()
        {
            var v = Jeep();
            v.Clamp = (p, r) =>
            {
                var flat = new Vector2(p.x, p.z);
                return flat.magnitude <= 10 - r ? p : new Vector3(0, p.y, 0) + new Vector3(flat.normalized.x, 0, flat.normalized.y) * (10 - r);
            };
            for (float t = 0; t < 20; t += Dt)
            {
                v.Tick(new VehicleInput { Throttle = 1, Steer = 0.2f * Mathf.Sin(t * 0.7f) }, Dt);
                Assert.LessOrEqual(new Vector2(v.Position.x, v.Position.z).magnitude, 10 - J.FootprintRadius + 0.01f);
            }
        }

        [Test]
        public void Stuck_PushingAWall_OffersReset()
        {
            World.Blocks = new[] { Wall(4, 2) };
            var v = Jeep();
            float offered = -1;
            for (float t = 0; t < 4; t += Dt)
            {
                v.Tick(Gas, Dt);
                if (offered < 0 && v.ResetOffered) offered = t + Dt;
                Assert.IsFalse(v.AutoReset, "no auto-reset: he can get out");
            }
            Assert.That(offered, Is.InRange(2.5f, 3.6f), "offered after 2.5 s of pushing");
        }

        [Test]
        public void Stuck_Tilted_OffersThenResets()
        {
            float k = Mathf.Tan(35 * Mathf.Deg2Rad);
            var v = Jeep(p => -p.x * k, FarRoad);
            v.Clamp = (p, r) => new Vector3(Mathf.Clamp(p.x, -1, 1), p.y, p.z); // wedged
            float offered = -1, reset = -1;
            for (float t = 0; t < 6; t += Dt)
            {
                v.Tick(new VehicleInput(), Dt);
                if (offered < 0 && v.ResetOffered) offered = t + Dt;
                if (reset < 0 && v.AutoReset) reset = t + Dt;
            }
            Assert.That(offered, Is.InRange(1, 1.2f), "tilted: offered after 1 s");
            Assert.That(reset, Is.InRange(4, 4.2f), "and reset after 4 s");
        }

        [Test]
        public void Stuck_FarOffTheRoad_OffersThenResets_OntoTheRoad()
        {
            var v = Jeep(road: Straight, at: new Vector3(16, 0, 0));
            float offered = -1, reset = -1;
            for (float t = 0; t < 11; t += Dt)
            {
                v.Tick(new VehicleInput(), Dt);
                if (offered < 0 && v.ResetOffered) offered = t + Dt;
                if (reset < 0 && v.AutoReset) reset = t + Dt;
            }
            Assert.That(offered, Is.InRange(4, 4.2f));
            Assert.That(reset, Is.InRange(10, 10.2f));
            v.ResetToRoad();
            Assert.AreEqual(0, v.Position.x, 0.05f, "on the centreline");
            Assert.AreEqual(0, v.Position.z, 0.5f, "at the nearest point");
            Assert.AreEqual(0, Mathf.DeltaAngle(0, v.Yaw), 1, "facing along the road");
            Assert.AreEqual(0, v.Speed);
        }

        [Test]
        public void SameInputs_SameResult()
        {
            Vehicle Drive()
            {
                World.Use(1000, 1000, new WallRect[0], new[] { new Vector3(3, 0.6f, 40), new Vector3(-4, 0.4f, 70) });
                var v = Jeep(p => Mathf.Sin(p.x * 0.3f) * Mathf.Cos(p.z * 0.2f));
                for (int i = 0; i < 600; i++) v.Tick(new VehicleInput { Throttle = 1, Steer = Mathf.Sin(i * 0.02f), Handbrake = i % 97 < 10 }, Dt);
                return v;
            }
            var a = Drive();
            var b = Drive();
            Assert.AreEqual(a.Position, b.Position);
            Assert.AreEqual(a.Yaw, b.Yaw);
        }
    }
}
