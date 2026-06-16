import Phaser from 'phaser';
import type { BulletData } from '../types/game';
import { GAME_WIDTH, GAME_HEIGHT, DEPTH } from '../config/constants';

export class Bullet extends Phaser.Physics.Arcade.Sprite {
  damage = 0;
  piercing = false;
  private spawnTime = 0;
  private lifetimeMs = 900;

  constructor(scene: Phaser.Scene, x: number, y: number) {
    super(scene, x, y, 'bullet');
    scene.add.existing(this);
    scene.physics.add.existing(this);
    this.deactivate();
  }

  fire(x: number, y: number, angle: number, data: BulletData, size: number): void {
    this.enableBody(true, x, y, true, true);
    this.setActive(true);
    this.setVisible(true);
    this.setRotation(angle);
    this.setScale(size);
    this.setDepth(DEPTH.bullet);
    this.damage = data.damage;
    this.piercing = data.piercing;
    this.lifetimeMs = data.lifetimeMs;
    this.spawnTime = this.scene.time.now;
    this.scene.physics.velocityFromRotation(
      angle,
      data.speed,
      (this.body as Phaser.Physics.Arcade.Body).velocity,
    );
  }

  deactivate(): void {
    this.disableBody(true, true);
    this.setActive(false);
    this.setVisible(false);
  }

  preUpdate(time: number, delta: number): void {
    super.preUpdate(time, delta);
    if (!this.active) return;
    if (time - this.spawnTime >= this.lifetimeMs) {
      this.deactivate();
      return;
    }
    if (
      this.x < -20 ||
      this.x > GAME_WIDTH + 20 ||
      this.y < -20 ||
      this.y > GAME_HEIGHT + 20
    ) {
      this.deactivate();
    }
  }
}
