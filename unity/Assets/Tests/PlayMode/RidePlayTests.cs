using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // The gun ride in the real jungle: B starts it by itself, the gun destroys targets (drums
    // chain), it never fires into the cab, no pausing in the swap, and the ride ends the
    // milestone.
    public class RidePlayTests : JungleTestBase
    {
        string savedPrefs;

        public override void Setup()
        {
            base.Setup();
            savedPrefs = PlayerPrefs.GetString(CampaignSave.Key, null);
        }

        public override void TearDown()
        {
            Time.captureFramerate = 0;
            if (string.IsNullOrEmpty(savedPrefs)) PlayerPrefs.DeleteKey(CampaignSave.Key);
            else PlayerPrefs.SetString(CampaignSave.Key, savedPrefs);
            base.TearDown();
        }

        static RoadStage Road => Stage.Road;
        static VehicleController Jeep => Road.jeep;
        static CameraFollow Follow => GameManager.Instance.cameraFollow;

        IEnumerator OnTheGun()
        {
            yield return LoadJungle(DropStart.CheckpointB);
            Assert.AreEqual(RoadStage.Phase.SecuredB, Road.CurrentPhase);
            yield return WaitFor(() => Road.Fading, RoadStage.SwapBeat + 1, "the swap to start by itself");
            Gm.Pause(true);
            Assert.AreNotEqual(GameManager.Mode.Paused, Gm.CurrentMode, "no pausing in the swap fade");
            yield return WaitFor(() => Road.CurrentPhase == RoadStage.Phase.Ride && !Road.Fading, 5, "the ride");
            Assert.IsTrue(Player.Seated);
            Assert.AreEqual(VehicleController.Seat.Gunner, Jeep.Occupied);
            Assert.IsTrue(Road.squadmate.gameObject.activeInHierarchy, "someone at the wheel");
            Assert.IsTrue(Follow.Gunning);
            Assert.IsTrue(Stage.rideOpen);
        }

        // Turn the gun camera onto a point.
        static void AimAt(Vector3 p)
        {
            var yoke = Jeep.rig.gunYoke.position + Vector3.up * DriveCamera.GunPivotUp;
            Vector3 d = p - yoke;
            Follow.SetGunAim(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
        }

        [UnityTest]
        public IEnumerator B_StartsTheRide_HoldingFireOnADrum_ChainsTheWarmUp()
        {
            yield return OnTheGun();
            Assert.IsTrue(Jeep.Turret.Heat == 0);
            var drums = Road.targets.Where(t => t.kind == RoadLayout.TargetKind.Drum).OrderBy(t => Vector3.Distance(t.transform.position, Jeep.transform.position)).Take(3).ToArray();
            yield return WaitFor(() => Vector3.Distance(drums[0].transform.position, Jeep.transform.position) < 30, 20, "the warm-up drums in range");
            Jeep.TriggerHeld = true;
            float end = Time.time + 6;
            while (Time.time < end && !drums[0].Down)
            {
                AimAt(drums[0].Centre);
                yield return null;
            }
            Jeep.TriggerHeld = false;
            Assert.IsTrue(drums[0].Down, "holding fire on a drum destroys it");
            Assert.Greater(Gm.Run.Score, 0, "score");
            yield return new WaitForSeconds(Destructible.ChainDelay * 3 + 0.1f);
            Assert.IsTrue(drums.All(d => d.Down), "and the drums beside it go up too");
            Assert.AreEqual(0, Gm.Run.Xp, "score only, no XP");
        }

        [UnityTest]
        public IEnumerator Gun_NeverFiresIntoTheCab()
        {
            yield return OnTheGun();
            Follow.SetGunAim(Jeep.Vehicle.Yaw, -6);
            Jeep.TriggerHeld = true;
            yield return new WaitForSeconds(1);
            Jeep.TriggerHeld = false;
            Assert.IsTrue(Jeep.Turret.InNoFireCone);
            Assert.AreEqual(0, Jeep.RoundsFired, "nothing fired at the driver's seat");
        }

        [UnityTest]
        public IEnumerator Hitscan_HitsAtTwentyFramesASecond()
        {
            yield return OnTheGun();
            // The nearest warm-up drum, in the open.
            var drum = Road.targets.Where(t => t.kind == RoadLayout.TargetKind.Drum).OrderBy(t => Vector3.Distance(t.transform.position, Jeep.transform.position)).First();
            yield return WaitFor(() => Vector3.Distance(drum.transform.position, Jeep.transform.position) < 30, 20, "the drum in range");
            Time.captureFramerate = 20; // dt = 1/20
            Jeep.TriggerHeld = true;
            float end = Time.time + 4;
            while (Time.time < end && !drum.Down)
            {
                AimAt(drum.Centre);
                yield return null;
            }
            Jeep.TriggerHeld = false;
            Time.captureFramerate = 0;
            Assert.AreEqual(1 / 20f, Time.deltaTime, 1e-3f);
            Assert.IsTrue(drum.Down, "the rounds hit at dt = 1/20");
        }

        [UnityTest]
        public IEnumerator Ride_EndsInMilestone3Complete()
        {
            Campaign.Save(new RunState(), CampaignSave.CheckpointB);
            yield return OnTheGun();
            Time.timeScale = 6;
            yield return WaitFor(() => Gm.menus.resultPanel.activeSelf, 40, "the milestone result", () => $"ride s {Road.Ride.S:0} end {Road.Ride.AtEnd} phase {Road.CurrentPhase}");
            Time.timeScale = 1;
            Assert.AreEqual("MILESTONE 3 COMPLETE", Gm.menus.resultEyebrow.text);
            Assert.AreEqual(CampaignSave.CheckpointB, CampaignSave.Load().Place, "no save at the end: Continue replays the ride from B");
        }
    }
}
