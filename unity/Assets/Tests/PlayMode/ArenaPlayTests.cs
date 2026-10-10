using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // Movement, facing, firing, dash, kills and death.
    public class ArenaPlayTests : ArenaTestBase
    {
        [UnityTest]
        public IEnumerator MovingSouth_AimingNorth_ShowsRunUpFrames()
        {
            yield return LoadArena();
            Vector3 start = Player.transform.position;
            Press(keyboard.sKey);
            yield return HoldAim(90, 0.4f);
            Assert.Less(Player.transform.position.z, start.z - 1);
            Assert.AreEqual(WardenSpriteView.Facing.Up, View.CurrentFacing);
            CollectionAssert.Contains(View.runUp, View.body.sprite);
            Release(keyboard.sKey);
        }

        [UnityTest]
        public IEnumerator MovingEast_AimingWest_RunsMirroredSideFrames()
        {
            yield return LoadArena();
            Vector3 start = Player.transform.position;
            Press(keyboard.dKey);
            yield return HoldAim(180, 0.3f);
            Assert.Greater(Player.transform.position.x, start.x + 1);
            CollectionAssert.Contains(View.runSide, View.body.sprite);
            Assert.IsTrue(View.body.flipX);
            Release(keyboard.dKey);
        }

        [UnityTest]
        public IEnumerator FiringAimingWest_ShowsMirroredShoot_BoltLeavesLeftOfCentre()
        {
            yield return LoadArena();
            yield return HoldAim(180, 0.1f);
            Press(mouse.leftButton);
            yield return HoldAim(180, 0.05f);
            Assert.AreEqual(View.shoot, View.body.sprite);
            Assert.IsTrue(View.body.flipX);
            // The bolt starts at the rifle, which is drawn left of the Warden when facing west.
            var bolt = Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None).First();
            Assert.Less(bolt.transform.position.x, Player.transform.position.x - 0.4f);
            Release(mouse.leftButton);
        }

        [UnityTest]
        public IEnumerator FiringAimingNorthWhileMovingSouth_ShowsShootUpWithBob()
        {
            yield return LoadArena();
            Press(keyboard.sKey);
            Press(mouse.leftButton);
            float maxBob = 0;
            for (float t = 0; t < 0.5f; t += Time.deltaTime)
            {
                AimAt(90);
                yield return null;
                maxBob = Mathf.Max(maxBob, View.body.transform.localPosition.y);
            }
            Assert.AreEqual(View.shootUp, View.body.sprite);
            Assert.Greater(maxBob, 0.02f);
            Release(mouse.leftButton);
            Release(keyboard.sKey);
        }

        [UnityTest]
        public IEnumerator AimNear45Degrees_DoesNotFlicker()
        {
            yield return LoadArena();
            yield return HoldAim(25, 0.2f);
            Assert.AreEqual(WardenSpriteView.Facing.Side, View.CurrentFacing);
            int changes = 0;
            var last = View.CurrentFacing;
            // Jitter either side of the 45-degree boundary for about a second.
            int i = 0;
            for (float t = 0; t < 1f; t += Time.deltaTime, i++)
            {
                AimAt(i % 2 == 0 ? 40 : 50);
                yield return null;
                if (View.CurrentFacing != last) changes++;
                last = View.CurrentFacing;
            }
            Assert.AreEqual(0, changes);
            // A clear move past the boundary still switches.
            yield return HoldAim(65, 0.2f);
            Assert.AreEqual(WardenSpriteView.Facing.Up, View.CurrentFacing);
        }

        [UnityTest]
        public IEnumerator Space_DashesWithDashPose_ThenCoolsDown()
        {
            yield return LoadArena();
            Vector3 start = Player.transform.position;
            PressAndRelease(keyboard.spaceKey);
            yield return null;
            Assert.AreEqual(View.dash, View.body.sprite);
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
            int bolts = Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None).Length;
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
            yield return null;
            Assert.AreEqual(View.death, View.body.sprite);

            yield return new WaitForSeconds(GameConfig.DyingTime + 0.2f);
            Assert.AreEqual(GameManager.Mode.Over, Gm.CurrentMode);
            Assert.IsTrue(Gm.menus.resultPanel.activeSelf);
            Assert.AreEqual("RUN OVER", Gm.menus.resultEyebrow.text);

            PressAndRelease(keyboard.rKey);
            yield return Frames(3);
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode);
            Assert.IsFalse(Gm.menus.resultPanel.activeSelf);
            Assert.AreEqual(100, Player.Health);
        }
    }
}
