using NUnit.Framework;
using UnityEngine;

namespace WardenZero.Tests
{
    // The gun (Turret), the ride (TurretRide over RoadLayout.Ride) and the targets' numbers.
    public class TurretTests
    {
        [Test]
        public void Aim_YawAllRound_PitchClamped()
        {
            var t = new Turret();
            t.Aim(200, 10, 20);
            Assert.AreEqual(180, Mathf.Abs(t.Yaw), 1e-3f, "straight back over the tailgate");
            t.Aim(90, 80, 0);
            Assert.AreEqual(Turret.MaxPitch, t.Pitch);
            t.Aim(90, -40, 0);
            Assert.AreEqual(Turret.MinPitch, t.Pitch);
        }

        [Test]
        public void NoFireCone_AheadAndLow_NeverShootsTheCab()
        {
            var t = new Turret();
            t.Aim(10, 0, 0);
            Assert.IsTrue(t.InNoFireCone);
            Assert.AreEqual(0, t.Tick(true, 1), "no rounds into the cab");
            t.Aim(10, 8, 0);
            Assert.IsFalse(t.InNoFireCone, "over the cab is fine");
            t.Aim(40, 0, 0);
            Assert.IsFalse(t.InNoFireCone, "off to the side is fine");
            Assert.Greater(t.Tick(true, 0.5f), 0);
        }

        [Test]
        public void FireRate_AndHeat_BurstsNeverOverheat_HoldingDoes_ShortLock()
        {
            var t = new Turret();
            t.Aim(90, 5, 0);
            int rounds = 0;
            for (int i = 0; i < 60; i++) rounds += t.Tick(true, 1 / 60f);
            Assert.AreEqual(Turret.FireRate, rounds, 1, "8 rounds a second");
            // Bursts: 2 s on, 1.5 s off, for a 50 s ride.
            t.Reset();
            float time = 0;
            for (; time < 50; time += 1 / 60f)
            {
                bool on = time % 3.5f < 2;
                t.Tick(on, 1 / 60f);
                Assert.IsFalse(t.Overheated, $"bursts overheated at {time:0.0} s");
            }
            // Held: it overheats in about 5 s, locks, then fires again soon.
            t.Reset();
            time = 0;
            while (!t.Overheated && time < 20)
            {
                t.Tick(true, 1 / 60f);
                time += 1 / 60f;
            }
            Assert.That(time, Is.InRange(3.5f, 7), "holding the trigger overheats");
            float locked = 0;
            while (t.Overheated && locked < 10)
            {
                Assert.AreEqual(0, t.Tick(true, 1 / 60f));
                locked += 1 / 60f;
            }
            Assert.That(locked, Is.InRange(0.8f, 2), "a short penalty");
            Assert.Greater(t.Tick(true, 0.2f), 0, "and it fires again");
        }

        [Test]
        public void Ride_FollowsTheRoad_TakesAbout50s_PausesAtTheFuelDump()
        {
            var ride = new TurretRide();
            var v = new Vehicle();
            var start = RoadLayout.Ride.At(0);
            v.Place(new Vector3(start.Position.x, 0, start.Position.y), ride.Heading);
            const float dt = 1 / 60f;
            float paused = 0, worst = 0;
            while (!ride.AtEnd && ride.Time < 120)
            {
                ride.Tick(dt);
                v.MoveAlong(ride.Here.Position, ride.Heading, ride.Speed, ride.SteerAngle, dt);
                worst = Mathf.Max(worst, Vector2.Distance(new Vector2(v.Position.x, v.Position.z), RoadLayout.Ride.At(ride.S).Position));
                if (ride.Paused)
                {
                    paused += dt;
                    Assert.AreEqual(RoadLayout.FuelDumpPause, ride.S, 1, "paused at the fuel dump");
                }
            }
            Assert.IsTrue(ride.AtEnd);
            Assert.That(ride.Time, Is.InRange(45, 60), "the ride's length");
            Assert.AreEqual(RoadLayout.PauseTime, paused, 0.05f);
            Assert.LessOrEqual(worst, 0.05f, "on the road");
            Assert.AreEqual(RoadLayout.RideEnd, ride.S, 1e-3f);
            Assert.Greater(RoadLayout.Ride.Length - ride.S, 10, "short of the roadblock");
        }

        [Test]
        public void Targets_PlatesRiseBeforeTheJeepReachesThem_ChainsReachTheirNeighbours()
        {
            foreach (var t in RoadLayout.Targets)
            {
                if (t.Kind == RoadLayout.TargetKind.Plate) Assert.Less(t.PopAt, Mathf.Min(t.S, RoadLayout.RideEnd) - 10, "rises in good time");
                else Assert.Less(t.PopAt, 0);
            }
            Assert.AreEqual(1, Destructible.MaxHealth(RoadLayout.TargetKind.Plate), "a plate takes one round");
            Assert.LessOrEqual(Destructible.MaxHealth(RoadLayout.TargetKind.Plate), Turret.Damage);
            Assert.AreEqual(3, Mathf.CeilToInt(Destructible.MaxHealth(RoadLayout.TargetKind.Drum) / Turret.Damage), "a drum takes three");
            // The warm-up drums go up together; the fuel dump's drums are all in the trailer's blast.
            var drums = System.Array.FindAll(RoadLayout.Targets, t => t.Kind == RoadLayout.TargetKind.Drum && t.S < 45);
            var trailer = System.Array.Find(RoadLayout.Targets, t => t.Kind == RoadLayout.TargetKind.Trailer);
            Vector3 At(RoadLayout.Target t) => RoadLayout.RidePoint(t.S, t.Side, out _);
            for (int i = 1; i < drums.Length; i++) Assert.LessOrEqual(Vector3.Distance(At(drums[i]), At(drums[i - 1])), Destructible.BlastRadius(RoadLayout.TargetKind.Drum));
            foreach (var t in RoadLayout.Targets)
                if (t.Kind == RoadLayout.TargetKind.Drum && Mathf.Abs(t.S - trailer.S) < 10)
                    Assert.LessOrEqual(Vector3.Distance(At(t), At(trailer)), Destructible.BlastRadius(RoadLayout.TargetKind.Trailer));
        }
    }
}
