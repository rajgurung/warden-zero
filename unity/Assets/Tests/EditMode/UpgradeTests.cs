using System.Linq;
using NUnit.Framework;

namespace WardenZero.Tests
{
    public class UpgradeTests
    {
        [Test]
        public void AllSixteenUpgrades_WithBabylonStackCaps()
        {
            var caps = Upgrades.All.ToDictionary(u => u.Id, u => u.MaxStacks);
            Assert.AreEqual(16, caps.Count);
            Assert.AreEqual(4, caps["multishot"]);
            Assert.AreEqual(6, caps["damage_up"]);
            Assert.AreEqual(5, caps["faster_fire_rate"]);
            Assert.AreEqual(3, caps["bigger_bullets"]);
            Assert.AreEqual(3, caps["bullet_speed"]);
            Assert.AreEqual(1, caps["piercing"]);
            Assert.AreEqual(5, caps["crit"]);
            Assert.AreEqual(5, caps["lifesteal"]);
            Assert.AreEqual(5, caps["max_health"]);
            Assert.AreEqual(3, caps["regen"]);
            Assert.AreEqual(4, caps["speed_boost"]);
            Assert.AreEqual(3, caps["magnet"]);
            Assert.AreEqual(3, caps["dash_cooldown"]);
            Assert.AreEqual(3, caps["bomb_cooldown"]);
            Assert.AreEqual(3, caps["bomb_radius"]);
            Assert.AreEqual(3, caps["bomb_damage"]);
        }

        [Test]
        public void Apply_ChangesStatsLikeBabylon()
        {
            var run = new RunState();
            foreach (var u in Upgrades.All) Upgrades.Apply(run, u.Id);
            var s = run.Stats;
            Assert.AreEqual(2, s.BulletCount);
            Assert.AreEqual(30, s.BulletDamage); // round(25 * 1.2)
            Assert.AreEqual(128, s.FireRateMs); // round(150 * 0.85)
            Assert.AreEqual(1.2f, s.BulletSize, 1e-5f);
            Assert.AreEqual(864, s.BulletSpeed);
            Assert.IsTrue(s.BulletPiercing);
            Assert.AreEqual(0.1f, s.CritChance, 1e-5f);
            Assert.AreEqual(1, s.Lifesteal);
            Assert.AreEqual(120, s.MaxHealth);
            Assert.AreEqual(120, s.Health); // +20 max and +20 heal
            Assert.AreEqual(1, s.Regen);
            Assert.AreEqual(274, s.Speed); // round(245 * 1.12)
            Assert.AreEqual(270, s.MagnetRange);
            Assert.AreEqual(1200, s.DashCooldownMs);
            Assert.AreEqual(5600, s.BombCooldownMs);
            Assert.AreEqual(180, s.BombRadius);
            Assert.AreEqual(100, s.BombDamage);
            Assert.AreEqual(16, run.Upgrades.Count);
        }

        [Test]
        public void PickThree_OffersDistinctUnmaxedUpgrades()
        {
            var run = new RunState();
            for (int i = 0; i < 50; i++)
            {
                var picks = Upgrades.PickThree(run);
                Assert.AreEqual(3, picks.Count);
                Assert.AreEqual(3, picks.Select(p => p.Id).Distinct().Count());
            }
            Upgrades.Apply(run, "piercing");
            for (int i = 0; i < 50; i++)
                Assert.IsFalse(Upgrades.PickThree(run).Any(u => u.Id == "piercing"));
        }

        [Test]
        public void PickThree_IsEmptyWhenEverythingIsMaxed()
        {
            var run = new RunState();
            foreach (var u in Upgrades.All)
                for (int i = 0; i < u.MaxStacks; i++) Upgrades.Apply(run, u.Id);
            Assert.IsEmpty(Upgrades.PickThree(run));
        }

        [Test]
        public void XpCurve_MatchesBabylon()
        {
            Assert.AreEqual(8, RunState.XpFor(1));
            Assert.AreEqual(12, RunState.XpFor(2));
            Assert.AreEqual(44, RunState.XpFor(10));
        }
    }
}
