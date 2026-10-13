using System.Collections;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // The cameras in the jeep: the chase camera on getting in, V, getting out, Manual, and the
    // pointer lock kept through it all.
    public class DriveCameraPlayTests : JungleTestBase
    {
        static CameraFollow Follow => GameManager.Instance.cameraFollow;
        static CinemachineBrain Brain => Follow.brain;
        static VehicleController Jeep => Stage.Road.jeep;

        IEnumerator AtTheDoor()
        {
            yield return LoadJungle(DropStart.CheckpointA);
            var door = Jeep.rig.doorL.position;
            door.y = World.HeightAt(door);
            Player.transform.position = door;
            yield return Frames(3);
        }

        IEnumerator GetIn()
        {
            PressAndRelease(keyboard.spaceKey);
            yield return null;
            Assert.IsTrue(Player.Seated);
        }

        IEnumerator GetOut()
        {
            yield return WaitFor(() => !Jeep.Entering, 2, "seated");
            PressAndRelease(keyboard.cKey);
            yield return null;
            Assert.IsFalse(Player.Seated);
        }

        static bool Live(CinemachineCamera c) => c.gameObject.activeInHierarchy;

        [UnityTest]
        public IEnumerator GettingIn_BlendsToTheChaseCamera_GettingOut_BackBehind()
        {
            yield return AtTheDoor();
            Assert.IsTrue(Live(Follow.shoulderCam));
            yield return GetIn();
            yield return null;
            Assert.IsTrue(Live(Follow.driveCam), "the chase camera");
            Assert.IsFalse(Live(Follow.shoulderCam));
            Assert.IsTrue(Brain.IsBlending, "blended, not cut");
            yield return new WaitForSeconds(0.8f);
            Assert.AreSame(Follow.driveCam, Brain.ActiveVirtualCamera);
            // Behind and above the jeep, looking along it.
            var jeep = Jeep.transform;
            var cam = Player.cam.transform;
            Assert.Less(Vector3.Dot(cam.position - jeep.position, jeep.forward), -4, "behind it");
            Assert.Greater(cam.position.y, jeep.position.y + 2, "above it");
            Assert.Greater(Vector3.Dot(cam.forward, jeep.forward), 0.8f);
            yield return GetOut();
            yield return null;
            Assert.IsTrue(Live(Follow.shoulderCam), "behind him again");
            Assert.IsTrue(Brain.IsBlending);
            Assert.AreEqual(CameraFollow.View.Behind, Follow.Current);
        }

        [UnityTest]
        public IEnumerator V_PinsTheHighView_UntilHeGetsOut()
        {
            yield return AtTheDoor();
            yield return GetIn();
            yield return new WaitForSeconds(0.8f);
            PressAndRelease(keyboard.vKey);
            yield return Frames(2);
            Assert.AreEqual(CameraFollow.DriveView.High, Follow.DriveMode);
            Assert.IsTrue(Live(Follow.highCam));
            Assert.IsTrue(Brain.IsBlending, "Auto: blended");
            Assert.IsFalse(PlayerPrefs.HasKey(CameraFollow.DriveViewKey), "Auto never writes the remembered view");
            Press(keyboard.wKey);
            yield return new WaitForSeconds(2);
            Release(keyboard.wKey);
            Assert.AreEqual(CameraFollow.DriveView.High, Follow.DriveMode, "pinned while driving");
            var cam = Player.cam.transform.position;
            Assert.Greater(cam.y - Jeep.transform.position.y, DriveCamera.HighUp - 1.5f, "high above the jeep");
            Press(keyboard.sKey);
            yield return WaitFor(() => Jeep.Vehicle.Speed < 0.3f, 6, "the stop");
            Release(keyboard.sKey);
            yield return GetOut();
            yield return null;
            Assert.IsTrue(Live(Follow.shoulderCam), "the pin ends with the drive");
            yield return new WaitForSeconds(0.2f);
            // In again: the chase camera, the pin forgotten.
            var door = Jeep.rig.doorL.position;
            door.y = World.HeightAt(door);
            Player.transform.position = door;
            yield return Frames(2);
            yield return GetIn();
            yield return null;
            Assert.AreEqual(CameraFollow.DriveView.Chase, Follow.DriveMode);
        }

        [UnityTest]
        public IEnumerator Manual_VCuts_AndIsRemembered()
        {
            CameraFollow.AutoCamera = false;
            yield return AtTheDoor();
            yield return GetIn();
            yield return new WaitForSeconds(0.8f);
            Assert.AreSame(Follow.driveCam, Brain.ActiveVirtualCamera);
            PressAndRelease(keyboard.vKey);
            yield return null;
            Assert.IsTrue(Live(Follow.highCam));
            Assert.IsFalse(Brain.IsBlending, "Manual: a cut");
            Assert.AreEqual((int)CameraFollow.DriveView.High, PlayerPrefs.GetInt(CameraFollow.DriveViewKey, -1), "remembered");
            yield return GetOut();
            yield return new WaitForSeconds(0.2f);
            var door = Jeep.rig.doorL.position;
            door.y = World.HeightAt(door);
            Player.transform.position = door;
            yield return Frames(2);
            yield return GetIn();
            yield return null;
            Assert.AreEqual(CameraFollow.DriveView.High, Follow.DriveMode, "back in: the remembered view");
        }

        [UnityTest]
        public IEnumerator PointerLock_KeptThroughGettingInAndOut()
        {
            yield return AtTheDoor();
            yield return Frames(2);
            Assert.IsTrue(Follow.LockWanted, "on foot behind him");
            int dropped = 0;
            yield return GetIn();
            for (int i = 0; i < 40; i++)
            {
                if (!Follow.LockWanted) dropped++;
                yield return null;
            }
            yield return GetOut();
            for (int i = 0; i < 20; i++)
            {
                if (!Follow.LockWanted) dropped++;
                yield return null;
            }
            Assert.AreEqual(0, dropped, "the lock was asked for every frame");
            Assert.AreNotEqual(GameManager.Mode.Paused, Gm.CurrentMode);
        }

        [UnityTest]
        public IEnumerator PointerLock_KeptThroughTheSwapAtB()
        {
            yield return LoadJungle(DropStart.CheckpointB);
            yield return Frames(2);
            Assert.IsTrue(Follow.LockWanted, "on foot at B");
            int dropped = 0;
            float end = Time.time + RoadStage.SwapBeat + 6;
            while (Time.time < end && !(Stage.Road.CurrentPhase == RoadStage.Phase.Ride && !Stage.Road.Fading))
            {
                if (!Follow.LockWanted) dropped++;
                yield return null;
            }
            Assert.AreEqual(RoadStage.Phase.Ride, Stage.Road.CurrentPhase);
            for (int i = 0; i < 20; i++)
            {
                if (!Follow.LockWanted) dropped++;
                yield return null;
            }
            Assert.AreEqual(0, dropped, "the lock was asked for every frame, through the fade, onto the gun");
            Assert.AreNotEqual(GameManager.Mode.Paused, Gm.CurrentMode);
        }
    }
}
