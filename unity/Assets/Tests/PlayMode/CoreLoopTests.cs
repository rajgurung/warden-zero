using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // Phase 1 systems: menu, pause, XP and upgrades, pickups, bomb and weapon upgrades.
    public class CoreLoopTests : ArenaTestBase
    {
        [UnityTest]
        public IEnumerator OpensOnMenu_PlayStartsRun_MainMenuReturns()
        {
            yield return LoadMenu();
            Assert.AreEqual(GameManager.Mode.Menu, Gm.CurrentMode);
            Assert.IsTrue(Gm.menus.menuPanel.activeSelf);
            Assert.IsFalse(Player.gameObject.activeSelf);

            Gm.menus.playButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode);
            Assert.IsFalse(Gm.menus.menuPanel.activeSelf);
            Assert.IsTrue(Player.gameObject.activeSelf);

            PressAndRelease(keyboard.escapeKey);
            yield return null;
            Gm.menus.pauseMenuButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(GameManager.Mode.Menu, Gm.CurrentMode);
            Assert.AreEqual(1, Time.timeScale);
            Assert.AreEqual(0, Enemy.All.Count);
        }

        [UnityTest]
        public IEnumerator EscPauses_FreezesTime_PResumes()
        {
            yield return LoadArena();
            PressAndRelease(keyboard.escapeKey);
            yield return null;
            Assert.AreEqual(GameManager.Mode.Paused, Gm.CurrentMode);
            Assert.AreEqual(0, Time.timeScale);
            Assert.IsTrue(Gm.menus.pausePanel.activeSelf);
            Vector3 p = Player.transform.position;
            Press(keyboard.dKey);
            yield return Frames(5);
            Assert.AreEqual(p, Player.transform.position);
            Release(keyboard.dKey);
            PressAndRelease(keyboard.pKey);
            yield return null;
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsFalse(Gm.menus.pausePanel.activeSelf);
        }

        [UnityTest]
        public IEnumerator Kill_DropsGem_GemGivesXpAndScore()
        {
            yield return LoadArena();
            yield return WaitForEnemy();
            var e = Enemy.All[0];
            e.TakeHit(9999);
            Assert.AreEqual(1, Gem.All.Count);
            int score = Gm.Score;
            // Bring the gem to the Warden.
            Gem.All[0].transform.position = Player.transform.position + Vector3.up * 0.6f;
            yield return new WaitUntil(() => Gem.All.Count == 0);
            Assert.AreEqual(1, Gm.Run.Xp);
            Assert.AreEqual(score + GameConfig.GemScore, Gm.Score);
        }

        [UnityTest]
        public IEnumerator GemsInsideMagnetRange_FlyToTheWarden()
        {
            yield return LoadArena();
            var gem = Object.Instantiate(Gm.gemPrefab, Player.transform.position + new Vector3(4, 0.6f, 0), Quaternion.identity);
            yield return new WaitForSeconds(1.5f);
            // 180 px magnet = 6 m, so a gem 4 m away is pulled in and collected.
            Assert.IsTrue(gem == null);
            var far = Object.Instantiate(Gm.gemPrefab, Player.transform.position + new Vector3(15, 0.6f, 0), Quaternion.identity);
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(far != null);
        }

        [UnityTest]
        public IEnumerator LevelUp_PausesOnPicker_Key1AppliesUpgrade()
        {
            yield return LoadArena();
            Gm.Run.Xp = Gm.Run.XpToNext - 1;
            Object.Instantiate(Gm.gemPrefab, Player.transform.position + Vector3.up * 0.6f, Quaternion.identity);
            yield return new WaitUntil(() => Gm.CurrentMode == GameManager.Mode.Upgrade);
            Assert.AreEqual(2, Gm.Run.Level);
            Assert.AreEqual(RunState.XpFor(2), Gm.Run.XpToNext);
            Assert.AreEqual(0, Time.timeScale);
            Assert.IsTrue(Gm.menus.upgradePanel.activeSelf);
            Assert.AreEqual(3, Gm.Offered.Count);
            Assert.AreEqual(Gm.Offered[0].Title, Gm.menus.cardTitles[0].text);
            string picked = Gm.Offered[0].Id;

            Press(keyboard.digit1Key);
            yield return Frames(2);
            Release(keyboard.digit1Key);
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode);
            Assert.AreEqual(1, Time.timeScale);
            CollectionAssert.AreEqual(new[] { picked }, Gm.Run.Upgrades);
        }

        [UnityTest]
        public IEnumerator Bomb_DamagesEnemiesInRadius_ThenCoolsDown()
        {
            yield return LoadArena();
            yield return new WaitUntil(() => Enemy.All.Count >= 3);
            var near = Enemy.All[0];
            var far = Enemy.All[1];
            Vector3 p = Player.transform.position;
            near.transform.position = p + new Vector3(2, 0, 0);
            far.transform.position = p + new Vector3(-20, 0, 0);
            PressAndRelease(keyboard.eKey);
            yield return null;
            // 80 bomb damage kills grunts (50), swarmers (16) and runners (30).
            Assert.IsFalse(Enemy.All.Contains(near));
            Assert.IsTrue(Enemy.All.Contains(far));
            Assert.Less(Player.BombReady, 1);

            far.transform.position = Player.transform.position + new Vector3(2, 0, 0);
            PressAndRelease(keyboard.eKey);
            yield return null;
            Assert.IsTrue(Enemy.All.Contains(far));
        }

        [UnityTest]
        public IEnumerator Heart_Heals18_Coin_AddsCoinAndScore()
        {
            yield return LoadArena();
            Stats.Health = 50;
            Object.Instantiate(Gm.heartPrefab, Player.transform.position + Vector3.up * 0.7f, Quaternion.identity);
            yield return Frames(2);
            Assert.AreEqual(68, Player.Health);
            int score = Gm.Score;
            Object.Instantiate(Gm.coinPrefab, Player.transform.position + Vector3.up * 0.7f, Quaternion.identity);
            yield return Frames(2);
            Assert.AreEqual(1, Gm.Run.Coins);
            Assert.AreEqual(score + GameConfig.CoinValue, Gm.Score);
        }

        [UnityTest]
        public IEnumerator Pickups_ExpireAfterEightSeconds()
        {
            yield return LoadArena();
            var coin = Object.Instantiate(Gm.coinPrefab, Player.transform.position + new Vector3(10, 0.7f, 0), Quaternion.identity);
            Time.timeScale = 4;
            yield return new WaitForSeconds(GameConfig.PickupLife + 0.5f);
            Time.timeScale = 1;
            Assert.IsTrue(coin == null);
        }

        [UnityTest]
        public IEnumerator Multishot_FiresBulletCountBolts()
        {
            yield return LoadArena();
            Upgrades.Apply(Gm.Run, "multishot");
            Upgrades.Apply(Gm.Run, "multishot");
            Press(mouse.leftButton);
            yield return Frames(2);
            Release(mouse.leftButton);
            Assert.AreEqual(3, Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator FullCritChance_FiresCritBolts()
        {
            yield return LoadArena();
            Stats.CritChance = 1;
            Press(mouse.leftButton);
            yield return Frames(2);
            Release(mouse.leftButton);
            var bolts = Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None);
            Assert.IsNotEmpty(bolts);
            Assert.IsTrue(bolts.All(b => b.name.StartsWith("CritBolt")));
        }

        [UnityTest]
        public IEnumerator PiercingBolt_PassesThroughEnemies_NormalBoltStops()
        {
            yield return LoadArena();
            yield return new WaitUntil(() => Enemy.All.Count >= 4);
            // Line two enemies up north of the Warden, on their own approach path, and fire north.
            Vector3 p = Player.transform.position;
            var a = Enemy.All[0];
            var b = Enemy.All[1];
            a.transform.position = p + new Vector3(0, 0, 4);
            b.transform.position = p + new Vector3(0, 0, 6);
            Fire(p + new Vector3(0, 1.2f, 2), true);
            yield return new WaitForSeconds(0.2f);
            Assert.IsFalse(Enemy.All.Contains(a));
            Assert.IsFalse(Enemy.All.Contains(b));

            var c = Enemy.All[0];
            var d = Enemy.All[1];
            p = Player.transform.position;
            c.transform.position = p + new Vector3(0, 0, 4);
            d.transform.position = p + new Vector3(0, 0, 6);
            Fire(p + new Vector3(0, 1.2f, 2), false);
            yield return new WaitForSeconds(0.2f);
            Assert.IsFalse(Enemy.All.Contains(c));
            Assert.IsTrue(Enemy.All.Contains(d));
        }

        static void Fire(Vector3 from, bool piercing)
        {
            var bolt = Object.Instantiate(Player.boltPrefab, from, Quaternion.identity);
            bolt.Launch(Vector3.forward, 24, 9999, piercing, 1);
        }

        [UnityTest]
        public IEnumerator Lifesteal_HealsOnKill_RegenHealsEachSecond()
        {
            yield return LoadArena();
            yield return WaitForEnemy();
            Stats.Health = 50;
            Stats.Lifesteal = 5;
            Enemy.All[0].TakeHit(9999);
            Assert.AreEqual(55, Player.Health);
            Stats.Regen = 2;
            yield return new WaitForSeconds(1.2f);
            Assert.GreaterOrEqual(Player.Health, 57);
        }

        [UnityTest]
        public IEnumerator ClearingWaveOne_CollectsGems_StartsWaveTwo()
        {
            yield return LoadArena();
            float t = 0;
            while (Gm.Wave == 1 && t < 20)
            {
                foreach (var e in Enemy.All.ToArray()) e.TakeHit(9999);
                // Level-ups pause the game: take the first card.
                if (Gm.CurrentMode == GameManager.Mode.Upgrade) Gm.PickUpgrade(0);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(2, Gm.Wave);
            Assert.AreEqual(22, Gm.Run.Kills);
            // Every gem was vacuumed in before the wave could clear.
            Assert.GreaterOrEqual(Gm.Score, 14 * 100 + 8 * 60 + 22 * GameConfig.GemScore);
        }
    }
}
