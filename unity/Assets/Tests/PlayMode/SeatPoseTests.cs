using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // The Warden and the squadmate in the jeep (VehiclePose): hands on the wheel at full lock
    // and on the gun's grips at full pitch, the rifle slung in the seat and back in his hands
    // just after, and the squadmate's hands on its wheel.
    public class SeatPoseTests : JungleTestBase
    {
        static VehicleController Jeep => Stage.Road.jeep;

        // How far each hand bone ended up from its IK target this frame.
        static float HandMiss(Animator anim, WardenHandIK ik) => Mathf.Max(
            Vector3.Distance(anim.GetBoneTransform(HumanBodyBones.LeftHand).position, ik.LeftWrist),
            Vector3.Distance(anim.GetBoneTransform(HumanBodyBones.RightHand).position, ik.RightWrist));

        [UnityTest]
        public IEnumerator Driver_HandsOnTheWheel_AtFullLock_RifleSlung_BackInHandsAfter()
        {
            yield return LoadJungle(DropStart.Drive);
            Assert.IsTrue(Player.Seated);
            Press(keyboard.dKey);
            yield return new WaitForSeconds(1);
            Assert.AreEqual(1, Jeep.Vehicle.SteerInput, 1e-3f, "full lock");
            VehiclePose.WheelGrips(Jeep.rig.steering, out var left, out var right);
            Assert.Less(Vector3.Distance(View.handIK.LeftHold.Point, left), 1e-3f, "the hold is the wheel's grip");
            Assert.Less(HandMiss(View.animator, View.handIK), 0.1f, "hands on the wheel");
            Release(keyboard.dKey);
            Assert.IsTrue(View.Slung, "rifle slung in the seat");
            Assert.Less(View.handIK.weight, 0.01f);
            // Out: the rifle back in his hands within half a second.
            yield return new WaitForSeconds(0.5f);
            PressAndRelease(keyboard.cKey);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(Player.Seated);
            Assert.IsFalse(View.Slung);
            Assert.Greater(View.handIK.weight, 0.99f, "both hands back on the rifle");
        }

        [UnityTest]
        public IEnumerator Gunner_HandsOnTheGrips_AtFullPitch_SquadmateOnItsWheel()
        {
            yield return LoadJungle(DropStart.Ride);
            yield return WaitFor(() => Stage.Road.CurrentPhase == RoadStage.Phase.Ride && !Stage.Road.Fading, 8, "the ride");
            var follow = Gm.cameraFollow;
            follow.SetGunAim(Jeep.Vehicle.Yaw + 60, Turret.MaxPitch + 20);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(Turret.MaxPitch, Jeep.Turret.Pitch, 2, "full pitch");
            VehiclePose.GunGrips(Jeep.rig.gun, out var left, out _);
            Assert.Less(Vector3.Distance(View.handIK.LeftHold.Point, left), 0.05f, "the hold is the gun's grip");
            Assert.Less(HandMiss(View.animator, View.handIK), 0.1f, "hands on the grips");
            // The squadmate's hands on its wheel, which turns with the road (looking over the cab:
            // off screen it isn't animated).
            follow.SetGunAim(Jeep.Vehicle.Yaw + 12, 14);
            yield return new WaitForSeconds(0.3f);
            var mate = Stage.Road.Squadmate;
            Assert.IsNotNull(mate);
            Assert.IsTrue(mate.gameObject.activeInHierarchy);
            var anim = mate.GetComponentInChildren<Animator>();
            var ik = mate.GetComponentInChildren<WardenHandIK>();
            VehiclePose.WheelGrips(Jeep.rig.steering, out left, out _);
            Assert.Less(Vector3.Distance(ik.LeftHold.Point, left), 0.05f);
            Assert.Less(HandMiss(anim, ik), 0.1f, "the squadmate's hands on the wheel");
            // Not a second Warden: tinted, no rim or see-through pass, helmet on, no rifle.
            var body = mate.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.AreEqual(1, body.sharedMaterials.Length);
            var block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block, 0);
            Assert.AreEqual(WardenModelView.SquadmateTint, block.GetColor("_BaseColor"));
        }
    }
}
