using System;
using System.Linq;
using NUnit.Framework;

namespace WardenZero.Tests
{
    public class EnemyConfigTests
    {
        [Test]
        public void AllElevenTypes_MatchEnemiesTs()
        {
            Assert.AreEqual(11, Enum.GetValues(typeof(EnemyType)).Length);
            var boss = GameConfig.Enemy(EnemyType.Boss);
            Assert.AreEqual(60000, boss.MaxHealth);
            Assert.AreEqual(45, boss.ContactDamage);
            Assert.AreEqual(5000, boss.Score);
            Assert.AreEqual(70 / 30f, boss.Radius, 1e-4f);
            var spider = GameConfig.Enemy(EnemyType.Spider);
            Assert.AreEqual(22, spider.MaxHealth);
            Assert.AreEqual(175 / 30f, spider.Speed, 1e-4f);
            Assert.AreEqual(EnemyArt.Spider, spider.Art);
            var warlord = GameConfig.Enemy(EnemyType.Warlord);
            Assert.AreEqual(1400, warlord.MaxHealth);
            Assert.AreEqual(EnemyArt.Grunt, warlord.Art);
            Assert.AreEqual(EnemyArt.Runner, GameConfig.Enemy(EnemyType.Spitter).Art);
        }

        [Test]
        public void VisualScale_FollowsV1RadiusRule()
        {
            foreach (EnemyType t in Enum.GetValues(typeof(EnemyType)))
            {
                var s = GameConfig.Enemy(t);
                Assert.AreEqual(s.Radius * 30 / 16, s.VisualScale, 1e-4f, t.ToString());
                Assert.AreEqual(t, s.Type);
            }
        }

        [Test]
        public void EightWaves_MatchWavesTs()
        {
            Assert.AreEqual(8, GameConfig.Waves.Length);
            int[] totals = { 22, 40, 63, 90, 119, 145, 171, 210 };
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(totals[i], GameConfig.Waves[i].Sum(w => w.count), $"wave {i + 1}");
            Assert.IsTrue(GameConfig.Waves[7].Any(w => w.type == EnemyType.Demon && w.count == 10));
        }
    }
}
