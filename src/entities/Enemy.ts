import Phaser from 'phaser';
import type { EnemyConfig } from '../config/enemies';
import type { EffectsSystem } from '../systems/EffectsSystem';
import { makeShadow, syncShadow } from '../utils/shadow';

// On-screen height ≈ radius * this factor (sprite drawn larger than the
// hitbox). Tuned for the 256px-tall Toon Character poses.
const VISUAL_FACTOR = 5;

export class Enemy extends Phaser.Physics.Arcade.Sprite {
  readonly config: EnemyConfig;
  health: number;
  private effects: EffectsSystem;
  private shadow: Phaser.GameObjects.Image;
  private baseScale: number;
  private lastContactAt = 0;

  constructor(
    scene: Phaser.Scene,
    x: number,
    y: number,
    config: EnemyConfig,
    effects: EffectsSystem,
  ) {
    super(scene, x, y, `${config.spriteKey}_idle`);
    this.config = config;
    this.health = config.maxHealth;
    this.effects = effects;

    scene.add.existing(this);
    scene.physics.add.existing(this);

    if (config.tint !== undefined) this.setTint(config.tint);

    this.baseScale = (config.radius * VISUAL_FACTOR) / this.height;
    const bodyR = config.radius / this.baseScale;
    (this.body as Phaser.Physics.Arcade.Body).setCircle(
      bodyR,
      this.width / 2 - bodyR,
      this.height / 2 - bodyR,
    );
    this.setCollideWorldBounds(true);

    // Size at full scale first so the shadow is measured correctly.
    this.setScale(this.baseScale);
    this.shadow = makeShadow(scene, this);
    this.anims.play(`${config.spriteKey}_walk`, true);

    // Spawn pop-in.
    this.setScale(0);
    scene.tweens.add({
      targets: this,
      scale: this.baseScale,
      duration: 180,
      ease: 'Back.easeOut',
    });
  }

  // Steer toward the target each frame; face movement, sort by depth.
  chase(targetX: number, targetY: number): void {
    const body = this.body as Phaser.Physics.Arcade.Body;
    const angle = Phaser.Math.Angle.Between(this.x, this.y, targetX, targetY);
    this.scene.physics.velocityFromRotation(angle, this.config.speed, body.velocity);
    if (Math.abs(body.velocity.x) > 1) this.setFlipX(body.velocity.x < 0);
    this.setDepth(this.y);
    syncShadow(this.shadow, this);
  }

  takeDamage(amount: number): boolean {
    this.health -= amount;
    this.effects.sound.play('enemy_hit', 0.2);

    this.setTintFill(0xffffff);
    this.scene.time.delayedCall(80, () => {
      if (!this.active) return;
      if (this.config.tint !== undefined) this.setTint(this.config.tint);
      else this.clearTint();
    });

    this.scene.tweens.add({
      targets: this,
      scale: this.baseScale * 1.12,
      duration: 50,
      yoyo: true,
    });
    return this.health <= 0;
  }

  canDealContactDamage(time: number): boolean {
    if (time - this.lastContactAt < 500) return false;
    this.lastContactAt = time;
    return true;
  }

  die(): void {
    this.effects.enemyDeath(this.x, this.y, this.config.color);
    this.destroy();
  }
}
