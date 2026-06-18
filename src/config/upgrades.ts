import type { PlayerStats } from '../types/game';

export type UpgradeId =
  | 'multishot'
  | 'damage_up'
  | 'faster_fire_rate'
  | 'bigger_bullets'
  | 'bullet_speed'
  | 'piercing'
  | 'crit'
  | 'lifesteal'
  | 'speed_boost'
  | 'max_health'
  | 'regen'
  | 'magnet'
  | 'dash_cooldown'
  | 'bomb_cooldown'
  | 'bomb_radius'
  | 'bomb_damage';

export type Upgrade = {
  id: UpgradeId;
  title: string;
  description: string;
  maxStacks: number;
  apply: (stats: PlayerStats) => void;
};

export const UPGRADES: Upgrade[] = [
  // --- Offense ---
  {
    id: 'multishot',
    title: 'Multishot',
    description: 'Fire +1 extra bullet in a spread.',
    maxStacks: 4,
    apply: (s) => {
      s.bulletCount += 1;
    },
  },
  {
    id: 'damage_up',
    title: 'High Caliber',
    description: '+20% bullet damage.',
    maxStacks: 6,
    apply: (s) => {
      s.bulletDamage = Math.round(s.bulletDamage * 1.2);
    },
  },
  {
    id: 'faster_fire_rate',
    title: 'Rapid Fire',
    description: 'Shoot 15% faster.',
    maxStacks: 5,
    apply: (s) => {
      s.fireRateMs = Math.round(s.fireRateMs * 0.85);
    },
  },
  {
    id: 'bigger_bullets',
    title: 'Bigger Bullets',
    description: 'Bullets are 20% larger.',
    maxStacks: 3,
    apply: (s) => {
      s.bulletSize *= 1.2;
    },
  },
  {
    id: 'bullet_speed',
    title: 'Hollow Point',
    description: 'Bullets travel 20% faster.',
    maxStacks: 3,
    apply: (s) => {
      s.bulletSpeed = Math.round(s.bulletSpeed * 1.2);
    },
  },
  {
    id: 'piercing',
    title: 'Piercing Rounds',
    description: 'Bullets pass through enemies.',
    maxStacks: 1,
    apply: (s) => {
      s.bulletPiercing = true;
    },
  },
  {
    id: 'crit',
    title: 'Critical Strikes',
    description: '+10% chance to deal double damage.',
    maxStacks: 5,
    apply: (s) => {
      s.critChance = Math.min(1, s.critChance + 0.1);
    },
  },
  // --- Survival ---
  {
    id: 'lifesteal',
    title: 'Vampirism',
    description: 'Heal +1 HP per kill.',
    maxStacks: 5,
    apply: (s) => {
      s.lifesteal += 1;
    },
  },
  {
    id: 'max_health',
    title: 'Extra Heart',
    description: '+20 max health and heal.',
    maxStacks: 5,
    apply: (s) => {
      s.maxHealth += 20;
      s.health = Math.min(s.maxHealth, s.health + 20);
    },
  },
  {
    id: 'regen',
    title: 'Regeneration',
    description: 'Regenerate +1 HP per second.',
    maxStacks: 3,
    apply: (s) => {
      s.regen += 1;
    },
  },
  {
    id: 'speed_boost',
    title: 'Adrenaline',
    description: 'Move 12% faster.',
    maxStacks: 4,
    apply: (s) => {
      s.speed = Math.round(s.speed * 1.12);
    },
  },
  {
    id: 'magnet',
    title: 'Gem Magnet',
    description: '+50% gem pickup range.',
    maxStacks: 3,
    apply: (s) => {
      s.magnetRange = Math.round(s.magnetRange * 1.5);
    },
  },
  // --- Abilities ---
  {
    id: 'dash_cooldown',
    title: 'Quick Dash',
    description: 'Dash cooldown -20%.',
    maxStacks: 3,
    apply: (s) => {
      s.dashCooldownMs = Math.round(s.dashCooldownMs * 0.8);
    },
  },
  {
    id: 'bomb_cooldown',
    title: 'Bomb Training',
    description: 'Bomb cooldown -20%.',
    maxStacks: 3,
    apply: (s) => {
      s.bombCooldownMs = Math.round(s.bombCooldownMs * 0.8);
    },
  },
  {
    id: 'bomb_radius',
    title: 'Bigger Blast',
    description: 'Bomb radius +20%.',
    maxStacks: 3,
    apply: (s) => {
      s.bombRadius = Math.round(s.bombRadius * 1.2);
    },
  },
  {
    id: 'bomb_damage',
    title: 'Heavy Ordnance',
    description: 'Bomb damage +25%.',
    maxStacks: 3,
    apply: (s) => {
      s.bombDamage = Math.round(s.bombDamage * 1.25);
    },
  },
];

export const UPGRADES_BY_ID: Record<UpgradeId, Upgrade> = UPGRADES.reduce(
  (acc, u) => {
    acc[u.id] = u;
    return acc;
  },
  {} as Record<UpgradeId, Upgrade>,
);
