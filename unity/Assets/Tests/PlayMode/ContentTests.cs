using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace WardenZero.Tests
{
    // Phase 2: every enemy type, the Spitter, all waves and the Colossus.
    public class ContentTests : ArenaTestBase
    {
        [UnityTest]
        public IEnumerator EveryEnemyType_SpawnsWithItsArtTintAndSize()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            var spawned = Enum.GetValues(typeof(EnemyType)).Cast<EnemyType>()
                .Select(t => Gm.SpawnEnemy(t)).ToList();
            yield return new WaitForSeconds(0.4f);
            foreach (var e in spawned)
            {
                var s = e.Stats;
                Assert.AreEqual(s.VisualScale, e.transform.localScale.x, 0.02f, s.Type.ToString());
                Assert.AreEqual(s.Tint, e.body.color, s.Type.ToString());
                string tex = e.body.sprite.texture.name;
                switch (s.Art)
                {
                    case EnemyArt.Grunt: StringAssert.StartsWith("grunt_walk", tex); break;
                    case EnemyArt.Runner: StringAssert.StartsWith("runner_walk", tex); break;
                    default:
                        Assert.AreEqual(s.Art.ToString().ToLower() + "_idle", tex);
                        Assert.AreEqual(FilterMode.Point, e.body.sprite.texture.filterMode);
                        break;
                }
            }
        }

        [UnityTest]
        public IEnumerator Spitter_HoldsRange_AndSpitsAtTheWarden()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            foreach (var other in Enemy.All.ToArray()) Object.Destroy(other.gameObject);
            Enemy.All.Clear();
            var spitter = Gm.SpawnEnemy(EnemyType.Spitter);
            spitter.transform.position = Player.transform.position + new Vector3(0, 0, 8);
            bool sawSpit = false;
            for (float t = 0; t < 3.5f; t += Time.deltaTime)
            {
                sawSpit |= Object.FindObjectsByType<Spit>(FindObjectsSortMode.None).Length > 0;
                yield return null;
            }
            Assert.IsTrue(sawSpit);
            // It stood its ground inside 340 px (11.3 m) rather than closing to melee.
            Assert.Greater(Vector3.Distance(spitter.transform.position, Player.transform.position), 7);
            Assert.Less(Player.Health, 100);
        }

        [UnityTest]
        public IEnumerator Colossus_SummonsSwarmers_KillWinsTheRun()
        {
            yield return LoadArena();
            Gm.JumpToBoss();
            var boss = Gm.Boss;
            Assert.IsNotNull(boss);
            Assert.AreEqual(EnemyType.Boss, boss.Stats.Type);
            Assert.IsTrue(Gm.hud.bossBar.activeSelf);
            Assert.IsTrue(Gm.hud.BannerShowing("COLOSSUS INBOUND"));
            Assert.AreEqual(8, Gm.Wave);

            // Keep the Warden alive while the boss closes in, then count the summons.
            Stats.MaxHealth = Stats.Health = 1e6f;
            yield return new WaitForSeconds(GameConfig.BossSummonEvery + 0.3f);
            Assert.That(Enemy.All.Count(e => e.Stats.Type == EnemyType.Swarmer), Is.InRange(4, 7));

            boss.TakeHit(1e6f);
            Assert.AreEqual(GameManager.Mode.Won, Gm.CurrentMode);
            Assert.IsFalse(Gm.hud.bossBar.activeSelf);
            yield return new WaitForSeconds(GameConfig.DyingTime + 0.2f);
            Assert.AreEqual(GameManager.Mode.Over, Gm.CurrentMode);
            Assert.AreEqual("VICTORY", Gm.menus.resultEyebrow.text);
        }

        [UnityTest]
        public IEnumerator ClearingTheFinalWave_BringsTheColossus()
        {
            yield return LoadArena();
            Stats.MaxHealth = Stats.Health = 1e6f;
            Gm.JumpToWave(8);
            float t = 0;
            while (Gm.Boss == null && t < 40)
            {
                foreach (var e in Enemy.All.ToArray()) e.TakeHit(9999);
                if (Gm.CurrentMode == GameManager.Mode.Upgrade) Gm.PickUpgrade(0);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(Gm.Boss);
            Assert.AreEqual(210, Gm.Run.Kills);
        }

        [UnityTest]
        public IEnumerator Wave3_QueuesItsSixtyThreeEnemies()
        {
            yield return LoadArena();
            Gm.JumpToWave(3);
            Assert.AreEqual(3, Gm.Wave);
            Assert.AreEqual(63, Gm.QueuedSpawns + Enemy.All.Count);
            yield return null;
        }
    }
}
