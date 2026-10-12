using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using J = WardenZero.GameConfig.Jeep;

namespace WardenZero.Tests
{
    // Driving on touch (ForceTouch): DRIVE gets in, GAS and the stick drive and steer, EXIT and
    // RESET show only when they apply, a touch on GAS never moves the stick, a right-half drag
    // orbits the chase camera.
    public class TouchDrivePlayTests : JungleTestBase
    {
        Touchscreen screen;

        public override void Setup()
        {
            base.Setup();
            screen = InputSystem.AddDevice<Touchscreen>();
            TouchControls.ForceTouch = true;
        }

        static TouchControls Touch => GameManager.Instance.hud.touch;
        static VehicleController Jeep => Stage.Road.jeep;
        static Vector2 Centre(RectTransform rt) => rt.TransformPoint(rt.rect.center);

        IEnumerator InTheJeep()
        {
            yield return LoadJungle(DropStart.CheckpointA);
            var door = Jeep.rig.doorL.position;
            door.y = World.HeightAt(door);
            Player.transform.position = door;
            yield return Frames(3);
            Assert.IsTrue(Touch.actionButton.gameObject.activeSelf);
            Assert.AreEqual("DRIVE", Touch.actionLabel.text, "the context button says DRIVE");
            Touch.PressAction();
            yield return Frames(2);
            Assert.IsTrue(Player.Seated);
            yield return WaitFor(() => !Jeep.Entering, 2, "seated");
            yield return null;
            Assert.AreEqual(TouchLayout.Scheme.Drive, Touch.Scheme);
            Assert.AreEqual("GAS", Touch.actionLabel.text);
        }

        [UnityTest]
        public IEnumerator Gas_AndTheStick_DriveAndTurn_GasNeverMovesTheStick()
        {
            yield return InTheJeep();
            float yaw = Jeep.Vehicle.Yaw;
            // GAS held: it goes; the touch on GAS leaves the stick alone.
            Vector2 gas = Centre(Touch.actionButton);
            BeginTouch(1, gas);
            yield return null;
            MoveTouch(1, gas + new Vector2(-30, 10));
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(Touch.ActionHeld);
            Assert.AreEqual(Vector2.zero, Touch.Stick, "GAS never starts the stick");
            Assert.Greater(Jeep.Vehicle.Speed, 3, "GAS drives");
            // The stick to the right turns right.
            Vector2 left = new Vector2(Screen.width * 0.2f, Screen.height * 0.4f);
            BeginTouch(2, left);
            yield return null;
            MoveTouch(2, left + new Vector2(80, 0));
            yield return new WaitForSeconds(0.8f);
            Assert.Greater(Touch.Stick.x, 0.9f);
            Assert.Greater(Mathf.DeltaAngle(yaw, Jeep.Vehicle.Yaw), 8, "the stick steers");
            EndTouch(2, left + new Vector2(80, 0));
            EndTouch(1, gas);
            yield return Frames(2);
            Assert.IsFalse(Touch.ActionHeld);
        }

        [UnityTest]
        public IEnumerator Exit_ShowsOnlyBelow2Point5_Reset_OnlyWhenOffered()
        {
            yield return InTheJeep();
            Assert.IsTrue(Touch.dropButton.gameObject.activeSelf, "stopped: EXIT");
            Assert.AreEqual("EXIT", Touch.dropButton.Find("Label").GetComponent<UnityEngine.UI.Text>().text);
            Vector2 gas = Centre(Touch.actionButton);
            BeginTouch(1, gas);
            yield return WaitFor(() => Jeep.Vehicle.Speed > J.ExitSpeed + 0.5f, 4, "speed");
            yield return null;
            Assert.IsFalse(Touch.dropButton.gameObject.activeSelf, "no EXIT at speed");
            Assert.IsFalse(Touch.bombButton.gameObject.activeSelf, "no RESET at speed");
            EndTouch(1, gas);
            // BRAKE to a stop; RESET after 2 s below 1 m/s.
            Vector2 brake = Centre(Touch.fireButton);
            BeginTouch(2, brake);
            yield return WaitFor(() => Jeep.Vehicle.Speed < 0.3f, 6, "the stop");
            EndTouch(2, brake);
            yield return Frames(2);
            Assert.IsTrue(Touch.dropButton.gameObject.activeSelf, "EXIT once stopped");
            Assert.IsFalse(Touch.bombButton.gameObject.activeSelf, "RESET not yet");
            yield return new WaitForSeconds(J.TouchResetAfter + 0.3f);
            Assert.IsTrue(Touch.bombButton.gameObject.activeSelf, "RESET after a while stopped");
            // EXIT gets him out; the buttons go back to the traversal ones.
            Touch.PressDrop();
            yield return Frames(2);
            Assert.IsFalse(Player.Seated);
            yield return Frames(2);
            Assert.AreEqual(TouchLayout.Scheme.OnFoot, Touch.Scheme);
            Assert.AreEqual("DRIVE", Touch.actionLabel.text, "beside the door: DRIVE again");
        }

        [UnityTest]
        public IEnumerator RightHalfDrag_OrbitsTheChaseCamera()
        {
            yield return InTheJeep();
            var follow = Gm.cameraFollow;
            yield return new WaitForSeconds(0.7f);
            Vector2 right = new Vector2(Screen.width * 0.6f, Screen.height * 0.6f);
            BeginTouch(3, right);
            yield return null;
            for (int i = 1; i <= 10; i++)
            {
                MoveTouch(3, right + new Vector2(i * 10, 0));
                yield return null;
            }
            Assert.Greater(follow.Drive.OrbitYaw, 5, "dragging right orbits right");
            Assert.AreEqual(Vector2.zero, Touch.Stick);
            EndTouch(3, right + new Vector2(100, 0));
        }
    }
}
