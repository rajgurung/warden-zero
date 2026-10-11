using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // The jungle's context camera: enemies near switch to the high view and back once clear,
    // blending; V pins; Manual is the M1 toggle; set pieces keep their own shots.
    public class ContextCameraTests : ArenaTestBase
    {
        static JungleStage Stage => Object.FindFirstObjectByType<JungleStage>();
        static CameraFollow Follow => GameManager.Instance.cameraFollow;
        static CinemachineBrain Brain => Follow.brain;

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

        // A few grunts held still `distance` metres away.
        static void Grunts(float distance)
        {
            Vector3 p = Player.transform.position;
            for (int i = 0; i < 3; i++)
                Gm.SpawnEnemyAt(EnemyType.Grunt, p + Quaternion.Euler(0, i * 20, 0) * Vector3.forward * distance).enabled = false;
        }

        static void ClearEnemies()
        {
            foreach (var e in Enemy.All.ToArray()) Object.Destroy(e.gameObject);
            Enemy.All.Clear();
        }

        [UnityTest]
        public IEnumerator Enemies_SwitchToHigh_AndBackOnceClear_Blending()
        {
            yield return LoadJungle(DropStart.Landed);
            Stats.MaxHealth = Stats.Health = 1e6f;
            Assert.IsTrue(Follow.ContextCamera);
            Assert.IsTrue(Follow.IsBehind, "exploring: behind");
            Grunts(12);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(CameraContext.Context.Combat, Follow.Context.Current);
            Assert.AreEqual(CameraFollow.View.High, Follow.Current);
            yield return null;
            Assert.IsTrue(Brain.IsBlending, "blends to the high view");
            Assert.AreEqual(0.6f, Brain.ActiveBlend.Duration, 0.01f);
            yield return new WaitForSeconds(0.8f);
            Assert.IsFalse(Brain.IsBlending);
            Assert.IsTrue(View.XRay, "the high view's silhouette");
            Assert.IsFalse(PlayerPrefs.HasKey("wz.view.jungle"), "automatic switches aren't remembered");

            ClearEnemies();
            yield return new WaitForSeconds(CameraContext.CombatHold - 0.6f);
            Assert.AreEqual(CameraFollow.View.High, Follow.Current, "holds for a moment after");
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(CameraFollow.View.Behind, Follow.Current, "and back behind him");
        }

        [UnityTest]
        public IEnumerator V_PinsTheOtherView_WithoutRememberingIt()
        {
            yield return LoadJungle(DropStart.Landed);
            Assert.IsTrue(Follow.IsBehind);
            PressAndRelease(keyboard.vKey);
            yield return Frames(2);
            Assert.AreEqual(CameraFollow.View.High, Follow.Current);
            Assert.IsTrue(Follow.Context.Pinned);
            yield return new WaitForSeconds(3);
            Assert.AreEqual(CameraFollow.View.High, Follow.Current, "the pin holds while exploring");
            Assert.IsFalse(PlayerPrefs.HasKey("wz.view.jungle"));
        }

        // A retry (StartRun snaps the camera) starts the context afresh: no pin carried over.
        [UnityTest]
        public IEnumerator Retry_ClearsThePin()
        {
            yield return LoadJungle(DropStart.Landed);
            PressAndRelease(keyboard.vKey);
            yield return Frames(2);
            Assert.IsTrue(Follow.Context.Pinned);
            Assert.AreEqual(CameraFollow.View.High, Follow.Current);
            Gm.StartRun();
            yield return Frames(2);
            Assert.IsFalse(Follow.Context.Pinned, "the pin is gone");
            Assert.AreEqual(CameraFollow.View.Behind, Follow.Current, "back on the exploring view");
        }

        [UnityTest]
        public IEnumerator Manual_VCuts_AndEnemiesDontSwitch()
        {
            CameraFollow.AutoCamera = false;
            yield return LoadJungle(DropStart.Landed);
            Stats.MaxHealth = Stats.Health = 1e6f;
            Assert.IsTrue(Follow.IsBehind);
            Grunts(10);
            yield return new WaitForSeconds(2.5f);
            Assert.IsTrue(Follow.IsBehind, "Manual: no switch by itself");
            PressAndRelease(keyboard.vKey);
            yield return Frames(2);
            Assert.AreEqual(CameraFollow.View.High, Follow.Current);
            Assert.IsFalse(Brain.IsBlending, "a cut, as in M1");
            Assert.AreEqual((int)CameraFollow.View.High, PlayerPrefs.GetInt("wz.view.jungle"), "remembered");
        }

        [UnityTest]
        public IEnumerator SetPieces_KeepTheirShots_GameplayCamerasOff()
        {
            yield return LoadJungle(DropStart.Jump);
            Stage.JumpNow();
            yield return Frames(3);
            Assert.IsFalse(Follow.highCam.gameObject.activeSelf);
            Assert.IsFalse(Follow.shoulderCam.gameObject.activeSelf);
            Assert.AreEqual(Stage.freefallCam, (Object)Brain.ActiveVirtualCamera);
        }

        [UnityTest]
        public IEnumerator Arena_StaysManual_NoHighCam()
        {
            yield return LoadArena();
            Assert.IsNull(Follow.highCam);
            Assert.IsFalse(Follow.ContextCamera);
            Assert.IsFalse(Brain.enabled, "the arena's high view is the camera itself, as before");
        }
    }
}
