using UnityEngine;

namespace WardenZero
{
    // The jeep's rear heavy machine gun, as plain numbers (M3): where it points relative to the
    // hull, whether it may fire, and its heat. The camera is the aim authority and the gun
    // follows it exactly (M3 review cut: no slew, no barrel marker).
    // - Yaw all the way round the pedestal; pitch from MinPitch to MaxPitch.
    // - A no-fire cone ahead (NoFireYaw either side, below NoFirePitch): it never shoots the cab.
    // - FireRate rounds a second while the trigger is held. Each round adds HeatPerRound; it
    //   cools CoolRate a second. At 1 it overheats and locks until it is back to Unlock, cooling
    //   faster meanwhile (a short penalty, not a dead stop). Bursts of 2 s with 1.5 s pauses
    //   never overheat; holding the trigger does after about 5 s.
    public class Turret
    {
        public const float MinPitch = -8, MaxPitch = 35;
        public const float NoFireYaw = 25, NoFirePitch = 4;
        public const float FireRate = 8; // rounds a second (480 a minute)
        public const float HeatPerRound = 0.07f, CoolRate = 0.35f, LockedCoolRate = 0.5f, Unlock = 0.35f;
        public const float Damage = 25; // a round
        public const float Range = 160;

        public float Yaw { get; private set; } // degrees from the hull's nose, -180..180
        public float Pitch { get; private set; } // degrees, up positive
        public float Heat { get; private set; }
        public bool Overheated { get; private set; }

        float cooldown; // seconds to the next round

        // Point at a world heading and elevation from a hull facing hullYaw.
        public void Aim(float worldYaw, float pitch, float hullYaw)
        {
            Yaw = Mathf.DeltaAngle(hullYaw, worldYaw);
            Pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
        }

        // Would a round from here hit the jeep's own cab?
        public bool InNoFireCone => Mathf.Abs(Yaw) < NoFireYaw && Pitch < NoFirePitch;

        public bool CanFire => !Overheated && !InNoFireCone;

        // The trigger for dt seconds; returns the rounds fired.
        public int Tick(bool trigger, float dt)
        {
            int rounds = 0;
            cooldown -= dt;
            if (trigger && CanFire)
            {
                while (cooldown <= 0 && !Overheated)
                {
                    rounds++;
                    cooldown += 1 / FireRate;
                    Heat += HeatPerRound;
                    if (Heat >= 1)
                    {
                        Heat = 1;
                        Overheated = true;
                    }
                }
            }
            if (cooldown < 0) cooldown = 0;
            Heat = Mathf.Max(0, Heat - (Overheated ? LockedCoolRate : CoolRate) * dt);
            if (Overheated && Heat <= Unlock) Overheated = false;
            return rounds;
        }

        public void Reset()
        {
            Heat = 0;
            Overheated = false;
            cooldown = 0;
        }
    }
}
