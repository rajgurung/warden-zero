using NUnit.Framework;
using UnityEngine;

namespace WardenZero.Tests
{
    // The jeep's feel (VehicleAudio): the engine climbs through its gears with speed, and a
    // harder knock shakes the camera harder.
    public class VehicleFeelTests
    {
        [Test]
        public void EnginePitch_RisesWithSpeed_InEachGear_ShiftsDown()
        {
            float lo = 0;
            foreach (float hi in VehicleAudio.Gears)
            {
                float last = -1;
                for (float v = lo + 0.5f; v <= hi; v += 0.5f)
                {
                    float p = VehicleAudio.EnginePitch(v, 1);
                    Assert.Greater(p, last, $"rising at {v} m/s");
                    last = p;
                }
                lo = hi;
            }
            Assert.Less(VehicleAudio.EnginePitch(7.5f, 1), VehicleAudio.EnginePitch(6.9f, 1), "a shift drops the revs");
            Assert.Greater(VehicleAudio.EnginePitch(GameConfig.Jeep.TopSpeed, 1), VehicleAudio.EnginePitch(0, 0), "flat out is higher than the idle");
            Assert.Greater(VehicleAudio.EnginePitch(10, 1), VehicleAudio.EnginePitch(10, 0), "under load");
            Assert.Greater(VehicleAudio.EngineVolume(1, true), VehicleAudio.EngineVolume(0, true));
            Assert.AreEqual(0, VehicleAudio.EngineVolume(1, false), "quiet when nobody is in it");
        }

        [Test]
        public void ImpactStrength_MapsToShake()
        {
            Assert.AreEqual(0, VehicleAudio.ImpactShake(1), "a nudge doesn't shake");
            Assert.Greater(VehicleAudio.ImpactShake(4), 0);
            Assert.Greater(VehicleAudio.ImpactShake(8), VehicleAudio.ImpactShake(4));
            Assert.AreEqual(1, VehicleAudio.ImpactShake(30), "capped");
        }
    }
}
