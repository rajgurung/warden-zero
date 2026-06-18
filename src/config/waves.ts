import type { EnemyType } from './enemies';

export type WaveSpawn = { type: EnemyType; count: number };
export type Wave = { wave: number; enemies: WaveSpawn[] };

// Horde-shooter waves: big, escalating, swarm-heavy chaos.
export const WAVES: Wave[] = [
  {
    wave: 1,
    enemies: [
      { type: 'grunt', count: 14 },
      { type: 'swarmer', count: 8 },
    ],
  },
  {
    wave: 2,
    enemies: [
      { type: 'grunt', count: 18 },
      { type: 'swarmer', count: 16 },
      { type: 'runner', count: 6 },
    ],
  },
  {
    wave: 3,
    enemies: [
      { type: 'grunt', count: 22 },
      { type: 'swarmer', count: 24 },
      { type: 'runner', count: 10 },
      { type: 'brute', count: 1 },
      { type: 'skeleton', count: 6 },
    ],
  },
  {
    wave: 4,
    enemies: [
      { type: 'grunt', count: 26 },
      { type: 'swarmer', count: 30 },
      { type: 'runner', count: 14 },
      { type: 'brute', count: 2 },
      { type: 'tank', count: 2 },
      { type: 'skeleton', count: 8 },
      { type: 'spider', count: 8 },
    ],
  },
  {
    wave: 5,
    enemies: [
      { type: 'grunt', count: 30 },
      { type: 'swarmer', count: 40 },
      { type: 'runner', count: 18 },
      { type: 'brute', count: 3 },
      { type: 'tank', count: 3 },
      { type: 'skeleton', count: 10 },
      { type: 'spider', count: 12 },
      { type: 'demon', count: 3 },
    ],
  },
  {
    wave: 6,
    enemies: [
      { type: 'grunt', count: 34 },
      { type: 'swarmer', count: 48 },
      { type: 'runner', count: 22 },
      { type: 'brute', count: 4 },
      { type: 'tank', count: 4 },
      { type: 'skeleton', count: 12 },
      { type: 'spider', count: 16 },
      { type: 'demon', count: 5 },
    ],
  },
  {
    wave: 7,
    enemies: [
      { type: 'grunt', count: 38 },
      { type: 'swarmer', count: 56 },
      { type: 'runner', count: 26 },
      { type: 'brute', count: 5 },
      { type: 'tank', count: 5 },
      { type: 'skeleton', count: 14 },
      { type: 'spider', count: 20 },
      { type: 'demon', count: 7 },
    ],
  },
  {
    wave: 8,
    enemies: [
      { type: 'grunt', count: 44 },
      { type: 'swarmer', count: 70 },
      { type: 'runner', count: 30 },
      { type: 'brute', count: 7 },
      { type: 'tank', count: 7 },
      { type: 'skeleton', count: 16 },
      { type: 'spider', count: 26 },
      { type: 'demon', count: 10 },
    ],
  },
];

export const FINAL_WAVE = WAVES.length;
