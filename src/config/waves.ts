import type { EnemyType } from './enemies';

export type WaveSpawn = { type: EnemyType; count: number };
export type Wave = { wave: number; enemies: WaveSpawn[] };

export const WAVES: Wave[] = [
  { wave: 1, enemies: [{ type: 'grunt', count: 4 }] },
  {
    wave: 2,
    enemies: [
      { type: 'grunt', count: 6 },
      { type: 'runner', count: 2 },
    ],
  },
  {
    wave: 3,
    enemies: [
      { type: 'grunt', count: 8 },
      { type: 'runner', count: 3 },
    ],
  },
  {
    wave: 4,
    enemies: [
      { type: 'grunt', count: 6 },
      { type: 'runner', count: 4 },
      { type: 'tank', count: 1 },
    ],
  },
  {
    wave: 5,
    enemies: [
      { type: 'grunt', count: 8 },
      { type: 'runner', count: 4 },
      { type: 'tank', count: 2 },
    ],
  },
];

export const FINAL_WAVE = WAVES.length;
