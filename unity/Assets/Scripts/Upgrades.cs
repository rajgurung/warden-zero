using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WardenZero
{
    public class Upgrade
    {
        public string Id;
        public string Title;
        public string Description;
        public int MaxStacks;
        public Action<PlayerStats> Apply;
    }

    // The 16 upgrades from src/config/upgrades.ts and the offer/apply logic from
    // src/systems/UpgradeSystem.ts.
    public static class Upgrades
    {
        // JS Math.round (halves round up), so stats match Babylon exactly.
        static int Round(float v) => Mathf.FloorToInt(v + 0.5f);

        public static readonly Upgrade[] All =
        {
            // Offense
            new Upgrade { Id = "multishot", Title = "Multishot", Description = "Fire +1 extra bullet in a spread.", MaxStacks = 4, Apply = s => s.BulletCount += 1 },
            new Upgrade { Id = "damage_up", Title = "High Caliber", Description = "+20% bullet damage.", MaxStacks = 6, Apply = s => s.BulletDamage = Round(s.BulletDamage * 1.2f) },
            new Upgrade { Id = "faster_fire_rate", Title = "Rapid Fire", Description = "Shoot 15% faster.", MaxStacks = 5, Apply = s => s.FireRateMs = Round(s.FireRateMs * 0.85f) },
            new Upgrade { Id = "bigger_bullets", Title = "Bigger Bullets", Description = "Bullets are 20% larger.", MaxStacks = 3, Apply = s => s.BulletSize *= 1.2f },
            new Upgrade { Id = "bullet_speed", Title = "Hollow Point", Description = "Bullets travel 20% faster.", MaxStacks = 3, Apply = s => s.BulletSpeed = Round(s.BulletSpeed * 1.2f) },
            new Upgrade { Id = "piercing", Title = "Piercing Rounds", Description = "Bullets pass through enemies.", MaxStacks = 1, Apply = s => s.BulletPiercing = true },
            new Upgrade { Id = "crit", Title = "Critical Strikes", Description = "+10% chance to deal double damage.", MaxStacks = 5, Apply = s => s.CritChance = Mathf.Min(1, s.CritChance + 0.1f) },
            // Survival
            new Upgrade { Id = "lifesteal", Title = "Vampirism", Description = "Heal +1 HP per kill.", MaxStacks = 5, Apply = s => s.Lifesteal += 1 },
            new Upgrade { Id = "max_health", Title = "Extra Heart", Description = "+20 max health and heal.", MaxStacks = 5, Apply = s => { s.MaxHealth += 20; s.Health = Mathf.Min(s.MaxHealth, s.Health + 20); } },
            new Upgrade { Id = "regen", Title = "Regeneration", Description = "Regenerate +1 HP per second.", MaxStacks = 3, Apply = s => s.Regen += 1 },
            new Upgrade { Id = "speed_boost", Title = "Adrenaline", Description = "Move 12% faster.", MaxStacks = 4, Apply = s => s.Speed = Round(s.Speed * 1.12f) },
            new Upgrade { Id = "magnet", Title = "Gem Magnet", Description = "+50% gem pickup range.", MaxStacks = 3, Apply = s => s.MagnetRange = Round(s.MagnetRange * 1.5f) },
            // Abilities
            new Upgrade { Id = "dash_cooldown", Title = "Quick Dash", Description = "Dash cooldown -20%.", MaxStacks = 3, Apply = s => s.DashCooldownMs = Round(s.DashCooldownMs * 0.8f) },
            new Upgrade { Id = "bomb_cooldown", Title = "Bomb Training", Description = "Bomb cooldown -20%.", MaxStacks = 3, Apply = s => s.BombCooldownMs = Round(s.BombCooldownMs * 0.8f) },
            new Upgrade { Id = "bomb_radius", Title = "Bigger Blast", Description = "Bomb radius +20%.", MaxStacks = 3, Apply = s => s.BombRadius = Round(s.BombRadius * 1.2f) },
            new Upgrade { Id = "bomb_damage", Title = "Heavy Ordnance", Description = "Bomb damage +25%.", MaxStacks = 3, Apply = s => s.BombDamage = Round(s.BombDamage * 1.25f) },
        };

        public static Upgrade ById(string id) => All.FirstOrDefault(u => u.Id == id);

        public static int Stacks(RunState run, string id) => run.Upgrades.Count(x => x == id);

        // Up to three random upgrades that are not maxed out.
        public static List<Upgrade> PickThree(RunState run)
        {
            var eligible = All.Where(u => Stacks(run, u.Id) < u.MaxStacks).ToList();
            for (int i = eligible.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
            }
            return eligible.Take(3).ToList();
        }

        public static void Apply(RunState run, string id)
        {
            var u = ById(id);
            if (u == null) return;
            u.Apply(run.Stats);
            run.Upgrades.Add(id);
        }
    }
}
