import Phaser from 'phaser';
import { WAVES } from '../config/waves';
import type { EnemyType } from '../config/enemies';
import type { EnemySpawnSystem } from './EnemySpawnSystem';

const SPAWN_INTERVAL_MS = 400;

// Drives spawning for a single wave: builds a shuffled spawn queue and drips
// enemies into the arena. A wave is "cleared" once the queue is empty and no
// enemies remain alive (the latter is checked by the scene).
export class WaveSystem {
  private scene: Phaser.Scene;
  private spawner: EnemySpawnSystem;
  private queue: EnemyType[] = [];
  private timer?: Phaser.Time.TimerEvent;
  private getPlayerPos: () => { x: number; y: number };

  constructor(
    scene: Phaser.Scene,
    spawner: EnemySpawnSystem,
    getPlayerPos: () => { x: number; y: number },
  ) {
    this.scene = scene;
    this.spawner = spawner;
    this.getPlayerPos = getPlayerPos;
  }

  startWave(waveNumber: number): void {
    const wave = WAVES[waveNumber - 1];
    this.queue = [];
    if (wave) {
      for (const group of wave.enemies) {
        for (let i = 0; i < group.count; i++) this.queue.push(group.type);
      }
      Phaser.Utils.Array.Shuffle(this.queue);
    }

    this.timer?.remove();
    this.timer = this.scene.time.addEvent({
      delay: SPAWN_INTERVAL_MS,
      loop: true,
      callback: this.spawnNext,
      callbackScope: this,
    });
  }

  get doneSpawning(): boolean {
    return this.queue.length === 0;
  }

  stop(): void {
    this.timer?.remove();
    this.timer = undefined;
    this.queue = [];
  }

  private spawnNext(): void {
    if (this.queue.length === 0) {
      this.timer?.remove();
      this.timer = undefined;
      return;
    }
    const pos = this.getPlayerPos();
    const type = this.queue[0];
    const spawned = this.spawner.spawn(type, pos.x, pos.y);
    // Only consume the entry once it actually spawned (cap may be hit).
    if (spawned) this.queue.shift();
  }
}
