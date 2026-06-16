import { COLORS } from './constants';

export type EnemyType = 'grunt' | 'runner' | 'tank';

export type EnemyConfig = {
  type: EnemyType;
  maxHealth: number;
  speed: number;
  contactDamage: number;
  scoreValue: number;
  radius: number;
  color: number; // particle/glow colour on hit + death
  spriteKey: string; // animation/texture prefix: `${spriteKey}_idle` + `${spriteKey}_walk`
  tint?: number; // optional sprite tint (e.g. corrupted tank)
};

// Speeds scaled for the 1280x720 arena. Distinct sprites/sizes keep
// silhouettes readable: zombie grunt, robot runner, big purple zombie tank.
export const ENEMY_CONFIGS: Record<EnemyType, EnemyConfig> = {
  grunt: {
    type: 'grunt',
    maxHealth: 50,
    speed: 105,
    contactDamage: 10,
    scoreValue: 100,
    radius: 16,
    color: COLORS.enemy,
    spriteKey: 'grunt',
  },
  runner: {
    type: 'runner',
    maxHealth: 30,
    speed: 185, // real threat, still < player so it's dodgeable
    contactDamage: 8,
    scoreValue: 120,
    radius: 13,
    color: COLORS.gold,
    spriteKey: 'runner',
  },
  tank: {
    type: 'tank',
    maxHealth: 160,
    speed: 66,
    contactDamage: 20,
    scoreValue: 250,
    radius: 24,
    color: COLORS.enemyAccent,
    spriteKey: 'grunt',
    tint: 0xb15cff,
  },
};

// Predefined spawn points around the arena edges (1280x720).
export const SPAWN_POINTS = [
  { x: 100, y: 100 },
  { x: 640, y: 90 },
  { x: 1180, y: 100 },
  { x: 100, y: 360 },
  { x: 1180, y: 360 },
  { x: 100, y: 620 },
  { x: 640, y: 630 },
  { x: 1180, y: 620 },
];

export const MIN_SPAWN_DISTANCE_FROM_PLAYER = 220;
