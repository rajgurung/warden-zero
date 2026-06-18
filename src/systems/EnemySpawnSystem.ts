import Phaser from 'phaser';
import { Enemy } from '../entities/Enemy';
import { ENEMY_CONFIGS, type EnemyType } from '../config/enemies';
import { WORLD_WIDTH, WORLD_HEIGHT } from '../config/constants';
import type { EffectsSystem } from './EffectsSystem';

const MAX_ENEMIES = 80;
// Spawn ring: just beyond the camera view (~half-view = 640px) so enemies
// appear off-screen around the player, clamped to the world bounds.
const SPAWN_MIN = 720;
const SPAWN_MAX = 920;

// Owns the enemy group and spawns enemies off-screen around the player.
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
    const angle = Phaser.Math.FloatBetween(0, Math.PI * 2);
    const dist = Phaser.Math.Between(SPAWN_MIN, SPAWN_MAX);
    return {
      x: Phaser.Math.Clamp(playerX + Math.cos(angle) * dist, 40, WORLD_WIDTH - 40),
      y: Phaser.Math.Clamp(playerY + Math.sin(angle) * dist, 40, WORLD_HEIGHT - 40),
    };
  }
}
