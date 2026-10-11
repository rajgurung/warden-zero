using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
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

        // ------------------------------------------------------------------ presentation

        // The palm: just past the wrist bone along the hand (as JungleStage.LineEnd).
        static Vector3 Palm(bool left)
        {
            var a = View.animator;
            var hand = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var fore = a.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            return hand.position + (hand.position - fore.position).normalized * 0.08f * a.transform.lossyScale.y;
        }

        static float ToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, d) / d.sqrMagnitude);
            return Vector3.Distance(p, a + d * t);
        }

        static IEnumerator StandBefore(string piece, float back)
        {
            var b = CourseLayout.Pieces.First(p => p.Name == piece).Block;
            Vector3 at = b.ToWorld(new Vector3(0, 0, -b.Half.y - back));
            at.y = Stage.Surface(at);
            Vector3 face = -b.Normal(2);
            Player.transform.position = at;
            Player.SetAim(face);
            Gm.cameraFollow.SetLook(Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, 8);
            yield return new WaitForSeconds(0.5f);
        }

        static void CameraOutsideBlocks()
        {
            Vector3 c = Player.cam.transform.position;
            foreach (var b in World.Blocks)
                Assert.IsFalse(c.y > b.Bottom && c.y < b.Top && b.Contains(c, -0.05f), $"camera inside a block at {c}");
        }

        void CheckAnimator()
        {
            var t = Player.Traversal;
            string want = WardenModelView.AnimFor(t.State, t.VerticalSpeed);
            if (View.Action != null) return;
            Assert.AreEqual(want, View.TraversalAnim, $"traversal anim for {t.State}");
            string state = want != null && View.animator.HasState(0, Animator.StringToHash(want)) ? want : "Locomotion";
            Assert.AreEqual(state, View.BaseState);
        }

        [UnityTest]
        public IEnumerator Hang_HandsOnTheEdge_RifleSlung_ThenBackInHand()
        {
            yield return LoadJungle(DropStart.Course);
            Stats.MaxHealth = Stats.Health = 1e6f;
            yield return StandBefore("6_Terrace", 0.3f);
            PressAndRelease(keyboard.spaceKey);
            for (float t = 0; t < 2 && Player.Traversal.State != TraversalState.Hang; t += Time.deltaTime)
            {
                yield return null;
                CheckAnimator();
            }
            Assert.AreEqual(TraversalState.Hang, Player.Traversal.State);
            yield return new WaitForSeconds(0.5f);
            var trav = Player.Traversal;
            float worst = 0;
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                worst = Mathf.Max(worst, ToSegment(Palm(true), trav.EdgeA, trav.EdgeB), ToSegment(Palm(false), trav.EdgeA, trav.EdgeB));
                var a = View.animator;
                Assert.Less(a.GetBoneTransform(HumanBodyBones.LeftFoot).position.y, trav.Top, "feet below the edge");
                Assert.Less(a.GetBoneTransform(HumanBodyBones.RightFoot).position.y, trav.Top);
                CameraOutsideBlocks();
                CheckAnimator();
            }
            Debug.Log($"[Pose] hang: hands {worst:F3} m from the edge");
            Assert.Less(worst, 0.15f, "both hands on the edge");
            Assert.IsTrue(View.Slung);
            Assert.AreEqual(1, View.SlingAmount, 1e-3f, "rifle on his back");
            Assert.AreEqual(0, View.handIK.weight, 1e-3f, "hands off the rifle");

            PressAndRelease(keyboard.spaceKey);
            for (float t = 0; t < 2 && !Player.Traversal.Grounded; t += Time.deltaTime)
            {
                yield return null;
                if (!Player.Traversal.Grounded) Assert.IsTrue(View.Slung, "slung while he climbs");
                CameraOutsideBlocks();
                CheckAnimator();
            }
            Assert.IsTrue(Player.Traversal.Grounded);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(View.Slung);
            Assert.AreEqual(0, View.SlingAmount, 1e-3f, "rifle back in front");
            Assert.AreEqual(1, View.handIK.weight, 1e-3f, "both hands back on it within 0.5 s");
        }

        [UnityTest]
        public IEnumerator Vault_TheSupportHandReachesTheTop()
        {
            yield return LoadJungle(DropStart.Course);
            yield return StandBefore("1_Log", 0.5f);
            var log = CourseLayout.Pieces.First(p => p.Name == "1_Log").Block;
            PressAndRelease(keyboard.spaceKey);
            float best = float.MaxValue;
            bool vaulted = false;
            for (float t = 0; t < 1.5f && !(vaulted && Player.Traversal.Grounded); t += Time.deltaTime)
            {
                yield return null;
                if (Player.Traversal.State != TraversalState.Vault) continue;
                vaulted = true;
                CheckAnimator();
                Vector3 palm = Palm(true);
                if (log.Contains(palm, -0.1f)) best = Mathf.Min(best, Mathf.Abs(palm.y - log.Top));
            }
            Debug.Log($"[Pose] vault: support hand {best:F3} m from the top");
            Assert.IsTrue(vaulted);
            Assert.Less(best, 0.15f, "the left hand plants on the log");
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

    // Touch on the course: the context JUMP, DROP while hanging, look while hanging, the high
    // view's JUMP, and JUMP never starting the stick.
    public class TraversalTouchTests : ArenaTestBase
    {
        Touchscreen screen;

        public override void Setup()
        {
            base.Setup();
            screen = InputSystem.AddDevice<Touchscreen>();
            TouchControls.ForceTouch = true;
        }

        static JungleStage Stage => Object.FindFirstObjectByType<JungleStage>();
        static CameraFollow Follow => GameManager.Instance.cameraFollow;
        static TouchControls Touch => GameManager.Instance.hud.touch;
        static Vector2 Centre(RectTransform rt) => rt.TransformPoint(rt.rect.center);

        static IEnumerator LoadCourse()
        {
            yield return LoadMenu();
            Campaign.Active = true;
            Campaign.Run = new RunState();
            Campaign.Start = DropStart.Course;
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

        // Stand `back` metres out from a piece's -z face (the trail side), facing it.
        static IEnumerator Before(string piece, float back)
        {
            var b = CourseLayout.Pieces.First(p => p.Name == piece).Block;
            Vector3 at = b.ToWorld(new Vector3(0, 0, -b.Half.y - back));
            at.y = Stage.Surface(at);
            Vector3 face = -b.Normal(2);
            Player.transform.position = at;
            Player.SetAim(face);
            Follow.SetLook(Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, 8);
            yield return Frames(3);
        }

        IEnumerator Tap(RectTransform button, int id)
        {
            BeginTouch(id, Centre(button), queueEventOnly: true);
            yield return Frames(2);
            EndTouch(id, Centre(button), queueEventOnly: true);
            yield return Frames(2);
        }

        [UnityTest]
        public IEnumerator Jump_ShowsVault_AtTheLog_AndThePressVaults()
        {
            yield return LoadCourse();
            yield return Before("1_Log", 0.5f);
            Assert.IsTrue(Touch.actionButton.gameObject.activeSelf, "JUMP shows in the jungle");
            Assert.AreEqual("VAULT", Touch.actionLabel.text);
            Assert.IsFalse(Touch.dropButton.gameObject.activeSelf, "no DROP on the ground");
            yield return Tap(Touch.actionButton, 1);
            bool vaulted = false;
            for (float t = 0; t < 1.5f && !(vaulted && Player.Traversal.Grounded); t += Time.deltaTime)
            {
                vaulted |= Player.Traversal.State == TraversalState.Vault;
                yield return null;
            }
            Assert.IsTrue(vaulted);
            var log = CourseLayout.Pieces.First(p => p.Name == "1_Log").Block;
            Assert.Greater(log.ToLocal(Player.transform.position).z, log.Half.y, "beyond the log");
        }

        [UnityTest]
        public IEnumerator Drop_OnlyWhileHanging_AndLookStillTurnsTheCamera()
        {
            yield return LoadCourse();
            yield return Before("6_Terrace", 0.3f);
            Assert.AreEqual("CLIMB", Touch.actionLabel.text);
            Assert.IsFalse(Touch.dropButton.gameObject.activeSelf);
            yield return Tap(Touch.actionButton, 1);
            yield return new WaitUntil(() => Player.Traversal.State == TraversalState.Hang);
            yield return Frames(2);
            Assert.IsTrue(Touch.dropButton.gameObject.activeSelf, "DROP while hanging");
            Assert.AreEqual("UP", Touch.actionLabel.text);

            // A drag on the right half still looks round.
            float yaw = Follow.Yaw;
            Vector2 right = new Vector2(Screen.width * 0.6f, Screen.height * 0.5f);
            BeginTouch(2, right);
            yield return null;
            for (int i = 1; i <= 10; i++)
            {
                MoveTouch(2, right + new Vector2(i * 8, 0));
                yield return null;
            }
            EndTouch(2, right + new Vector2(80, 0));
            yield return Frames(2);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(yaw, Follow.Yaw)), 5, "looking while hanging");
            Assert.AreEqual(TraversalState.Hang, Player.Traversal.State, "and still hanging");

            yield return Tap(Touch.dropButton, 3);
            Assert.AreNotEqual(TraversalState.Hang, Player.Traversal.State, "let go");
            yield return new WaitUntil(() => Player.Traversal.Grounded);
            yield return Frames(2);
            Assert.IsFalse(Touch.dropButton.gameObject.activeSelf, "DROP gone on the ground");
        }

        [UnityTest]
        public IEnumerator HighView_TouchJump_Vaults()
        {
            yield return LoadCourse();
            Follow.SetView(CameraFollow.View.High);
            yield return Before("1_Log", 0.5f);
            Assert.IsFalse(Follow.IsBehind);
            Assert.AreEqual("VAULT", Touch.actionLabel.text);
            yield return Tap(Touch.actionButton, 1);
            bool vaulted = false;
            for (float t = 0; t < 0.5f && !vaulted; t += Time.deltaTime)
            {
                vaulted |= Player.Traversal.State == TraversalState.Vault;
                yield return null;
            }
            Assert.IsTrue(vaulted);
        }

        // Browsers give a new touch the id the last one had: every tap still counts.
        [UnityTest]
        public IEnumerator RepeatedTaps_WithTheSameTouchId_EachJump()
        {
            yield return LoadCourse();
            for (int i = 0; i < 3; i++)
            {
                yield return Tap(Touch.actionButton, 4);
                Assert.IsTrue(Player.Traversal.State == TraversalState.Air, $"tap {i + 1} jumps");
                yield return new WaitUntil(() => Player.Traversal.Grounded);
                yield return Frames(2);
            }
        }

        [UnityTest]
        public IEnumerator ATouchStartingOnJump_NeverStartsTheStick()
        {
            yield return LoadCourse();
            yield return Frames(2);
            Vector2 jump = Centre(Touch.actionButton);
            BeginTouch(1, jump);
            yield return null;
            for (int i = 1; i <= 10; i++)
            {
                MoveTouch(1, jump + new Vector2(-i * 10, i * 4));
                yield return null;
                Assert.AreEqual(Vector2.zero, Touch.Stick, "no stick from JUMP");
                Assert.IsFalse(Touch.joy.gameObject.activeSelf);
            }
            EndTouch(1, jump + new Vector2(-100, 40));
            yield return Frames(2);
        }
    }
}
