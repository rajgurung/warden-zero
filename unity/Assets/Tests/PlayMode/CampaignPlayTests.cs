using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // The drop slice end to end: arena victory -> extraction -> board -> flight -> jump ->
    // land -> checkpoint A -> saved -> continue. Time runs fast (timeScale) where the flow waits.
    public class CampaignPlayTests : ArenaTestBase
    {
        string savedPrefs;

        public override void Setup()
        {
            base.Setup();
            savedPrefs = PlayerPrefs.GetString(CampaignSave.Key, null);
            CampaignSave.Clear();
        }

        public override void TearDown()
        {
            if (string.IsNullOrEmpty(savedPrefs)) PlayerPrefs.DeleteKey(CampaignSave.Key);
            else PlayerPrefs.SetString(CampaignSave.Key, savedPrefs);
            Quality.Apply(QualityTier.High);
            base.TearDown();
        }

        // A jungle still streaming in must not land in the next test's scene.
        [UnityTearDown]
        public IEnumerator FinishLoads()
        {
            Time.timeScale = 1;
            while (StageLoader.Loading) yield return null;
        }

        static IEnumerator WaitFor(System.Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        static JungleStage Stage => Object.FindFirstObjectByType<JungleStage>();

        // Load the jungle the way the game does (Addressables), at a given start.
        static IEnumerator LoadJungle(DropStart start, RunState run = null)
        {
            // From the arena, so a jungle left over from another test can't pass for this one.
            yield return LoadMenu();
            Campaign.Active = true;
            Campaign.Run = run ?? new RunState();
            Campaign.Start = start;
            StageLoader.LoadJungle();
            yield return WaitFor(() => Stage != null && GameManager.Instance != null && GameManager.Instance.jungle == Stage, 60, "the jungle scene");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Menu_HasCampaign_ContinueNeedsASave()
        {
            yield return LoadMenu();
            Assert.IsTrue(Gm.menus.campaignButton.gameObject.activeInHierarchy);
            Assert.IsFalse(Gm.menus.continueButton.interactable);
            Campaign.Save(new RunState(), CampaignSave.AfterArena);
            Gm.menus.ShowMenu();
            Assert.IsTrue(Gm.menus.continueButton.interactable);
        }

        [UnityTest]
        public IEnumerator ArenaRun_ColossusDown_EndsWithoutExtraction()
        {
            yield return LoadArena();
            Gm.JumpToBoss();
            yield return null;
            Gm.Boss.TakeHit(1e7f);
            yield return Frames(3);
            Assert.AreEqual(GameManager.Mode.Won, Gm.CurrentMode);
            Assert.IsFalse(Gm.extraction.Running);
        }

        [UnityTest]
        public IEnumerator Campaign_ColossusDown_CallsTheChopper()
        {
            yield return LoadMenu();
            Gm.menus.campaignButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(Campaign.Active);
            Gm.JumpToBoss();
            yield return null;
            Gm.Boss.TakeHit(1e7f);
            yield return Frames(3);
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode, "the run goes on");
            Assert.AreEqual(Extraction.Phase.Inbound, Gm.extraction.CurrentPhase);
            Assert.IsTrue(Gm.extraction.chopper.gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator Extraction_HoldTheLz_Board_Saves_LoadsTheFlight()
        {
            yield return LoadMenu();
            Gm.StartCampaign();
            yield return null;
            Gm.JumpToExtraction();
            Run().Level = 4;
            Upgrades.Apply(Run(), "multishot");
            var ex = Gm.extraction;
            Time.timeScale = 6;
            yield return WaitFor(() => ex.CurrentPhase == Extraction.Phase.Hold, 10, "the landing");
            Assert.Less(Vector3.Distance(ex.chopper.transform.position, Extraction.Lz), 0.1f, "landed on the pad");

            // Outside the ring the clock stops.
            Player.transform.position = Extraction.Lz + new Vector3(0, 0, -25);
            yield return Frames(10);
            Assert.AreEqual(Extraction.HoldTime, ex.HoldLeft, 1e-3f);
            // Inside it runs (enemies keep coming, the Warden can't die here).
            Stats.MaxHealth = Stats.Health = 1e6f;
            yield return WaitFor(() =>
            {
                Player.transform.position = Extraction.Lz + new Vector3(0, 0, -4);
                return ex.CurrentPhase == Extraction.Phase.Board;
            }, 15, "the hold");
            Assert.Greater(Enemy.All.Count, 0, "the horde kept coming");

            Player.transform.position = ex.chopper.door.position;
            yield return Frames(2);
            Assert.AreEqual(Extraction.Phase.LiftOff, ex.CurrentPhase);
            Assert.AreEqual(GameManager.Mode.Cinematic, Gm.CurrentMode);
            var save = CampaignSave.Load();
            Assert.IsNotNull(save, "boarding saves the run");
            Assert.AreEqual(CampaignSave.AfterArena, save.Place);
            Assert.AreEqual(4, save.Level);
            CollectionAssert.Contains(save.Upgrades, "multishot");

            // Lift-off, then the jungle opens on the flight with the same run.
            yield return WaitFor(() => Stage != null, 30, "the jungle scene");
            yield return Frames(2);
            Time.timeScale = 1;
            Assert.AreEqual(JungleStage.Phase.Flight, Stage.CurrentPhase);
            Assert.AreEqual(4, Run().Level);
            Assert.AreEqual(2, Run().Stats.BulletCount);
        }

        static RunState Run() => GameManager.Instance.Run;

        [UnityTest]
        public IEnumerator Flight_Skips_ToGreenLight_Then_Jumps()
        {
            yield return LoadJungle(DropStart.Flight);
            Assert.AreEqual(JungleStage.Phase.Flight, Stage.CurrentPhase);
            Assert.AreEqual(GameManager.Mode.Cinematic, Gm.CurrentMode);
            Press(keyboard.spaceKey);
            yield return null;
            Release(keyboard.spaceKey);
            yield return null;
            Assert.AreEqual(JungleStage.Phase.GreenLight, Stage.CurrentPhase);
            Assert.Greater(Player.transform.position.y - Stage.lz.y, 800, "at jump height");
            yield return new WaitForSeconds(0.6f);
            Press(keyboard.spaceKey);
            yield return null;
            Release(keyboard.spaceKey);
            yield return null;
            Assert.AreEqual(JungleStage.Phase.Freefall, Stage.CurrentPhase);
            Assert.IsNull(Player.transform.parent, "out of the door");
        }

        [UnityTest]
        public IEnumerator Jump_PullInTime_LandSoft_WalkToCheckpoint_Saves_Completes()
        {
            var run = new RunState { Level = 5, Score = 999 };
            Upgrades.Apply(run, "speed_boost");
            yield return LoadJungle(DropStart.Jump, run);
            Stage.JumpNow();
            Time.timeScale = 8;
            yield return WaitFor(() => Stage.Dive.Altitude < 280, 20, "the pull height");
            Press(keyboard.spaceKey);
            yield return null;
            Release(keyboard.spaceKey);
            yield return null;
            Assert.AreEqual(JungleStage.Phase.Canopy, Stage.CurrentPhase);
            Assert.IsTrue(Stage.parachute.gameObject.activeSelf);
            yield return WaitFor(() => Stage.CurrentPhase == JungleStage.Phase.Landing, 30, "touchdown");
            Assert.AreEqual(0, Stage.LandingDamage, "a timely pull lands soft");
            Assert.AreEqual(100, Stats.Health);
            var p = Player.transform.position;
            Assert.AreEqual(Stage.Ground(p), p.y, 0.05f, "on the ground");
            Assert.LessOrEqual(new Vector2(p.x - Stage.lz.x, p.z - Stage.lz.z).magnitude, Skydive.FunnelBase + 1);

            yield return WaitFor(() => Stage.CurrentPhase == JungleStage.Phase.Walk, 10, "the camera to settle");
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode);
            Assert.IsTrue(Gm.cameraFollow.enabled);

            // Walk up to the beacon and hold it.
            Player.transform.position = Stage.checkpoint;
            yield return WaitFor(() => Stage.CurrentPhase == JungleStage.Phase.Secured, 10, "the capture");
            var save = CampaignSave.Load();
            Assert.IsNotNull(save);
            Assert.AreEqual(CampaignSave.CheckpointA, save.Place);
            Assert.AreEqual(5, save.Level);
            Assert.AreEqual(999, save.Score);
            CollectionAssert.Contains(save.Upgrades, "speed_boost");
            yield return WaitFor(() => Gm.menus.resultPanel.activeSelf, 10, "the milestone result");
            Assert.AreEqual("MILESTONE 1 COMPLETE", Gm.menus.resultEyebrow.text);
            Assert.IsFalse(Gm.menus.retryButton.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Jump_NoPull_AutoDeploy_HardLanding_Survives()
        {
            yield return LoadJungle(DropStart.Jump);
            Stage.JumpNow();
            Time.timeScale = 8;
            yield return WaitFor(() => Stage.CurrentPhase == JungleStage.Phase.Landing, 40, "touchdown");
            Assert.IsTrue(Stage.Dive.AutoDeployed);
            Assert.Greater(Stage.LandingDamage, 0);
            Assert.Greater(Stats.Health, 0);
            Assert.AreEqual(100 - Stage.LandingDamage, Stats.Health, 0.01f);
        }

        [UnityTest]
        public IEnumerator Continue_ResumesAtCheckpointA_WithTheRun()
        {
            var run = new RunState { Level = 7 };
            Upgrades.Apply(run, "max_health");
            run.Stats.Health = 61;
            Campaign.Save(run, CampaignSave.CheckpointA);
            yield return LoadMenu();
            Gm.menus.ShowMenu();
            Assert.IsTrue(Gm.menus.continueButton.interactable);
            Gm.menus.continueButton.onClick.Invoke();
            yield return WaitFor(() => Stage != null && GameManager.Instance.jungle == Stage, 60, "the jungle scene");
            yield return Frames(2);
            Assert.AreEqual(JungleStage.Phase.Secured, Stage.CurrentPhase);
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode);
            Assert.AreEqual(7, Run().Level);
            Assert.AreEqual(61, Stats.Health);
            Assert.AreEqual(120, Stats.MaxHealth);
            Assert.Less(Vector3.Distance(Player.transform.position, Stage.checkpoint), 8);
        }

        [UnityTest]
        public IEnumerator Continue_AfterTheArena_ResumesAtTheFlight()
        {
            Campaign.Save(new RunState { Level = 3 }, CampaignSave.AfterArena);
            yield return LoadMenu();
            Assert.IsTrue(Campaign.Continue());
            yield return WaitFor(() => Stage != null && GameManager.Instance.jungle == Stage, 60, "the jungle scene");
            yield return null;
            Assert.AreEqual(JungleStage.Phase.Flight, Stage.CurrentPhase);
            Assert.AreEqual(3, Run().Level);
        }

        [UnityTest]
        public IEnumerator Quality_Low_ThinsTheJungle()
        {
            yield return LoadJungle(DropStart.Landed);
            var q = Object.FindFirstObjectByType<JungleQuality>();
            Quality.Apply(QualityTier.High);
            Assert.AreEqual(1, q.patch.detailObjectDensity, 1e-3f);
            Quality.Apply(QualityTier.Low);
            Assert.AreEqual("Low", QualitySettings.names[QualitySettings.GetQualityLevel()]);
            Assert.Less(q.patch.detailObjectDensity, 0.5f);
            Assert.Less(q.patch.treeDistance, 300);
        }

        [UnityTest]
        public IEnumerator Landed_WardenFollowsTheGround()
        {
            yield return LoadJungle(DropStart.Landed);
            Assert.AreEqual(JungleStage.Phase.Walk, Stage.CurrentPhase);
            Press(keyboard.wKey);
            yield return new WaitForSeconds(1.5f);
            Release(keyboard.wKey);
            var p = Player.transform.position;
            Assert.Greater(p.z, Stage.lz.z + 3, "walked north");
            Assert.AreEqual(Stage.Ground(p), p.y, 0.05f);
        }

        [UnityTest]
        public IEnumerator Touch_DeployButton_OpensTheChute()
        {
            TouchControls.ForceTouch = true;
            yield return LoadJungle(DropStart.Jump);
            Stage.JumpNow();
            Time.timeScale = 8;
            yield return WaitFor(() => Stage.Dive.Altitude < Skydive.DeployPrompt - 20, 20, "the pull prompt");
            Time.timeScale = 1;
            yield return null;
            var touch = Gm.hud.touch;
            Assert.IsTrue(touch.actionButton.gameObject.activeSelf, "DEPLOY shows on touch");
            Assert.AreEqual("DEPLOY", touch.actionLabel.text);
            touch.PressAction();
            yield return null;
            Assert.AreEqual(JungleStage.Phase.Canopy, Stage.CurrentPhase);
        }
    }
}
