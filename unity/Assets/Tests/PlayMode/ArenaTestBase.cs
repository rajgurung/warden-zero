using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace WardenZero.Tests
{
    // Loads the real Arena scene and drives it through virtual Input System devices.
    public abstract class ArenaTestBase : InputTestFixture
    {
        protected Keyboard keyboard;
        protected Mouse mouse;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            Time.timeScale = 1;
            base.TearDown();
        }

        protected static GameManager Gm => GameManager.Instance;
        protected static PlayerController Player => GameManager.Instance.player;
        protected static WardenSpriteView View => GameManager.Instance.player.view;
        protected static PlayerStats Stats => GameManager.Instance.Run.Stats;

        // Load the scene (it opens on the main menu).
        protected static IEnumerator LoadMenu()
        {
            SceneManager.LoadScene("Arena");
            yield return null;
            yield return null;
        }

        // Load the scene and start a run.
        protected static IEnumerator LoadArena()
        {
            yield return LoadMenu();
            Gm.StartRun();
            yield return null;
        }

        protected static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        // Point the mouse at a ground point `degrees` around the Warden (0 = east, 90 = north).
        protected void AimAt(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            Vector3 p = Player.transform.position + new Vector3(Mathf.Cos(r), 0, Mathf.Sin(r)) * 6;
            Set(mouse.position, (Vector2)Player.cam.WorldToScreenPoint(p));
        }

        // Hold the aim for `seconds`, re-aiming every frame as the Warden moves.
        protected IEnumerator HoldAim(float degrees, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                AimAt(degrees);
                yield return null;
            }
        }

        // Wait for the first enemies of the wave and return one.
        protected static IEnumerator WaitForEnemy()
        {
            yield return new WaitUntil(() => Enemy.All.Count > 0);
        }

        // Fire a bolt from just west of `target`, heading east.
        protected static Bolt ShootAt(Vector3 target, float damage, bool piercing = false)
        {
            var bolt = Object.Instantiate(Player.boltPrefab, target + new Vector3(-1, 1.2f, 0), Quaternion.identity);
            bolt.Launch(Vector3.right, 24, damage, piercing, 1);
            return bolt;
        }
    }
}
