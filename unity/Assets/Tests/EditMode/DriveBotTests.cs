using NUnit.Framework;
using UnityEngine;
using J = WardenZero.GameConfig.Jeep;

namespace WardenZero.Tests
{
    // The jeep on the real road (RoadLayout.Drive over JungleLayout's ground), driven by
    // DriveBot: the route drives well, and careless or reckless drivers never leave the corridor
    // or stay stuck.
    public class DriveBotTests
    {
        static readonly RoadPath Drive = RoadLayout.Drive;

        [SetUp]
        public void Jungle()
        {
            World.Use(JungleStage.PlayHalfSize, JungleStage.PlayHalfSize, new WallRect[0], new Vector3[0]);
            World.Ground = p => JungleLayout.Height(p.x, p.z);
            World.Blocks = CourseLayout.Blocks;
        }

        [TearDown]
        public void Reset() => World.UseArena();

        static Vehicle Jeep()
        {
            var v = new Vehicle
            {
                Ground = World.HeightAt,
                Road = Drive,
                Clamp = (p, r) => RoadLayout.Clamp(p, r, RoadLayout.CorridorHalf, false),
            };
            var park = RoadLayout.ParkingA;
            v.Place(park.position, park.rotation.eulerAngles.y);
            return v;
        }

        static bool InBounds(Vehicle v) => RoadLayout.Clamp(v.Position, J.FootprintRadius, RoadLayout.CorridorHalf + 0.05f, false) == v.Position;

        [Test]
        public void Bot_MotorPoolToB_Under55s_OnTheRoad()
        {
            var v = Jeep();
            var b = new Vector2(RoadLayout.CheckpointB.x, RoadLayout.CheckpointB.z);
            const float dt = 1 / 60f;
            float t = 0, worst = 0, top = 0;
            while (t < 70 && Vector2.Distance(new Vector2(v.Position.x, v.Position.z), b) > RoadLayout.CaptureRadiusB - 1)
            {
                v.Tick(DriveBot.Input(v, Drive), dt);
                Assert.IsFalse(v.AutoReset, $"no reset (at {v.Position}, t = {t:0.0})");
                worst = Mathf.Max(worst, v.RoadDistance);
                top = Mathf.Max(top, v.Speed);
                t += dt;
            }
            Debug.Log($"[DriveBot] motor pool to B: {t:0.0} s, top {top:0.0} m/s, at most {worst:0.00} m off the centreline");
            Assert.Less(t, 55, "to B");
            Assert.LessOrEqual(worst, 6, "on the road");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void NoisyDriver_StaysInTheCorridor_AndResets(int seed)
        {
            var rng = new System.Random(seed);
            var v = Jeep();
            const float dt = 1 / 30f;
            float noise = 0, stuck = 0;
            int resets = 0;
            for (float t = 0; t < 120; t += dt)
            {
                // Lurches of steering and throttle on top of the bot.
                if (rng.NextDouble() < 0.05f) noise = (float)(rng.NextDouble() * 2 - 1) * 1.6f;
                var input = DriveBot.Input(v, Drive);
                input.Steer = Mathf.Clamp(input.Steer + noise, -1, 1);
                if (rng.NextDouble() < 0.01f) input.Handbrake = true;
                if (Drive.Nearest(new Vector2(v.Position.x, v.Position.z)).S > Drive.Length - 8) v.Place(RoadLayout.ParkingA.position, RoadLayout.ParkingA.rotation.eulerAngles.y);
                v.Tick(input, dt);
                Assert.IsTrue(InBounds(v), $"in the corridor at {v.Position} (t = {t:0.0})");
                stuck = v.ResetOffered ? stuck + dt : 0;
                if (v.AutoReset || v.ResetOffered && stuck > 3)
                {
                    v.ResetToRoad();
                    resets++;
                    stuck = 0;
                }
                Assert.Less(stuck, J.OffRoadAuto + 1, "no stuck state lasts");
            }
            Debug.Log($"[DriveBot] noisy driver {seed}: {resets} resets");
        }

        [Test]
        public void ReverseOnly_CantEscape()
        {
            var rng = new System.Random(9);
            var v = Jeep();
            const float dt = 1 / 30f;
            float steer = 0;
            for (float t = 0; t < 90; t += dt)
            {
                if (rng.NextDouble() < 0.03f) steer = (float)(rng.NextDouble() * 2 - 1);
                v.Tick(new VehicleInput { Throttle = -1, Steer = steer }, dt);
                Assert.IsTrue(InBounds(v), $"in the corridor at {v.Position} (t = {t:0.0})");
                if (v.AutoReset) v.ResetToRoad();
            }
        }
    }
}
