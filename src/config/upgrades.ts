import type { PlayerStats } from '../types/game';

export type UpgradeId =
  | 'double_shot'
  | 'faster_fire_rate'
  | 'bigger_bullets'
  | 'speed_boost'
  | 'max_health'
  | 'dash_cooldown'
  | 'bomb_cooldown';

export type Upgrade = {
  id: UpgradeId;
  title: string;
  description: string;
  maxStacks: number;
  apply: (stats: PlayerStats) => void;
};

export const UPGRADES: Upgrade[] = [
  {
    id: 'double_shot',
    title: 'Double Shot',
    description: 'Fire two bullets in a slight spread.',
    maxStacks: 1,
    apply: (s) => {
      s.bulletCount = 2;
    },
  },
  {
    id: 'faster_fire_rate',
    title: 'Faster Fire Rate',
    description: 'Shoot 20% faster.',
    maxStacks: 4,
    apply: (s) => {
      s.fireRateMs = Math.round(s.fireRateMs * 0.8);
    },
  },
  {
    id: 'bigger_bullets',
    title: 'Bigger Bullets',
    description: 'Bullets are 25% larger.',
    maxStacks: 2,
    apply: (s) => {
      s.bulletSize *= 1.25;
    },
  },
  {
    id: 'speed_boost',
    title: 'Speed Boost',
    description: 'Move 15% faster.',
    maxStacks: 3,
    apply: (s) => {
      s.speed = Math.round(s.speed * 1.15);
    },
  },
  {
    id: 'max_health',
    title: 'Extra Heart',
    description: 'Increase max health by 20 and heal.',
    maxStacks: 3,
    apply: (s) => {
      s.maxHealth += 20;
      s.health = Math.min(s.maxHealth, s.health + 20);
    },
  },
  {
    id: 'dash_cooldown',
    title: 'Quick Dash',
    description: 'Dash cooldown reduced by 25%.',
    maxStacks: 3,
    apply: (s) => {
      s.dashCooldownMs = Math.round(s.dashCooldownMs * 0.75);
    },
  },
  {
    id: 'bomb_cooldown',
    title: 'Bomb Training',
    description: 'Bomb cooldown reduced by 25%.',
    maxStacks: 3,
    apply: (s) => {
      s.bombCooldownMs = Math.round(s.bombCooldownMs * 0.75);
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
