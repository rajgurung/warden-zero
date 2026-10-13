using NUnit.Framework;
using UnityEngine;

namespace WardenZero.Tests
{
    // The chase camera's pose maths (DriveCamera).
    public class DriveCameraTests
    {
        [TearDown]
        public void Reset() => World.UseArena();

        [Test]
        public void Heading_LagsTheJeep_ButNeverMoreThan25Degrees()
        {
            var c = new DriveCamera();
            c.Reset(0);
            float worst = 0;
            // A hard turn: the jeep swings 90 degrees a second.
            for (float t = 0; t < 2; t += 1 / 60f)
            {
                float jeep = 90 * t;
                c.Tick(jeep, 1 / 60f);
                worst = Mathf.Max(worst, Mathf.Abs(Mathf.DeltaAngle(jeep, c.Heading)));
            }
            Assert.Greater(worst, 5, "it lags, so the side shows");
            Assert.LessOrEqual(worst, DriveCamera.MaxLag + 1e-3f);
            // Straight again: it catches up.
            for (int i = 0; i < 120; i++) c.Tick(180, 1 / 60f);
            Assert.AreEqual(180, c.Heading, 0.5f);
        }

        [Test]
        public void DistanceAndLens_GrowWithSpeed()
        {
            var c = new DriveCamera();
            c.Reset(0);
            var still = c.Pose(Vector3.zero, 0, 16 / 9f, out float fov0, out var pivot);
            var fast = c.Pose(Vector3.zero, GameConfig.Jeep.TopSpeed, 16 / 9f, out float fov1, out _);
            Assert.AreEqual(DriveCamera.Distance, Vector3.Distance(still.position, pivot), 1e-3f);
            Assert.AreEqual(DriveCamera.DistanceAtSpeed, Vector3.Distance(fast.position, pivot), 1e-3f);
            Assert.AreEqual(DriveCamera.Fov, fov0, 1e-3f);
            Assert.AreEqual(DriveCamera.FovAtSpeed, fov1, 1e-3f);
            Assert.Less(still.position.z, 0, "behind the jeep");
            Assert.Greater(still.position.y, pivot.y, "and above it");
            // Portrait: further back, wider.
            var tall = c.Pose(Vector3.zero, 0, 9 / 19.5f, out float fovP, out _);
            Assert.Greater(Vector3.Distance(tall.position, pivot), DriveCamera.Distance + 0.5f);
            Assert.Greater(fovP, fov0);
        }

        [Test]
        public void Orbit_RecentresAfter1Point2s()
        {
            var c = new DriveCamera();
            c.Reset(0);
            c.Look(90, 20);
            for (float t = 0; t < 1.1f; t += 0.1f) c.Tick(0, 0.1f);
            Assert.AreEqual(90, c.OrbitYaw, 1e-3f, "holds while he looks");
            for (float t = 0; t < 3; t += 0.1f) c.Tick(0, 0.1f);
            Assert.Less(Mathf.Abs(c.OrbitYaw), 2, "eased back");
            Assert.Less(Mathf.Abs(c.OrbitPitch), 1);
            c.Look(500, 500);
            Assert.AreEqual(DriveCamera.OrbitYawMax, c.OrbitYaw);
            Assert.AreEqual(DriveCamera.OrbitPitchMax, c.OrbitPitch);
        }

        [Test]
        public void Collide_StaysAboveTheGround_AndSkipsTheJeepsOwnBox()
        {
            World.Use(100, 100, new WallRect[0], new Vector3[0]);
            World.Ground = p => p.z < -3 ? (-3 - p.z) * 0.8f : 0; // a bank behind the jeep
            // The jeep's own box around the pivot, and a wall further back.
            World.Blocks = new[]
            {
                new Block { Center = Vector2.zero, Half = new Vector2(1.2f, 2.6f), Bottom = -0.3f, Top = 2.1f, Flags = BlockFlags.Vehicle },
            };
            var pivot = new Vector3(0, DriveCamera.PivotHeight, 0);
            var cam = DriveCamera.Collide(pivot, new Vector3(0, 3.8f, -7.9f));
            Assert.GreaterOrEqual(cam.y - World.HeightAt(cam), DriveCamera.Clearance - 1e-3f, "clear of the ground");
            Assert.Less(cam.z, -3, "not stopped by its own box");
            World.Blocks = new[] { World.Blocks[0], new Block { Center = new Vector2(0, -5), Half = new Vector2(4, 0.3f), Bottom = -1, Top = 6 } };
            cam = DriveCamera.Collide(pivot, new Vector3(0, 3.8f, -7.9f));
            Assert.Greater(cam.z, -5 + 0.3f, "but a wall behind pulls it in");
        }

        [Test]
        public void GunPose_LooksAlongTheAim_CloseAndWide()
        {
            var yoke = new Vector3(3, 2, 7);
            foreach (bool wide in new[] { false, true })
            {
                var pose = DriveCamera.GunPose(yoke, 60, 10, wide, out var pivot);
                var aim = Quaternion.Euler(-10, 60, 0) * Vector3.forward;
                var far = pivot + aim * 30;
                var ray = new Ray(pose.position, pose.rotation * Vector3.forward);
                float miss = Vector3.Cross(ray.direction, far - ray.origin).magnitude;
                Assert.Less(miss, 0.01f, "the centre of the view is the gun's aim");
                Assert.Less(Vector3.Dot(pose.position - pivot, aim), 0, "behind the gun");
            }
            var close = DriveCamera.GunPose(yoke, 60, 10, false, out var p0);
            var wideP = DriveCamera.GunPose(yoke, 60, 10, true, out _);
            Assert.Greater(Vector3.Distance(wideP.position, p0), Vector3.Distance(close.position, p0), "Wide is further back");
        }

        [Test]
        public void AimAssist_SlowsNearATarget_PullsOntoIt_NeverThroughCover()
        {
            var eye = Vector3.zero;
            var target = Quaternion.Euler(0, 3.5f, 0) * Vector3.forward * 40; // 3.5 degrees right
            var list = new System.Collections.Generic.List<Vector3> { target };
            var pull = AimAssist.Pull(eye, 0, 0, list, (a, b) => true, 0.1f, out float slow);
            Assert.Greater(pull.x, 0, "pulled toward it");
            Assert.AreEqual(AimAssist.PullRate * 0.1f, pull.magnitude, 1e-3f, "gently");
            Assert.AreEqual(1, slow, "not slowed yet at 3.5 degrees");
            AimAssist.Pull(eye, 2, 0, list, (a, b) => true, 0.1f, out slow);
            Assert.AreEqual(AimAssist.Slow, slow, "slowed within 3 degrees");
            pull = AimAssist.Pull(eye, -2, 0, list, (a, b) => true, 0.1f, out slow);
            Assert.AreEqual(Vector2.zero, pull, "nothing from 5.5 degrees away");
            pull = AimAssist.Pull(eye, 1, 0, list, (a, b) => false, 0.1f, out slow);
            Assert.AreEqual(Vector2.zero, pull, "never through cover");
            Assert.AreEqual(1, slow);
        }
    }
}
