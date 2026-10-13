using NUnit.Framework;

namespace WardenZero.Tests
{
    // The jungle's context camera rules, with fake enemy distances and zones.
    public class CameraContextTests
    {
        const float Dt = 0.1f;
        static readonly CameraFollow.View High = CameraFollow.View.High, Behind = CameraFollow.View.Behind;

        static CameraContext.Inputs At(float enemy, bool zone = false, bool settled = true) =>
            new CameraContext.Inputs { NearestEnemy = enemy, InZone = zone, Settled = settled };

        // Tick for `seconds`; returns the time at the end.
        static float Run(CameraContext c, CameraContext.Inputs i, float from, float seconds)
        {
            float t = from;
            for (; t < from + seconds; t += Dt) c.Tick(i, t);
            return t;
        }

        [Test]
        public void Combat_StartsAt18m_EndsOnly4sAfterClearOf25m()
        {
            var c = new CameraContext(Behind);
            float t = Run(c, At(19), 0, 1);
            Assert.AreEqual(CameraContext.Context.Explore, c.Current, "19 m: not yet");
            t = Run(c, At(17.5f), t, 0.2f);
            Assert.AreEqual(CameraContext.Context.Combat, c.Current);
            Assert.AreEqual(High, c.View);
            // Backing off to 22 m keeps it (inside the exit ring).
            t = Run(c, At(22), t, 10);
            Assert.AreEqual(CameraContext.Context.Combat, c.Current, "hysteresis");
            // Clear of 25 m: 4 s later it ends.
            t = Run(c, At(30), t, 3.5f);
            Assert.AreEqual(CameraContext.Context.Combat, c.Current);
            t = Run(c, At(30), t, 0.7f);
            Assert.AreEqual(CameraContext.Context.Explore, c.Current);
            Assert.AreEqual(Behind, c.View);
        }

        [Test]
        public void Zone_EntersCombat_WithNoEnemyNear()
        {
            var c = new CameraContext(Behind);
            Run(c, At(float.PositiveInfinity, true), 0, 0.2f);
            Assert.AreEqual(CameraContext.Context.Combat, c.Current);
        }

        [Test]
        public void Dwell_TwoSecondsBetweenAutomaticSwitches()
        {
            var c = new CameraContext(Behind);
            float t = Run(c, At(10), 0, 0.2f);
            Assert.AreEqual(High, c.View);
            // Combat ends (calm for 4 s) and starts again at once: the switch back waits for
            // the dwell, counted from the last switch.
            t = Run(c, At(40), t, 4.2f);
            Assert.AreEqual(Behind, c.View);
            float switchedAt = t;
            t = Run(c, At(10), t, 1.5f);
            Assert.AreEqual(Behind, c.View, "within the dwell");
            Run(c, At(10), t, switchedAt + CameraContext.Dwell + 0.2f - t);
            Assert.AreEqual(High, c.View);
        }

        [Test]
        public void NoAutomaticSwitch_MidAirOrMidMove()
        {
            var c = new CameraContext(Behind);
            float t = Run(c, At(10, settled: false), 0, 3);
            Assert.AreEqual(CameraContext.Context.Combat, c.Current);
            Assert.AreEqual(Behind, c.View, "waits for him to be on the ground");
            Run(c, At(10), t, 0.2f);
            Assert.AreEqual(High, c.View);
        }

        [Test]
        public void Override_SticksUntilTheContextChanges_PlatformingCountsAsExplore()
        {
            var c = new CameraContext(Behind);
            Assert.AreEqual(High, c.Override(), "V while exploring");
            float t = Run(c, At(40), 0, 3);
            Assert.AreEqual(High, c.View, "pinned");
            var climbing = At(40);
            climbing.Platforming = true;
            t = Run(c, climbing, t, 3);
            Assert.AreEqual(CameraContext.Context.Platforming, c.Current);
            Assert.AreEqual(High, c.View, "not undone at the next obstacle");
            // Into combat: the pin clears and the context's view (high) holds.
            t = Run(c, At(10), t, 0.5f);
            Assert.IsFalse(c.Pinned);
            // V in combat pins behind; combat ending clears it and switches back by itself.
            Assert.AreEqual(Behind, c.Override());
            t = Run(c, At(10), t, 3);
            Assert.AreEqual(Behind, c.View, "pinned in combat");
            Run(c, At(40), t, 6);
            Assert.IsFalse(c.Pinned);
            Assert.AreEqual(Behind, c.View);
        }

        [Test]
        public void Manual_NeverSwitchesByItself()
        {
            var c = new CameraContext(Behind) { Auto = false };
            float t = Run(c, At(5), 0, 5);
            Assert.AreEqual(CameraContext.Context.Combat, c.Current);
            Assert.AreEqual(Behind, c.View);
            Assert.AreEqual(High, c.Override());
            Run(c, At(40), t, 10);
            Assert.AreEqual(High, c.View, "V only");
        }

        // ------------------------------------------------------------------ M3: the jeep

        static CameraContext.Inputs Seated(VehicleController.Seat seat, float enemy = float.PositiveInfinity) =>
            new CameraContext.Inputs { NearestEnemy = enemy, Settled = true, Seat = seat };

        [Test]
        public void GettingIn_ClearsAPin_AndNothingSwitchesWhileDriving()
        {
            var c = new CameraContext(Behind);
            c.Override();
            Assert.IsTrue(c.Pinned);
            float t = Run(c, Seated(VehicleController.Seat.Driver), 0, 0.2f);
            Assert.IsFalse(c.Pinned, "getting in is a context change");
            Assert.AreEqual(CameraContext.Context.Driving, c.Current);
            var view = c.View;
            // Enemies close by while driving: no switch.
            for (; t < 10; t += Dt) Assert.IsNull(c.Tick(Seated(VehicleController.Seat.Driver, 5), t));
            Assert.AreEqual(view, c.View);
            Assert.AreEqual(CameraContext.Context.Driving, c.Current);
        }

        [Test]
        public void GettingOut_BackToExploreAndBehind()
        {
            var c = new CameraContext(Behind);
            float t = Run(c, At(10), 0, 3);
            Assert.AreEqual(High, c.View, "in combat before he got in");
            t = Run(c, Seated(VehicleController.Seat.Driver), t, 2);
            var back = c.Tick(At(float.PositiveInfinity), t);
            Assert.AreEqual(Behind, back, "getting out goes back behind him, at once");
            Assert.AreEqual(CameraContext.Context.Explore, c.Current);
            // Manual: getting out leaves the view alone.
            var m = new CameraContext(High) { Auto = false };
            Run(m, Seated(VehicleController.Seat.Driver), 0, 1);
            Assert.IsNull(m.Tick(At(float.PositiveInfinity), 1));
            Assert.AreEqual(High, m.View);
        }
    }
}
