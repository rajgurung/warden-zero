using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // Phase 4: Operation Greenfang.
    public class GreenfangTests : ArenaTestBase
    {
        static GreenfangMission Mission => GameManager.Instance.mission;
        static StrikeSystem Strikes => GameManager.Instance.mission.strikes;

        static IEnumerator LoadGreenfang()
        {
            SceneManager.LoadScene("Greenfang");
            yield return null;
            yield return null;
        }

        static void Invincible() => Stats.MaxHealth = Stats.Health = 1e6f;

        [UnityTest]
        public IEnumerator MainMenuButton_OpensGreenfang()
        {
            yield return LoadMenu();
            Gm.menus.greenfangButton.onClick.Invoke();
            yield return Frames(3);
            Assert.AreEqual("Greenfang", SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Mission);
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode);
        }

        [UnityTest]
        public IEnumerator Insertion_ThenPush_WithV1Setup()
        {
            yield return LoadGreenfang();
            Assert.AreEqual(GreenfangMission.Phase.Insertion, Mission.CurrentPhase);
            Assert.AreEqual(130, Stats.MaxHealth);
            Assert.Less(Vector3.Distance(Player.transform.position, GreenfangMission.PlayerStart), 0.5f);
            Assert.AreEqual(StrikeSystem.AirMaxCharges, Strikes.AirCharges);
            yield return new WaitForSeconds(2.6f);
            Assert.AreEqual(GreenfangMission.Phase.Push, Mission.CurrentPhase);
            Assert.AreEqual("SECURE BEACON ALPHA", Mission.hud.objective.text);
        }

        [UnityTest]
        public IEnumerator HoldingTheBeacon_Secures_EvenWithEnemiesOnIt_AndRearmsAir()
        {
            yield return LoadGreenfang();
            Invincible();
            Mission.Enter(GreenfangMission.Phase.Push);
            // Spend two air charges far away first.
            Vector3 far = GreenfangMission.Beacons[2];
            Assert.IsTrue(Strikes.Fire(StrikeType.Air, far, far + Vector3.left));
            Assert.AreEqual(StrikeSystem.AirMaxCharges - 1, Strikes.AirCharges);
            // Crowd the beacon: presence still captures (the e3bf116 soft-lock fix).
            Vector3 b = GreenfangMission.Beacons[0];
            for (int i = 0; i < 5; i++) Gm.SpawnEnemyAt(EnemyType.Grunt, b + new Vector3(i - 2, 0, 1));
            Player.transform.position = b;
            yield return new WaitForSeconds(GreenfangMission.CaptureTime + 0.4f);
            Assert.AreEqual(GreenfangMission.Phase.Advance, Mission.CurrentPhase);
            Assert.AreEqual(1, Mission.ObjectivesDone);
            Assert.AreEqual(StrikeSystem.AirMaxCharges, Strikes.AirCharges); // +2, capped at 4
        }

        [UnityTest]
        public IEnumerator LeavingTheRing_BleedsCaptureBack()
        {
            yield return LoadGreenfang();
            Invincible();
            Mission.Enter(GreenfangMission.Phase.Push);
            Player.transform.position = GreenfangMission.Beacons[0];
            yield return new WaitForSeconds(2f);
            float held = Mission.Capture;
            Assert.That(held, Is.InRange(1.7f, 2.3f));
            Player.transform.position = GreenfangMission.Beacons[0] + new Vector3(15, 0, 0);
            yield return new WaitForSeconds(1f);
            // Bleeds at 1.5x: ~1.5 s lost in 1 s.
            Assert.That(Mission.Capture, Is.InRange(held - 1.8f, held - 1.2f));
            Assert.IsTrue(Mission.hud.capture.activeSelf);
        }

        [UnityTest]
        public IEnumerator Warlord_HasGuardAndBar_KillingHimStartsExtraction()
        {
            yield return LoadGreenfang();
            Invincible();
            int before = Enemy.All.Count;
            Mission.Enter(GreenfangMission.Phase.Warlord);
            var w = Mission.Warlord;
            Assert.IsNotNull(w);
            Assert.AreEqual(EnemyType.Warlord, w.Stats.Type);
            Assert.AreEqual(before + 7, Enemy.All.Count);
            yield return null;
            Assert.IsTrue(Gm.hud.bossBar.activeSelf);
            Assert.AreEqual("ELIMINATE THE WARLORD", Mission.hud.objective.text);
            w.TakeHit(1e6f);
            Assert.AreEqual(GreenfangMission.Phase.Extraction, Mission.CurrentPhase);
            Assert.AreEqual(GreenfangMission.ExtractionTime, Mission.ExtractionLeft, 0.01f);
            Assert.IsFalse(Gm.hud.bossBar.activeSelf);
        }

        [UnityTest]
        public IEnumerator Warlord_TelegraphsAPound()
        {
            yield return LoadGreenfang();
            Invincible();
            Mission.Enter(GreenfangMission.Phase.Warlord);
            bool telegraphed = false;
            for (float t = 0; t < 5f; t += Time.deltaTime)
            {
                telegraphed |= Mission.poundRing.enabled;
                yield return null;
            }
            Assert.IsTrue(telegraphed);
        }

        [UnityTest]
        public IEnumerator SurvivingExtraction_CompletesTheMission()
        {
            yield return LoadGreenfang();
            Invincible();
            Mission.Enter(GreenfangMission.Phase.Extraction);
            Time.timeScale = 10;
            yield return new WaitUntil(() => Gm.CurrentMode == GameManager.Mode.Over);
            Time.timeScale = 1;
            Assert.AreEqual("MISSION COMPLETE", Gm.menus.resultEyebrow.text);
            Assert.AreEqual("OBJ", Gm.menus.resultWaveLabel.text);
            Assert.AreEqual("5/5", Gm.menus.resultWave.text);
        }

        [UnityTest]
        public IEnumerator Artillery_KillsTheCluster_AndCoolsDown()
        {
            yield return LoadGreenfang();
            Invincible();
            Vector3 target = Player.transform.position + new Vector3(12, 0, 0);
            var cluster = Enumerable.Range(0, 3).Select(i => Gm.SpawnEnemyAt(EnemyType.Grunt, target + new Vector3(0, 0, i - 1))).ToList();
            // Hold them in place for the telegraph.
            Assert.IsTrue(Strikes.Fire(StrikeType.Artillery, target, Player.transform.position));
            Assert.IsFalse(Strikes.CanFire(StrikeType.Artillery));
            for (float t = 0; t < 3f; t += Time.deltaTime)
            {
                for (int i = 0; i < cluster.Count; i++)
                    if (cluster[i] != null && !cluster[i].IsDying) cluster[i].transform.position = target + new Vector3(0, 0, i - 1);
                yield return null;
            }
            Assert.IsTrue(cluster.All(e => e == null || e.IsDying));
            Assert.GreaterOrEqual(Gm.Run.Kills, 3);
        }

        [UnityTest]
        public IEnumerator StandingInYourOwnStrike_Hurts()
        {
            yield return LoadGreenfang();
            Gm.ClearQueue();
            float before = Player.Health;
            Strikes.Fire(StrikeType.Artillery, Player.transform.position, Player.transform.position);
            yield return new WaitForSeconds(2.3f);
            Assert.LessOrEqual(Player.Health, before - StrikeSystem.SelfDamage + 4);
        }

        [UnityTest]
        public IEnumerator AirCharges_RunOut_QCyclesTheArmedStrike()
        {
            yield return LoadGreenfang();
            Invincible();
            Assert.AreEqual(StrikeType.Artillery, Strikes.Armed);
            PressAndRelease(keyboard.qKey);
            yield return Frames(2);
            Assert.AreEqual(StrikeType.Air, Strikes.Armed);
            Vector3 far = GreenfangMission.Beacons[2];
            Assert.IsTrue(Strikes.Fire(StrikeType.Air, far, far + Vector3.left));
            Assert.IsFalse(Strikes.Fire(StrikeType.Air, far, far + Vector3.left)); // cooling down
            Assert.AreEqual(StrikeSystem.AirMaxCharges - 1, Strikes.AirCharges);
        }

        [UnityTest]
        public IEnumerator Waypoint_PointsAtAnOffscreenObjective()
        {
            yield return LoadGreenfang();
            Invincible();
            Mission.Enter(GreenfangMission.Phase.Advance);
            yield return Frames(3);
            Assert.IsTrue(Mission.hud.arrow.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Trunks_BlockTheWarden()
        {
            yield return LoadGreenfang();
            Invincible();
            Assert.IsNotEmpty(World.Trunks);
            var trunk = World.Trunks[0];
            Player.transform.position = new Vector3(trunk.x - 3, 0, trunk.z);
            Press(keyboard.dKey);
            yield return new WaitForSeconds(1f);
            Release(keyboard.dKey);
            Vector3 p = Player.transform.position;
            float d = new Vector2(p.x - trunk.x, p.z - trunk.z).magnitude;
            Assert.GreaterOrEqual(d, trunk.y + GameConfig.PlayerRadius - 0.01f);
            Assert.Less(p.x, trunk.x); // stopped on the near side, didn't walk through
        }
    }
}
