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

        [UnityTest]
        public IEnumerator HoldingD_MovesRight_WithUnflippedSideFrames()
        {
            yield return LoadArena();
            Vector3 start = Player.transform.position;
            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.5f);
            // z is not checked: the run clips the corner of the wall at (4, -4.67) and slides a little.
            Assert.Greater(Player.transform.position.x - start.x, 1.5f);
            CollectionAssert.Contains(Player.runSide, Player.body.sprite);
            Assert.IsFalse(Player.body.flipX);
            Release(keyboard.dKey);
        }

        [UnityTest]
        public IEnumerator HoldingA_MirrorsSideFrames()
        {
            yield return LoadArena();
            Press(keyboard.aKey);
            yield return new WaitForSeconds(0.3f);
            CollectionAssert.Contains(Player.runSide, Player.body.sprite);
            Assert.IsTrue(Player.body.flipX);
            Release(keyboard.aKey);
        }

        [UnityTest]
        public IEnumerator HoldingW_UsesRunUpFrames()
        {
            yield return LoadArena();
            Press(keyboard.wKey);
            yield return new WaitForSeconds(0.3f);
            CollectionAssert.Contains(Player.runUp, Player.body.sprite);
            Release(keyboard.wKey);
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
