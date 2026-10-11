using NUnit.Framework;
using UnityEngine;

namespace WardenZero.Tests
{
    // The behind view's pure maths: the Esc guard after a lost pointer lock, and the rig's
    // portrait adjustment shared by the live camera and the landing's settle pose.
    public class CameraRigTests
    {
        [Test]
        public void EscGuard_IgnoresEscJustAfterALostLock()
        {
            Assert.IsTrue(CameraFollow.EscapeMayResume(10, -1), "no lock was lost");
            Assert.IsFalse(CameraFollow.EscapeMayResume(10.02f, 10), "the browser's late Esc");
            Assert.IsFalse(CameraFollow.EscapeMayResume(10 + CameraFollow.EscGuard * 0.9f, 10));
            Assert.IsTrue(CameraFollow.EscapeMayResume(10 + CameraFollow.EscGuard, 10), "a deliberate Esc later resumes");
        }

        [Test]
        public void Rig_Portrait_PullsTheShoulderIn_StepsBack_WidensTheLens()
        {
            var land = CameraFollow.Rig(16f / 9);
            var tall = CameraFollow.Rig(390f / 844);
            Assert.AreEqual(GameConfig.ShoulderOffset, land.shoulder);
            Assert.AreEqual(GameConfig.BehindDistance, land.distance, 1e-4f);
            Assert.AreEqual(GameConfig.BehindFov, land.fov, 1e-4f);
            Assert.Less(tall.shoulder.x, land.shoulder.x);
            Assert.Greater(tall.distance, land.distance);
            Assert.Greater(tall.fov, land.fov);
        }

        [Test]
        public void BehindPose_UsesTheRigForTheScreen()
        {
            float aspect = 390f / 844;
            var pose = CameraFollow.BehindPose(Vector3.zero, 0, aspect, 0);
            var rig = CameraFollow.Rig(aspect);
            Vector3 expect = Vector3.up * GameConfig.PivotHeight + rig.shoulder - Vector3.forward * rig.distance;
            Assert.Less(Vector3.Distance(expect, pose.position), 1e-4f);
        }
    }
}
