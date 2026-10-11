using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // Traversal in the real scenes: key routing (the arena keeps Space for the dash), the
    // jungle's jump, and the drop landing on and against blocks.
    public class TraversalPlayTests : ArenaTestBase
    {
        static JungleStage Stage => Object.FindFirstObjectByType<JungleStage>();

        static IEnumerator LoadJungle(DropStart start)
        {
            yield return LoadMenu();
            Campaign.Active = true;
            Campaign.Run = new RunState();
            Campaign.Start = start;
            StageLoader.LoadJungle();
            float end = Time.realtimeSinceStartup + 60;
            while (Stage == null || GameManager.Instance.jungle != Stage)
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out loading the jungle");
                yield return null;
            }
            yield return Frames(3);
            Gm.ClearQueue();
        }

        // Open ground east of the LZ, clear of the course.
        static Vector3 OpenGround()
        {
            Vector3 p = Stage.lz + new Vector3(4, 0, 2);
            p.y = Stage.Surface(p);
            return p;
        }

        [UnityTest]
        public IEnumerator Arena_SpaceAndShiftDash_NoJump()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            Assert.IsFalse(World.TraversalEnabled);
            foreach (var key in new[] { keyboard.spaceKey, keyboard.leftShiftKey })
            {
                yield return new WaitUntil(() => Player.DashReady >= 1);
                PressAndRelease(key);
                yield return null;
                Assert.Less(Player.DashReady, 1, $"{key.name} dashes");
                Assert.AreEqual(0, Player.transform.position.y, 1e-4f, "no jump in the arena");
            }
        }

        [UnityTest]
        public IEnumerator Jungle_SpaceJumps_ShiftDashes()
        {
            yield return LoadJungle(DropStart.Landed);
            Assert.IsTrue(World.TraversalEnabled);
            Vector3 at = OpenGround();
            Player.transform.position = at;
            yield return Frames(2);
            PressAndRelease(keyboard.spaceKey);
            float peak = 0;
            for (float t = 0; t < 0.4f; t += Time.deltaTime)
            {
                yield return null;
                peak = Mathf.Max(peak, Player.transform.position.y - at.y);
            }
            Assert.Greater(peak, 0.8f, "Space jumps");
            Assert.AreEqual(1, Player.DashReady, 1e-4f, "and does not dash");
            yield return new WaitUntil(() => Player.Traversal.Grounded);
            PressAndRelease(keyboard.leftShiftKey);
            yield return null;
            Assert.Less(Player.DashReady, 1, "Shift dashes");
        }

        [UnityTest]
        public IEnumerator Drop_LandsOnABlockTop()
        {
            yield return LoadJungle(DropStart.Jump);
            Vector3 lz = Stage.lz;
            // A 12 m square slab, 1.5 m high, all over the LZ.
            float top = lz.y + 1.5f;
            World.Blocks = World.Blocks.Concat(new[] { new Block { Center = new Vector2(lz.x, lz.z), Half = new Vector2(6, 6), Bottom = lz.y - 2, Top = top } }).ToArray();
            yield return LandAt(lz + Vector3.up * 5);
            Assert.AreEqual(top, Player.transform.position.y, 0.05f, "stands on the slab");
            Assert.AreEqual(top, Player.Traversal.Position.y, 0.05f);
        }

        [UnityTest]
        public IEnumerator Drop_LandsAgainstABlockSide_AndIsPushedOut()
        {
            yield return LoadJungle(DropStart.Jump);
            Vector3 lz = Stage.lz;
            // A 4 m wall just north of where he touches down; the landing slide runs north into it.
            var wall = new Block { Center = new Vector2(lz.x, lz.z + 0.8f), Half = new Vector2(5, 0.5f), Bottom = lz.y - 2, Top = lz.y + 4 };
            World.Blocks = World.Blocks.Concat(new[] { wall }).ToArray();
            // Touching down with his centre 0.4 m from the face (inside his radius), sliding on north.
            yield return LandAt(new Vector3(lz.x, lz.y + JungleStage.BodyCenter + 0.05f, lz.z - 0.1f));
            Vector3 p = Player.transform.position;
            Debug.Log($"[Trav] side landing: touchdown {touchdown:F2}, standing at {p:F2}");
            Assert.Less(p.z, lz.z + 0.3f - GameConfig.PlayerRadius + 0.01f, "outside the wall");
            Assert.Less(p.y, lz.y + 1, "on the ground, not on the wall");
        }

        Vector3 touchdown;

        // ?campaign=course, in each view: W toward the log, Space vaults it.
        [UnityTest]
        public IEnumerator CourseSmoke_VaultTheLog_Behind() => VaultTheLog(CameraFollow.View.Behind);

        [UnityTest]
        public IEnumerator CourseSmoke_VaultTheLog_High() => VaultTheLog(CameraFollow.View.High);

        IEnumerator VaultTheLog(CameraFollow.View view)
        {
            PlayerPrefs.SetInt("wz.view.jungle", (int)view);
            yield return LoadJungle(DropStart.Course);
            var follow = Gm.cameraFollow;
            Assert.AreEqual(view, follow.Current);
            Assert.IsTrue(World.Blocks.Length > 0, "the course is registered");
            var log = CourseLayout.Pieces.First(p => p.Name == "1_Log").Block;
            Press(keyboard.wKey);
            bool vaulted = false;
            float end = Time.time + 6;
            while (Time.time < end && !(vaulted && Player.Traversal.Grounded))
            {
                // W runs where the camera looks (behind) or north (high).
                Vector3 run = Quaternion.Euler(0, view == CameraFollow.View.Behind ? follow.Yaw : 0, 0) * Vector3.forward;
                if (TraversalProbe.Find(Player.Traversal.Position, run).Kind == TraversalProbe.Kind.Vault) PressAndRelease(keyboard.spaceKey);
                vaulted |= Player.Traversal.State == TraversalState.Vault;
                yield return null;
            }
            Release(keyboard.wKey);
            Assert.IsTrue(vaulted, "vaulted");
            Assert.Greater(log.ToLocal(Player.transform.position).z, log.Half.y, "beyond the log");
        }

        // Under the canopy, just above `at`, sinking and drifting north: wait for the walk.
        IEnumerator LandAt(Vector3 at)
        {
            Stage.JumpNow();
            yield return null;
            var dive = Stage.Dive;
            dive.Deploy();
            float end = Time.realtimeSinceStartup + 30;
            while (dive.Current != Skydive.State.Canopy)
            {
                dive.Position = at + Vector3.up * 40;
                dive.Velocity = Vector3.down * 5;
                if (Time.realtimeSinceStartup > end) Assert.Fail("No canopy");
                yield return null;
            }
            dive.Position = at;
            dive.Heading = 0;
            dive.Velocity = new Vector3(0, -4, 1.5f);
            Time.timeScale = 4;
            while (Stage.CurrentPhase != JungleStage.Phase.Walk)
            {
                if (Stage.CurrentPhase == JungleStage.Phase.Landing && touchdown == default) touchdown = Player.transform.position;
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for the walk");
                yield return null;
            }
            Time.timeScale = 1;
            yield return Frames(3);
        }
    }
}
