import Phaser from 'phaser';
import { Enemy } from '../entities/Enemy';
import {
  ENEMY_CONFIGS,
  SPAWN_POINTS,
  MIN_SPAWN_DISTANCE_FROM_PLAYER,
  type EnemyType,
} from '../config/enemies';
import type { EffectsSystem } from './EffectsSystem';

const MAX_ENEMIES = 30;

// Owns the enemy group and spawns enemies at edge points away from the player.
export class EnemySpawnSystem {
  readonly group: Phaser.Physics.Arcade.Group;
  private scene: Phaser.Scene;
  private effects: EffectsSystem;

  constructor(scene: Phaser.Scene, effects: EffectsSystem) {
    this.scene = scene;
    this.effects = effects;
    this.group = scene.physics.add.group();
  }

  get count(): number {
    return this.group.countActive(true);
  }

  spawn(type: EnemyType, playerX: number, playerY: number): Enemy | null {
    if (this.count >= MAX_ENEMIES) return null;
    const point = this.pickSpawnPoint(playerX, playerY);
    const enemy = new Enemy(
      this.scene,
      point.x,
      point.y,
      ENEMY_CONFIGS[type],
      this.effects,
    );
    this.group.add(enemy);
    return enemy;
  }

  // Each frame, steer every living enemy toward the player.
  chaseAll(playerX: number, playerY: number): void {
    for (const obj of this.group.getChildren()) {
      const enemy = obj as Enemy;
      if (enemy.active) enemy.chase(playerX, playerY);
    }
  }

  private pickSpawnPoint(
    playerX: number,
    playerY: number,
  ): { x: number; y: number } {
    const valid = SPAWN_POINTS.filter(
      (p) =>
        Phaser.Math.Distance.Between(p.x, p.y, playerX, playerY) >=
        MIN_SPAWN_DISTANCE_FROM_PLAYER,
    );
    const pool = valid.length > 0 ? valid : SPAWN_POINTS;
    return Phaser.Utils.Array.GetRandom(pool);
  }
}
