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
            StageLoader.Address = StageLoader.JungleAddress;
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
            if (StageLoader.Failed) StageLoader.GiveUp();
            yield return null;
        }

        static IEnumerator WaitFor(System.Func<bool> condition, float seconds, string what, System.Func<string> state = null)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what + (state != null ? " (" + state() + ")" : ""));
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
                if (Gm.CurrentMode == GameManager.Mode.Upgrade) Gm.PickUpgrade(0); // the gunner's kills level him up
                if (ex.CurrentPhase == Extraction.Phase.Hold) Player.transform.position = Extraction.Lz + new Vector3(6, 0, -6);
                return ex.CurrentPhase != Extraction.Phase.Hold;
            }, 15, "the hold", () => $"phase {ex.CurrentPhase} left {ex.HoldLeft} inLz {ex.InLz} mode {Gm.CurrentMode} hp {Stats.Health} pos {Player.transform.position}");
            Assert.AreEqual(Extraction.Phase.Board, ex.CurrentPhase);

            Player.transform.position = ex.chopper.door.position;
            yield return Frames(2);
            Assert.AreEqual(Extraction.Phase.LiftOff, ex.CurrentPhase);
            Assert.AreEqual(GameManager.Mode.Cinematic, Gm.CurrentMode);
            var save = CampaignSave.Load();
            Assert.IsNotNull(save, "boarding saves the run");
            Assert.AreEqual(CampaignSave.AfterArena, save.Place);
            Assert.GreaterOrEqual(save.Level, 4);
            CollectionAssert.Contains(save.Upgrades, "multishot");
            int level = save.Level;

            // Lift-off, then the jungle opens on the flight with the same run.
            yield return WaitFor(() => Stage != null, 30, "the jungle scene");
            yield return Frames(2);
            Time.timeScale = 1;
            Assert.AreEqual(JungleStage.Phase.Flight, Stage.CurrentPhase);
            Assert.AreEqual(level, Run().Level);
            CollectionAssert.AreEqual(save.Upgrades, Run().Upgrades);
        }

        static RunState Run() => GameManager.Instance.Run;

        [UnityTest]
        public IEnumerator Extraction_DoorGunner_CoversTheLz()
        {
            yield return LoadMenu();
            Gm.StartCampaign();
            yield return null;
            Gm.JumpToExtraction();
            var ex = Gm.extraction;
            Time.timeScale = 6;
            yield return WaitFor(() => ex.CurrentPhase == Extraction.Phase.Hold, 10, "the landing");
            Time.timeScale = 1;
            foreach (var e in Enemy.All.ToArray()) Object.Destroy(e.gameObject);
            Enemy.All.Clear();
            Player.transform.position = Extraction.Lz + new Vector3(12, 0, -10);
            var brute = Gm.SpawnEnemyAt(EnemyType.Grunt, Extraction.Lz + new Vector3(-3, 0, -9));
            yield return WaitFor(() => brute == null || brute.IsDying, 4, "the gunner to drop the grunt");
        }

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
            Assert.AreEqual(JungleStage.Phase.GreenLight, Stage.CurrentPhase, "a beat leaning out of the door");
            Assert.IsNotNull(Player.transform.parent);
            yield return new WaitForSeconds(JungleStage.LeanOutTime + 0.1f);
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
            Assert.IsTrue(Stage.HardLanding);
            Assert.Greater(Stage.pose.crouch, 0.5f, "he buckles into a crouch");
            Assert.Greater(Stage.LandingDamage, 0);
            Assert.Greater(Stats.Health, 0);
            Assert.AreEqual(100 - Stage.LandingDamage, Stats.Health, 0.01f);
        }

        static bool XRayOn() => System.Array.Exists(View.renderers[0].sharedMaterials, m => m != null && m.shader.name.Contains("XRay"));

        [UnityTest]
        public IEnumerator SetPieces_HideTheXRay_GameplayShowsIt()
        {
            yield return LoadJungle(DropStart.Jump);
            Assert.AreEqual(GameManager.Mode.Cinematic, Gm.CurrentMode);
            Assert.IsFalse(XRayOn(), "no silhouette in the chopper door");
            Stage.JumpNow();
            yield return Frames(3);
            Assert.IsFalse(XRayOn(), "nor in the air");
            yield return LoadJungle(DropStart.Landed);
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode);
            Assert.IsTrue(XRayOn(), "back on foot");
        }

        [UnityTest]
        public IEnumerator Opening_SwingsHimUpright()
        {
            yield return LoadJungle(DropStart.Jump);
            Stage.JumpNow();
            yield return new WaitForSeconds(1);
            Assert.Greater(Stage.BodyPitch, 70, "belly to earth in freefall");
            Stage.Dive.Deploy();
            Time.timeScale = 4;
            yield return WaitFor(() => Stage.LinesTaut, 10, "line stretch");
            yield return new WaitForSeconds(4 * 4);
            Assert.Less(Mathf.Abs(Stage.BodyPitch), 6, "hanging upright under the canopy");
        }

        [UnityTest]
        public IEnumerator Chopper_FliesLevelish_NoseDownAtCruise()
        {
            yield return LoadJungle(DropStart.Flight);
            yield return new WaitForSeconds(3);
            var body = Stage.chopper.body.localEulerAngles;
            float pitch = Mathf.DeltaAngle(0, body.x), bank = Mathf.DeltaAngle(0, body.z);
            Assert.That(pitch, Is.InRange(3f, 10f), "nose down at cruise");
            Assert.LessOrEqual(Mathf.Abs(bank), 22, "no wild banking");
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
            // Not stranded: the slice's end and its way back to the menu follow.
            yield return WaitFor(() => Gm.menus.resultPanel.activeSelf, 10, "the milestone result");
            Assert.AreEqual("MILESTONE 1 COMPLETE", Gm.menus.resultEyebrow.text);
        }

        // ------------------------------------------------------------------ loading

        [UnityTest]
        public IEnumerator LoadJungle_Twice_LoadsOnce()
        {
            yield return LoadMenu();
            Campaign.Begin();
            Campaign.Run = new RunState();
            Campaign.Start = DropStart.Landed;
            StageLoader.LoadJungle();
            StageLoader.LoadJungle();
            Assert.IsTrue(StageLoader.Busy);
            yield return WaitFor(() => !StageLoader.Busy, 60, "the load");
            yield return Frames(2);
            Assert.AreEqual(1, SceneManager.sceneCount);
            Assert.IsNotNull(Stage);
            Assert.AreEqual(1, Object.FindObjectsByType<JungleStage>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator Menu_DuringTheFade_CancelsTheLoad_AndNoPause()
        {
            yield return LoadMenu();
            Gm.StartCampaign();
            yield return null;
            StageLoader.LoadJungle();
            Gm.Pause(true);
            Assert.AreNotEqual(GameManager.Mode.Paused, Gm.CurrentMode, "no pause while loading");
            Gm.EnterMenu();
            Assert.IsFalse(StageLoader.Busy);
            Assert.IsFalse(Campaign.Active);
            yield return new WaitForSeconds(2);
            Assert.IsNull(Stage, "the jungle never arrives");
            Assert.IsTrue(Gm.menus.menuPanel.activeSelf);
            Assert.Less(StageLoader.Fade, 0.1f, "faded back in");
        }

        [UnityTest]
        public IEnumerator FailedLoad_OffersRetry_ThatWorks()
        {
            LogAssert.ignoreFailingMessages = true; // Addressables logs the bad key itself
            yield return LoadMenu();
            Campaign.Active = true;
            Campaign.Run = new RunState();
            Campaign.Start = DropStart.Landed;
            StageLoader.Address = "NoSuchScene";
            StageLoader.LoadJungle();
            yield return WaitFor(() => StageLoader.Failed, 20, "the failure");
            Assert.IsTrue(StageLoader.Busy, "still blocking the menus behind");
            StageLoader.Address = StageLoader.JungleAddress;
            StageLoader.Retry();
            yield return WaitFor(() => Stage != null && !StageLoader.Busy, 60, "the retried load");
            Assert.IsFalse(StageLoader.Failed);
        }

        [UnityTest]
        public IEnumerator FailedLoad_MainMenu_GoesBack()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadMenu();
            Campaign.Active = true;
            Campaign.Run = new RunState();
            StageLoader.Address = "NoSuchScene";
            StageLoader.LoadJungle();
            yield return WaitFor(() => StageLoader.Failed, 20, "the failure");
            StageLoader.GiveUp();
            yield return Frames(3);
            Assert.IsFalse(StageLoader.Busy);
            Assert.IsFalse(Campaign.Active);
            Assert.IsTrue(Gm.menus.menuPanel.activeSelf);
        }

        [UnityTest]
        public IEnumerator Jungle_ToMenu_ToArenaAndGreenfang_ResetsTheWorld()
        {
            yield return LoadJungle(DropStart.Landed);
            Assert.IsNotNull(World.Ground);
            Gm.EnterMenu();
            yield return WaitFor(() => GameManager.Instance != null && GameManager.Instance.jungle == null && Gm.menus.menuPanel.activeSelf, 10, "the arena menu");
            Assert.IsNull(World.Ground, "flat arena floor again");
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsFalse(Campaign.Active);
            Assert.IsFalse(StageLoader.HoldingBundles, "the jungle's bundles are let go");
            Gm.menus.playButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode);
            Assert.IsFalse(Gm.extraction.Running);
            yield return LoadMenu();
            Gm.menus.greenfangButton.onClick.Invoke();
            yield return WaitFor(() => GameManager.Instance != null && GameManager.Instance.mission != null, 10, "Greenfang");
            yield return null;
            Assert.IsNull(World.Ground);
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode);
        }

        [UnityTest]
        public IEnumerator DeathDuringTheHold_Retry_StartsTheCampaignAgain()
        {
            yield return LoadMenu();
            Gm.StartCampaign();
            yield return null;
            Gm.JumpToExtraction();
            var ex = Gm.extraction;
            Time.timeScale = 6;
            yield return WaitFor(() => ex.CurrentPhase == Extraction.Phase.Hold, 10, "the landing");
            Time.timeScale = 1;
            Stats.Health = 1;
            yield return WaitFor(() => Player.IsDead || Player.TryHurt(50), 5, "a hit to land");
            yield return WaitFor(() => Gm.menus.resultPanel.activeSelf, 10, "the result");
            Gm.menus.retryButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(Campaign.Active);
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode);
            Assert.AreEqual(1, Gm.Wave);
            Assert.AreEqual(Extraction.Phase.Idle, ex.CurrentPhase);
            Assert.IsFalse(ex.chopper.gameObject.activeSelf);
            Assert.IsNull(Player.transform.parent);
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
