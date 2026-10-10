using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // Aim, the 3D Warden's turning and animation, firing, dash, kills and death.
    public class ArenaPlayTests : ArenaTestBase
    {
        static Animator Anim => View.animator;

        static Vector3 AimTarget(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return Player.transform.position + new Vector3(Mathf.Cos(r), 0, Mathf.Sin(r)) * 6 + Vector3.up * GameConfig.AimHeight;
        }

        // Horizontal angle between a bolt's flight and the line from it to the target.
        static float AimError(Bolt b, Vector3 target)
        {
            Vector3 to = target - b.transform.position;
            to.y = 0;
            Vector3 f = b.transform.forward;
            f.y = 0;
            return Vector3.Angle(f, to);
        }

        static Bolt[] Bolts => Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None);

        [UnityTest]
        public IEnumerator BoltsFlyAtTheCursor_AllTheWayAround()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            for (int a = 0; a < 360; a += 45)
            {
                yield return HoldAim(a, 0.3f);
                Vector3 target = AimTarget(a);
                Press(mouse.leftButton);
                yield return HoldAim(a, 0.02f);
                Release(mouse.leftButton);
                var bolt = Bolts.OrderBy(b => b.GetInstanceID()).Last();
                Assert.Less(AimError(bolt, target), 2f, $"aim {a} deg");
                Assert.AreEqual(GameConfig.AimHeight, bolt.transform.position.y, 0.01f);
                foreach (var b in Bolts) Object.Destroy(b.gameObject);
                yield return new WaitForSeconds(0.2f);
            }
        }

        [UnityTest]
        public IEnumerator SuddenFlip_FirstBoltStillGoesToTheCursor()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            yield return HoldAim(0, 0.4f);
            // Flip behind and fire at once, while the body is still turning.
            AimAt(180);
            Press(mouse.leftButton);
            yield return Frames(2);
            Release(mouse.leftButton);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(View.Yaw, -90)), 5, "body should still be turning");
            Assert.Less(AimError(Bolts.First(), AimTarget(180)), 2f);
        }

        [UnityTest]
        public IEnumerator BodyTurnsSmoothly_ConvergesWithinPointOneFiveSeconds()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            // World 0 deg (east) is yaw 90; 180 deg (west) is yaw 270.
            yield return HoldAim(0, 0.4f);
            Assert.AreEqual(0, Mathf.DeltaAngle(View.Yaw, 90), 2);
            AimAt(180);
            yield return null;
            yield return null;
            float midway = Mathf.Abs(Mathf.DeltaAngle(View.Yaw, 270));
            Assert.Greater(midway, 2, "no snap: still turning after the first frames");
            float t = 0;
            while (t < 0.15f)
            {
                AimAt(180);
                t += Time.deltaTime;
                yield return null;
            }
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(View.Yaw, 270)), 6);
        }

        [UnityTest]
        public IEnumerator AnimatedBody_FacesWhereHeAims()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            foreach (float a in new[] { 0f, 90f, 200f })
            {
                yield return HoldAim(a, 0.4f);
                Vector3 l = Anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
                Vector3 r = Anim.GetBoneTransform(HumanBodyBones.RightUpperLeg).position;
                Vector3 bodyForward = Vector3.Cross(r - l, Vector3.up);
                bodyForward.y = 0;
                Assert.Greater(Vector3.Dot(bodyForward.normalized, View.yaw.forward), 0.8f, $"aim {a}");
            }
        }

        [UnityTest]
        public IEnumerator Animator_IdleRunAimHitDeath()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(Anim.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
            Assert.Less(Anim.GetFloat("Speed"), 0.05f);
            Assert.IsTrue(Anim.GetCurrentAnimatorStateInfo(WardenModelView.UpperLayer).IsName("Aim"));

            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(Anim.GetFloat("Speed"), 0.9f);
            Release(keyboard.dKey);

            Press(mouse.leftButton);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(1, Anim.GetFloat("UpperSpeed"), 0.01f);
            Release(mouse.leftButton);
            yield return new WaitForSeconds(0.4f);
            Assert.Less(Anim.GetFloat("UpperSpeed"), 0.5f);

            Player.TryHurt(5);
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(Anim.GetCurrentAnimatorStateInfo(WardenModelView.UpperLayer).IsName("Hit")
                || Anim.GetNextAnimatorStateInfo(WardenModelView.UpperLayer).IsName("Hit"));

            Stats.Health = 1;
            yield return new WaitForSeconds(GameConfig.HurtInvuln);
            Player.TryHurt(100);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(View.Dead);
            Assert.IsTrue(Anim.GetCurrentAnimatorStateInfo(0).IsName("Death"));
        }

        [UnityTest]
        public IEnumerator Rifle_StaysInTheRightHand_PointingWhereHeFaces()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            var hand = Anim.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.AreSame(hand, View.rifle.parent);
            Press(keyboard.wKey);
            for (float t = 0; t < 1.5f; t += Time.deltaTime)
            {
                AimAt(t * 240); // sweep the aim while running
                yield return null;
                Assert.Less(Vector3.Distance(View.rifle.position, hand.position), 0.15f);
                Assert.Greater(Vector3.Dot(View.rifle.forward, View.yaw.forward), 0.99f);
                Assert.AreEqual(0.99f * View.yaw.lossyScale.x * View.animator.transform.localScale.x, Vector3.Distance(View.muzzle.position, View.rifle.position), 0.07f);
            }
            Debug.Log($"[W3D] muzzle height {View.muzzle.position.y:F3}");
            Release(keyboard.wKey);
        }

        [UnityTest]
        public IEnumerator RunClip_DoesNotClimbOrDrift()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            Stats.MaxHealth = Stats.Health = 1e6f;
            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.3f);
            float lowMin = 99, lowMax = -99, hipsOff = 0;
            var lf = Anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rf = Anim.GetBoneTransform(HumanBodyBones.RightFoot);
            var hips = Anim.GetBoneTransform(HumanBodyBones.Hips);
            for (float t = 0; t < 2.5f; t += Time.deltaTime) // two run loops and more
            {
                // Back and forth so the arena wall doesn't stop him.
                if (Player.transform.position.x > 20) { Release(keyboard.dKey); Press(keyboard.aKey); }
                yield return null;
                float low = Mathf.Min(lf.position.y, rf.position.y);
                lowMin = Mathf.Min(lowMin, low);
                lowMax = Mathf.Max(lowMax, low);
                Vector3 off = hips.position - Player.transform.position;
                off.y = 0;
                hipsOff = Mathf.Max(hipsOff, off.magnitude);
            }
            Release(keyboard.dKey);
            Release(keyboard.aKey);
            Debug.Log($"[W3D] run lowest foot {lowMin:F3}..{lowMax:F3}, hips offset max {hipsOff:F3}");
            // Limits in the model's own metres (it is scaled up in the scene).
            float scale = View.animator.transform.lossyScale.y;
            Assert.Greater(lowMin, -0.1f * scale);
            Assert.Less(lowMax, 0.3f * scale, "the run clip climbs");
            Assert.Less(hipsOff, 0.4f * scale, "the run clip drifts away from the Warden");
        }

        [UnityTest]
        public IEnumerator Space_DashesThenCoolsDown()
        {
            yield return LoadArena();
            Vector3 start = Player.transform.position;
            PressAndRelease(keyboard.spaceKey);
            yield return new WaitForSeconds(0.3f);
            // 700 px/s for 0.15 s = 105 px = 3.5 m.
            float moved = Vector3.Distance(start, Player.transform.position);
            Assert.That(moved, Is.InRange(2.5f, 4.5f));
            Assert.Less(Player.DashReady, 1f);

            // A second dash during cooldown does nothing.
            start = Player.transform.position;
            PressAndRelease(keyboard.spaceKey);
            yield return new WaitForSeconds(0.2f);
            Assert.Less(Vector3.Distance(start, Player.transform.position), 0.01f);
        }

        [UnityTest]
        public IEnumerator HoldingLeftMouse_FiresBoltsAtFireRate()
        {
            yield return LoadArena();
            Press(mouse.leftButton);
            yield return new WaitForSeconds(0.8f);
            int bolts = Bolts.Length;
            Release(mouse.leftButton);
            // 150 ms interval -> ~6 shots in 0.8 s.
            Assert.That(bolts, Is.InRange(4, 7));
        }

        [UnityTest]
        public IEnumerator Bolts_KillEnemies_AndScore()
        {
            yield return LoadArena();
            yield return WaitForEnemy();
            var target = Enemy.All[0];
            for (int i = 0; i < 4 && target != null && Enemy.All.Contains(target); i++)
            {
                ShootAt(target.transform.position, 25);
                yield return new WaitForSeconds(0.15f);
            }
            Assert.IsFalse(Enemy.All.Contains(target));
            Assert.Greater(Gm.Score, 0);
            Assert.AreEqual(1, Gm.Run.Kills);
        }

        [UnityTest]
        public IEnumerator BoltsInFlight_DoNotScoreAfterGameOver()
        {
            yield return LoadArena();
            yield return WaitForEnemy();
            var target = Enemy.All[0];
            Player.TryHurt(1000);
            int score = Gm.Score;
            var bolt = ShootAt(target.transform.position, 9999);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(bolt == null);
            Assert.AreEqual(score, Gm.Score);
        }

        [UnityTest]
        public IEnumerator LethalDamage_ShowsResult_ThenRRestarts()
        {
            yield return LoadArena();
            Assert.IsTrue(Player.TryHurt(1000));
            Assert.IsTrue(Player.IsDead);
            Assert.AreEqual(GameManager.Mode.Dying, Gm.CurrentMode);
            // Dead players take no more damage.
            Assert.IsFalse(Player.TryHurt(10));

            yield return new WaitForSeconds(GameConfig.DyingTime + 0.2f);
            Assert.AreEqual(GameManager.Mode.Over, Gm.CurrentMode);
            Assert.IsTrue(View.Dead);
            Assert.IsTrue(Gm.menus.resultPanel.activeSelf);
            Assert.AreEqual("RUN OVER", Gm.menus.resultEyebrow.text);

            PressAndRelease(keyboard.rKey);
            yield return Frames(3);
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode);
            Assert.IsFalse(Gm.menus.resultPanel.activeSelf);
            Assert.AreEqual(100, Player.Health);
            Assert.IsFalse(View.Dead);
        }
    }
}
