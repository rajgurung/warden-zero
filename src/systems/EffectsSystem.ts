import Phaser from 'phaser';
import { COLORS, DEPTH } from '../config/constants';
import { SoundSystem } from './SoundSystem';

// Centralised juice: muzzle flashes, impacts, death bursts, hit flashes,
// camera shake and SFX. Keeps gameplay code free of one-off plumbing.
export class EffectsSystem {
  private scene: Phaser.Scene;
  readonly sound: SoundSystem;

  constructor(scene: Phaser.Scene) {
    this.scene = scene;
    this.sound = new SoundSystem(scene);
  }

  muzzleFlash(x: number, y: number, angle: number): void {
    this.sound.play('shoot', 0.25);
    const flash = this.scene.add
      .image(x, y, 'glow')
      .setScale(0.5)
      .setTint(COLORS.gold)
      .setAlpha(0.9)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(DEPTH.fx);
    this.scene.tweens.add({
      targets: flash,
      scale: 0.9,
      alpha: 0,
      duration: 120,
      onComplete: () => flash.destroy(),
    });
    this.burst(x, y, COLORS.gold, 4, 90, 200, 0.4, 220);
    void angle;
  }

  bulletImpact(x: number, y: number): void {
    this.burst(x, y, COLORS.gold, 5, 60, 180, 0.4, 260);
  }

  // White tint flash + tiny knockback handled by caller.
  enemyHitFlash(sprite: Phaser.Physics.Arcade.Sprite, baseTint: number): void {
    sprite.setTintFill(0xffffff);
    this.scene.time.delayedCall(80, () => {
      if (sprite.active) sprite.setTint(baseTint);
    });
  }

  enemyDeath(x: number, y: number, color: number): void {
    this.sound.play('enemy_die', 0.4);
    const ring = this.scene.add
      .image(x, y, 'glow')
      .setScale(0.6)
      .setTint(color)
      .setAlpha(0.8)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(DEPTH.fx);
    this.scene.tweens.add({
      targets: ring,
      scale: 1.4,
      alpha: 0,
      duration: 280,
      onComplete: () => ring.destroy(),
    });
    this.burst(x, y, color, 12, 120, 320, 0.6, 420);
  }

  playerHurt(): void {
    this.sound.play('player_hurt', 0.5);
    this.scene.cameras.main.shake(160, 0.012);
    this.scene.cameras.main.flash(120, 120, 20, 30);
  }

  // Expanding shockwave + radial sparks for the bomb ability.
  bombBlast(x: number, y: number, radius: number): void {
    this.sound.play('bomb', 0.6);
    this.scene.cameras.main.shake(260, 0.02);

    const ring = this.scene.add
      .image(x, y, 'glow')
      .setTint(COLORS.accent)
      .setAlpha(0.85)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(DEPTH.fx);
    ring.setScale((radius / 64) * 0.4);
    this.scene.tweens.add({
      targets: ring,
      scale: (radius / 64) * 2.2,
      alpha: 0,
      duration: 360,
      ease: 'Cubic.easeOut',
      onComplete: () => ring.destroy(),
    });
    this.burst(x, y, COLORS.accent, 24, 220, 460, 0.7, 460);
  }

  screenShake(duration: number, intensity: number): void {
    this.scene.cameras.main.shake(duration, intensity);
  }

  // One-shot additive particle burst, auto-cleaned after its lifespan.
  private burst(
    x: number,
    y: number,
    color: number,
    count: number,
    speedMin: number,
    speedMax: number,
    scaleStart: number,
    lifespan: number,
  ): void {
    const emitter = this.scene.add.particles(x, y, 'dot', {
      tint: color,
      speed: { min: speedMin, max: speedMax },
      scale: { start: scaleStart, end: 0 },
      lifespan,
      blendMode: Phaser.BlendModes.ADD,
      emitting: false,
    });
    emitter.setDepth(DEPTH.fx);
    emitter.explode(count);
    this.scene.time.delayedCall(lifespan + 80, () => emitter.destroy());
  }
}
