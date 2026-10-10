using NUnit.Framework;
using UnityEngine;

namespace WardenZero.Tests
{
    public class CampaignTests
    {
        string savedPrefs;

        [SetUp]
        public void BackUp()
        {
            savedPrefs = PlayerPrefs.GetString(CampaignSave.Key, null);
        }

        [TearDown]
        public void Restore()
        {
            if (string.IsNullOrEmpty(savedPrefs)) PlayerPrefs.DeleteKey(CampaignSave.Key);
            else PlayerPrefs.SetString(CampaignSave.Key, savedPrefs);
        }

        [Test]
        public void Quality_PhonesGetLow_DesktopsHigh_ChoiceWins()
        {
            Assert.AreEqual(QualityTier.Low, Quality.Pick(true, -1));
            Assert.AreEqual(QualityTier.High, Quality.Pick(false, -1));
            Assert.AreEqual(QualityTier.High, Quality.Pick(true, (int)QualityTier.High));
            Assert.AreEqual(QualityTier.Low, Quality.Pick(false, (int)QualityTier.Low));
            Assert.AreEqual(QualityTier.High, Quality.Pick(false, 7)); // junk in prefs: guess again
        }

        [Test]
        public void Quality_LevelsAreNamedLowAndHigh()
        {
            CollectionAssert.Contains(QualitySettings.names, "Low");
            CollectionAssert.Contains(QualitySettings.names, "High");
        }

        [Test]
        public void Save_RoundTripsRun()
        {
            var run = new RunState { Level = 6, Xp = 3, Score = 12345, Coins = 7, Kills = 321, Wave = 8, Seconds = 640.5f };
            Upgrades.Apply(run, "multishot");
            Upgrades.Apply(run, "multishot");
            Upgrades.Apply(run, "max_health");
            Upgrades.Apply(run, "piercing");
            run.Stats.Health = 57;

            Campaign.Save(run, CampaignSave.CheckpointA);
            var save = CampaignSave.Load();
            Assert.IsNotNull(save);
            Assert.AreEqual(CampaignSave.CheckpointA, save.Place);
            var back = save.ToRun();
            Assert.AreEqual(6, back.Level);
            Assert.AreEqual(RunState.XpFor(6), back.XpToNext);
            Assert.AreEqual(3, back.Xp);
            Assert.AreEqual(12345, back.Score);
            Assert.AreEqual(7, back.Coins);
            Assert.AreEqual(321, back.Kills);
            Assert.AreEqual(8, back.Wave);
            Assert.AreEqual(640.5f, back.Seconds, 1e-3f);
            CollectionAssert.AreEqual(run.Upgrades, back.Upgrades);
            // Stats come back from the upgrades, health from the save.
            Assert.AreEqual(3, back.Stats.BulletCount);
            Assert.IsTrue(back.Stats.BulletPiercing);
            Assert.AreEqual(120, back.Stats.MaxHealth);
            Assert.AreEqual(57, back.Stats.Health);
        }

        [Test]
        public void Save_MissingOrBroken_IsNull()
        {
            CampaignSave.Clear();
            Assert.IsNull(CampaignSave.Load());
            Assert.IsFalse(CampaignSave.Exists);
            PlayerPrefs.SetString(CampaignSave.Key, "{not json");
            Assert.IsNull(CampaignSave.Load());
            PlayerPrefs.SetString(CampaignSave.Key, "{\"Version\":99,\"Place\":\"flight\"}");
            Assert.IsNull(CampaignSave.Load());
        }

        [Test]
        public void DebugUrl_PicksTheDropStart()
        {
            Assert.AreEqual("jump", Campaign.DebugStart("https://x.dev/?campaign=jump"));
            Assert.AreEqual("extraction", Campaign.DebugStart("https://x.dev/?wave=3&campaign=Extraction"));
            Assert.IsNull(Campaign.DebugStart("https://x.dev/?boss"));
            Assert.AreEqual(DropStart.Flight, Campaign.DropFor("flight"));
            Assert.AreEqual(DropStart.Jump, Campaign.DropFor("jump"));
            Assert.AreEqual(DropStart.Landed, Campaign.DropFor("jungle"));
            Assert.AreEqual(DropStart.CheckpointA, Campaign.DropFor("checkpoint"));
            Assert.IsNull(Campaign.DropFor("extraction"));
        }

        // ------------------------------------------------------------------ the jump

        static Skydive Jump(float altitude, Vector3 offset = default)
        {
            return new Skydive { Position = offset + Vector3.up * altitude, Target = Vector3.zero, Ground = _ => 0 };
        }

        // Fall hands-off, pulling at `pullAt` (or never); one flare at `flareAt` (or none).
        static Skydive Fall(float pullAt, float flareAt = -1, float altitude = 900)
        {
            var d = Jump(altitude);
            bool tried = false;
            for (int i = 0; i < 20000 && d.Current != Skydive.State.Landed; i++)
            {
                if (d.Current == Skydive.State.Freefall && d.Altitude <= pullAt) d.Deploy();
                // One press of the flare key when passing `flareAt`.
                if (!tried && flareAt > 0 && d.Altitude <= flareAt)
                {
                    tried = true;
                    d.Flare();
                }
                d.Step(Vector2.zero, 1 / 60f);
            }
            Assert.AreEqual(Skydive.State.Landed, d.Current);
            return d;
        }

        [Test]
        public void Freefall_ReachesTerminalSpeed()
        {
            var d = Jump(3000);
            for (int i = 0; i < 60 * 20; i++) d.Step(Vector2.zero, 1 / 60f);
            Assert.AreEqual(Skydive.State.Freefall, d.Current);
            Assert.AreEqual(Skydive.Terminal, -d.Velocity.y, 1.5f);
        }

        [Test]
        public void PullHigh_SoftLanding_NoDamage()
        {
            var d = Fall(300);
            Assert.IsFalse(d.AutoDeployed);
            Assert.AreEqual(300, d.DeployAltitude, 2);
            Assert.Less(d.TouchdownSpeed, Skydive.SafeTouchdown);
            Assert.AreEqual(0, Skydive.LandingDamage(d.TouchdownSpeed));
        }

        [Test]
        public void PullAtTheSafeHeight_StillNoDamage()
        {
            var d = Fall(Skydive.SafeDeploy);
            Assert.AreEqual(0, Skydive.LandingDamage(d.TouchdownSpeed));
        }

        [Test]
        public void NoPull_AutoDeploys_HardLanding()
        {
            var d = Fall(-1);
            Assert.IsTrue(d.AutoDeployed);
            Assert.AreEqual(Skydive.AutoDeployAltitude, d.DeployAltitude, 2);
            float damage = Skydive.LandingDamage(d.TouchdownSpeed);
            Assert.Greater(damage, 10, "a missed pull hurts");
            Assert.Less(damage, 60, "but it is survivable");
        }

        [Test]
        public void LowerPull_HurtsMore()
        {
            float at110 = Skydive.LandingDamage(Fall(110).TouchdownSpeed);
            float auto = Skydive.LandingDamage(Fall(-1).TouchdownSpeed);
            Assert.Greater(auto, at110);
        }

        [Test]
        public void Flare_SoftensTheTouchdown()
        {
            var plain = Fall(250);
            var flared = Fall(250, 6);
            Assert.IsTrue(flared.Flared);
            Assert.Less(flared.TouchdownSpeed, plain.TouchdownSpeed * 0.6f);
        }

        [Test]
        public void Flare_TooHigh_DoesNothing()
        {
            var d = Fall(250, 200);
            Assert.IsFalse(d.Flared);
        }

        [Test]
        public void Funnel_KeepsTheLandingInThePatch()
        {
            // Jump far out and steer away the whole way down.
            var d = Jump(900, new Vector3(600, 0, 0));
            d.Heading = 90;
            for (int i = 0; i < 20000 && d.Current != Skydive.State.Landed; i++)
            {
                if (d.Altitude < 250) d.Deploy();
                d.Step(new Vector2(0, 1), 1 / 60f);
            }
            var flat = new Vector3(d.Position.x, 0, d.Position.z);
            Assert.LessOrEqual(flat.magnitude, Skydive.FunnelBase + 0.5f);
        }

        [Test]
        public void Steering_TurnsTheHeading()
        {
            var d = Jump(900);
            for (int i = 0; i < 60; i++) d.Step(new Vector2(1, 0), 1 / 60f);
            Assert.AreEqual(Skydive.FreefallTurn, d.Heading, 2);
        }
    }
}
