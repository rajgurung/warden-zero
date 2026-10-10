using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // Phase 3: the touch scheme and audio wiring.
    public class ShipReadyTests : ArenaTestBase
    {
        Touchscreen screen;

        public override void Setup()
        {
            base.Setup();
            screen = InputSystem.AddDevice<Touchscreen>();
            TouchControls.ForceTouch = true;
        }

        public override void TearDown()
        {
            TouchControls.ForceTouch = false;
            base.TearDown();
        }

        static Vector2 ScreenCentre => new Vector2(Screen.width / 2f, Screen.height / 2f);

        [UnityTest]
        public IEnumerator TouchMenu_ShowsTouchControlsAndButtons()
        {
            yield return LoadMenu();
            Assert.AreEqual("Left thumb", Gm.menus.controlKeys[0].text);
            Gm.StartRun();
            yield return null;
            Assert.IsTrue(Gm.hud.touch.dashButton.gameObject.activeSelf);
            Assert.IsTrue(Gm.hud.touch.bombButton.gameObject.activeSelf);
            Assert.IsFalse(Gm.hud.dashText.transform.parent.parent.gameObject.activeSelf);
            Assert.IsFalse(Player.reticle.gameObject.activeSelf); // no pointer on touch
        }

        [UnityTest]
        public IEnumerator DraggingAnywhere_MovesTheWarden()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            Vector3 start = Player.transform.position;
            BeginTouch(1, ScreenCentre + new Vector2(-200, 0));
            yield return null;
            MoveTouch(1, ScreenCentre + new Vector2(0, 0));
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(Gm.hud.touch.joy.gameObject.activeSelf);
            Assert.Greater(Gm.hud.touch.Stick.x, 0.9f);
            Assert.Greater(Player.transform.position.x, start.x + 1);
            EndTouch(1, ScreenCentre);
            yield return Frames(2);
            Assert.AreEqual(Vector2.zero, Gm.hud.touch.Stick);
            Assert.IsFalse(Gm.hud.touch.joy.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Touch_AutoAimsAtNearestEnemy_AndAutoFires()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            Assert.AreEqual(0, Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None).Length);
            var e = Gm.SpawnEnemy(EnemyType.Grunt);
            e.transform.position = Player.transform.position + new Vector3(-10, 0, 0);
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Player.AimDirection.x, -0.9f);
            Assert.Greater(Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None).Length, 0);
        }

        [UnityTest]
        public IEnumerator TouchButtons_DashAndBomb()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            Vector3 start = Player.transform.position;
            Gm.hud.touch.PressDash();
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(Vector3.Distance(start, Player.transform.position), 2.5f);
            Gm.hud.touch.PressBomb();
            yield return null;
            Assert.Less(Player.BombReady, 1);
        }

        [UnityTest]
        public IEnumerator TappingTheBombButton_BombsEvenForAOneFrameTap()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            var bomb = Gm.hud.touch.bombButton;
            Vector2 onButton = RectTransformUtility.WorldToScreenPoint(null, bomb.TransformPoint(bomb.rect.center));
            BeginTouch(3, onButton, queueEventOnly: true);
            EndTouch(3, onButton, queueEventOnly: true);
            yield return Frames(3);
            Assert.Less(Player.BombReady, 1);
            Assert.IsFalse(Gm.hud.touch.joy.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator TouchStartingOnAButton_DoesNotStartTheStick()
        {
            yield return LoadArena();
            var dash = Gm.hud.touch.dashButton;
            Vector2 onButton = RectTransformUtility.WorldToScreenPoint(null, dash.TransformPoint(dash.rect.center));
            BeginTouch(2, onButton);
            yield return Frames(3);
            Assert.IsFalse(Gm.hud.touch.joy.gameObject.activeSelf);
            EndTouch(2, onButton);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryBabylonSoundIsWired()
        {
            yield return LoadMenu();
            var clips = new[] { Gm.shootSound, Gm.enemyHitSound, Gm.enemyDieSound, Gm.hurtSound, Gm.pickupSound, Gm.dashSound, Gm.bombSound, Gm.upgradeSound, Gm.waveStartSound, Gm.gameOverSound };
            Assert.IsTrue(clips.All(c => c != null));
            Assert.AreEqual(10, clips.Select(c => c.name).Distinct().Count());
        }
    }
}
