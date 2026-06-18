import { COLORS } from './constants';

export type EnemyType =
  | 'grunt'
  | 'swarmer'
  | 'runner'
  | 'brute'
  | 'tank'
  | 'skeleton'
  | 'spider'
  | 'demon';

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
  // Small, fast, fragile zombie that swarms in big numbers — the chaos maker.
  swarmer: {
    type: 'swarmer',
    maxHealth: 16,
    speed: 205,
    contactDamage: 6,
    scoreValue: 60,
    radius: 11,
    color: 0xbfff5a,
    spriteKey: 'grunt',
    tint: 0xbfff5a,
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
  // Big, slow, beefy zombie — soaks damage, hits hard.
  brute: {
    type: 'brute',
    maxHealth: 260,
    speed: 54,
    contactDamage: 26,
    scoreValue: 320,
    radius: 30,
    color: COLORS.enemy,
    spriteKey: 'grunt',
    tint: 0x6f9a4f,
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
  // Pixel-art monsters (Kenney Tiny Dungeon, CC0) — distinct creatures.
  skeleton: {
    type: 'skeleton',
    maxHealth: 45,
    speed: 120,
    contactDamage: 10,
    scoreValue: 110,
    radius: 15,
    color: 0xe6dcc0,
    spriteKey: 'skeleton',
  },
  spider: {
    type: 'spider',
    maxHealth: 22,
    speed: 175,
    contactDamage: 7,
    scoreValue: 90,
    radius: 12,
    color: 0x9a6a44,
    spriteKey: 'spider',
  },
  demon: {
    type: 'demon',
    maxHealth: 70,
    speed: 140,
    contactDamage: 14,
    scoreValue: 160,
    radius: 16,
    color: 0xff5a5a,
    spriteKey: 'demon',
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
