using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // Loads the real Arena scene and drives it through virtual Input System devices.
    public class ArenaPlayTests : InputTestFixture
    {
        Keyboard keyboard;
        Mouse mouse;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        static IEnumerator LoadArena()
        {
            SceneManager.LoadScene("Arena");
            yield return null;
            yield return null;
        }

        static PlayerController Player => GameManager.Instance.player;

        // Point the mouse at a ground point `degrees` around the Warden (0 = east, 90 = north).
        void AimAt(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            Vector3 p = Player.transform.position + new Vector3(Mathf.Cos(r), 0, Mathf.Sin(r)) * 6;
            Set(mouse.position, (Vector2)Player.cam.WorldToScreenPoint(p));
        }

        // Hold the aim for `seconds`, re-aiming every frame as the Warden moves.
        IEnumerator HoldAim(float degrees, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                AimAt(degrees);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator MovingSouth_AimingNorth_ShowsRunUpFrames()
        {
            yield return LoadArena();
            Vector3 start = Player.transform.position;
            Press(keyboard.sKey);
            yield return HoldAim(90, 0.4f);
            Assert.Less(Player.transform.position.z, start.z - 1);
            Assert.AreEqual(PlayerController.Facing.Up, Player.CurrentFacing);
            CollectionAssert.Contains(Player.runUp, Player.body.sprite);
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
            CollectionAssert.Contains(Player.runSide, Player.body.sprite);
            Assert.IsTrue(Player.body.flipX);
            Release(keyboard.dKey);
        }

        [UnityTest]
        public IEnumerator FiringAimingWest_ShowsMirroredShoot_BoltLeavesLeftOfCentre()
        {
            yield return LoadArena();
            yield return HoldAim(180, 0.1f);
            Press(mouse.leftButton);
            yield return HoldAim(180, 0.05f);
            Assert.AreEqual(Player.shoot, Player.body.sprite);
            Assert.IsTrue(Player.body.flipX);
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
                maxBob = Mathf.Max(maxBob, Player.body.transform.localPosition.y);
            }
            Assert.AreEqual(Player.shootUp, Player.body.sprite);
            Assert.Greater(maxBob, 0.02f);
            Release(mouse.leftButton);
            Release(keyboard.sKey);
        }

        [UnityTest]
        public IEnumerator AimNear45Degrees_DoesNotFlicker()
        {
            yield return LoadArena();
            yield return HoldAim(25, 0.2f);
            Assert.AreEqual(PlayerController.Facing.Side, Player.CurrentFacing);
            int changes = 0;
            var last = Player.CurrentFacing;
            // Jitter either side of the 45-degree boundary for about a second.
            int i = 0;
            for (float t = 0; t < 1f; t += Time.deltaTime, i++)
            {
                AimAt(i % 2 == 0 ? 40 : 50);
                yield return null;
                if (Player.CurrentFacing != last) changes++;
                last = Player.CurrentFacing;
            }
            Assert.AreEqual(0, changes);
            // A clear move past the boundary still switches.
            yield return HoldAim(65, 0.2f);
            Assert.AreEqual(PlayerController.Facing.Up, Player.CurrentFacing);
        }

        [UnityTest]
        public IEnumerator Space_DashesWithDashPose_ThenCoolsDown()
        {
            yield return LoadArena();
            Vector3 start = Player.transform.position;
            PressAndRelease(keyboard.spaceKey);
            yield return null;
            Assert.AreEqual(Player.dash, Player.body.sprite);
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
            yield return new WaitUntil(() => Enemy.All.Count > 0);
            var target = Enemy.All[0];
            for (int i = 0; i < 4 && target != null && Enemy.All.Contains(target); i++)
            {
                Vector3 p = target.transform.position;
                var bolt = Object.Instantiate(Player.boltPrefab, p + new Vector3(-1, 1.2f, 0), Quaternion.identity);
                bolt.Launch(Vector3.right);
                yield return new WaitForSeconds(0.15f);
            }
            Assert.IsFalse(Enemy.All.Contains(target));
            Assert.Greater(GameManager.Instance.Score, 0);
        }

        [UnityTest]
        public IEnumerator BoltsInFlight_DoNotScoreAfterGameOver()
        {
            yield return LoadArena();
            yield return new WaitUntil(() => Enemy.All.Count > 0);
            var target = Enemy.All[0];
            Player.TryHurt(1000);
            int score = GameManager.Instance.Score;
            var bolt = Object.Instantiate(Player.boltPrefab, target.transform.position + new Vector3(-1, 1.2f, 0), Quaternion.identity);
            bolt.Launch(Vector3.right);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(bolt == null);
            Assert.AreEqual(score, GameManager.Instance.Score);
        }

        [UnityTest]
        public IEnumerator ClearingWaveOne_StartsWaveTwo()
        {
            yield return LoadArena();
            var gm = GameManager.Instance;
            float t = 0;
            while (gm.Wave == 1 && t < 15)
            {
                foreach (var e in Enemy.All.ToArray()) e.TakeHit(9999);
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(2, gm.Wave);
            Assert.AreEqual(14 * 100 + 8 * 60, gm.Score);
        }

        [UnityTest]
        public IEnumerator LethalDamage_GameOver_ThenRRestarts()
        {
            yield return LoadArena();
            var gm = GameManager.Instance;
            Assert.IsTrue(Player.TryHurt(1000));
            Assert.IsTrue(Player.IsDead);
            Assert.IsFalse(gm.IsPlaying);
            // Invulnerable right after a hit, and dead players take no more damage.
            Assert.IsFalse(Player.TryHurt(10));
            yield return null;
            Assert.AreEqual(Player.death, Player.body.sprite);

            yield return new WaitForSeconds(1.1f);
            PressAndRelease(keyboard.rKey);
            yield return null;
            yield return null;
            yield return null;
            Assert.AreNotSame(gm, GameManager.Instance);
            Assert.AreEqual(GameConfig.PlayerMaxHealth, GameManager.Instance.player.Health);
        }
    }
}
