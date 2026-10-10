using System.Collections.Generic;

namespace WardenZero
{
    // Mutable per-run player stats (src/types/game.ts, defaults from src/config/playerStats.ts).
    // Kept in the original pixel units; convert with GameConfig.PX where they meet the world.
    public class PlayerStats
    {
        public float MaxHealth = 100;
        public float Health = 100;
        public float Speed = 245;
        public float FireRateMs = 150;
        public float BulletDamage = 25;
        public float BulletSpeed = 720;
        public float BulletSize = 1;
        public bool BulletPiercing;
        public int BulletCount = 1;
        public float CritChance;
        public float CritMult = 2;
        public float Lifesteal; // HP per kill
        public float Regen; // HP per second
        public float MagnetRange = 180;
        public float DashSpeed = 700;
        public float DashDurationMs = 150;
        public float DashCooldownMs = 1500;
        public float BombDamage = 80;
        public float BombRadius = 150;
        public float BombCooldownMs = 7000;
    }

    // One run's progress (score, level, picked upgrades). A fresh one per run.
    public class RunState
    {
        public int Wave = 1;
        public int Score;
        public int Coins;
        public int Kills;
        public int Level = 1;
        public int Xp;
        public int XpToNext = XpFor(1);
        public float Seconds;
        public readonly List<string> Upgrades = new List<string>();
        public readonly PlayerStats Stats = new PlayerStats();

        // Gems needed to go from `level` to the next (Game.levelUp).
        public static int XpFor(int level) => 8 + (level - 1) * 4;
    }
}
