using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // The gun on touch (ForceTouch): a drag anywhere aims, FIRE held fires, ZOOM zooms, the VIEW
    // badge is hidden, the heat shows.
    public class TouchGunPlayTests : JungleTestBase
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
        static CameraFollow Follow => GameManager.Instance.cameraFollow;
        static Vector2 Centre(RectTransform rt) => rt.TransformPoint(rt.rect.center);

        IEnumerator OnTheGun()
        {
            yield return LoadJungle(DropStart.Ride);
            yield return WaitFor(() => Stage.Road.CurrentPhase == RoadStage.Phase.Ride && !Stage.Road.Fading, 8, "the ride");
            yield return Frames(2);
            Assert.AreEqual(TouchLayout.Scheme.Gun, Touch.Scheme);
        }

        [UnityTest]
        public IEnumerator DragAnywhereAims_FireHeldFires_ZoomZooms_NoAutoBadge()
        {
            yield return OnTheGun();
            Assert.IsTrue(Touch.actionButton.gameObject.activeSelf);
            Assert.AreEqual("FIRE", Touch.actionLabel.text);
            Assert.IsFalse(Touch.fireButton.gameObject.activeSelf, "the gun's trigger is the corner button");
            Assert.IsFalse(Touch.dashButton.gameObject.activeSelf);
            Assert.IsFalse(Touch.viewBadge.activeSelf, "no AUTO badge on the gun");
            // A drag on the left half aims too (the stick's half when driving).
            float yaw = Follow.GunYaw;
            Vector2 left = new Vector2(Screen.width * 0.25f, Screen.height * 0.5f);
            BeginTouch(1, left);
            yield return null;
            for (int i = 1; i <= 8; i++)
            {
                MoveTouch(1, left + new Vector2(i * 10, 0));
                yield return null;
            }
            Assert.Greater(Mathf.DeltaAngle(yaw, Follow.GunYaw), 4, "dragging right aims right");
            Assert.AreEqual(Vector2.zero, Touch.Stick);
            EndTouch(1, left + new Vector2(80, 0));
            yield return Frames(2);
            // FIRE held.
            Follow.SetGunAim(Jeep.Vehicle.Yaw + 70, 3);
            int before = Jeep.RoundsFired;
            Vector2 fire = Centre(Touch.actionButton);
            BeginTouch(2, fire);
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(Jeep.RoundsFired - before, 2, "fires while held");
            Assert.IsTrue(Stage.hud.heat.gameObject.activeSelf, "the heat shows");
            EndTouch(2, fire);
            // ZOOM.
            Assert.IsFalse(Follow.GunZoom);
            Touch.PressDrop();
            yield return Frames(2);
            Assert.IsTrue(Follow.GunZoom);
        }
    }
}
