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

        // The remembered camera view per stage: every test starts from the defaults, and the
        // editor's own choice comes back afterwards.
        static readonly string[] ViewKeys = { "wz.view.arena", "wz.view.jungle" };
        readonly int?[] savedViews = new int?[ViewKeys.Length];

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            for (int i = 0; i < ViewKeys.Length; i++)
            {
                savedViews[i] = PlayerPrefs.HasKey(ViewKeys[i]) ? PlayerPrefs.GetInt(ViewKeys[i]) : (int?)null;
                PlayerPrefs.DeleteKey(ViewKeys[i]);
            }
        }

        public override void TearDown()
        {
            Time.timeScale = 1;
            Campaign.End();
            TouchControls.ForceTouch = false;
            Cursor.lockState = CursorLockMode.None;
            for (int i = 0; i < ViewKeys.Length; i++)
            {
                if (savedViews[i].HasValue) PlayerPrefs.SetInt(ViewKeys[i], savedViews[i].Value);
                else PlayerPrefs.DeleteKey(ViewKeys[i]);
            }
            base.TearDown();
        }

        protected static GameManager Gm => GameManager.Instance;
        protected static PlayerController Player => GameManager.Instance.player;
        protected static WardenModelView View => (WardenModelView)GameManager.Instance.player.view;
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

        // Point the mouse at a spot `degrees` around the Warden (0 = east, 90 = north), 6 m out
        // on the aim plane (rifle height).
        protected void AimAt(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            Vector3 p = Player.transform.position + new Vector3(Mathf.Cos(r), 0, Mathf.Sin(r)) * 6 + Vector3.up * GameConfig.AimHeight;
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
