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
    // Gun is intentionally the loudest thing in the mix — gain pushed past
    // unity (Web Audio allows >1) for a hard, in-your-face bang.
    this.sound.play('shoot', 1.8, Phaser.Math.Between(-160, 160));
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
    this.sound.play('enemy_die', 1);
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
  // Big dramatic detonation: white-hot core, expanding fireball, staggered
  // shockwave rings, fire debris, lingering embers, smoke and a camera flash.
  bombBlast(x: number, y: number, radius: number): void {
    const cam = this.scene.cameras.main;
    this.sound.play('bomb', 1.4);
    cam.shake(460, 0.04);
    cam.flash(170, 255, 180, 90); // warm muzzle-of-god flash

    // White-hot core flash.
    const core = this.scene.add
      .image(x, y, 'glow')
      .setTint(0xffffff)
      .setAlpha(1)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(DEPTH.fx)
      .setScale((radius / 64) * 0.3);
    this.scene.tweens.add({
      targets: core,
      scale: (radius / 64) * 1.5,
      alpha: 0,
      duration: 220,
      ease: 'Quad.easeOut',
      onComplete: () => core.destroy(),
    });

    // Fireball body (gold, fading out as it grows).
    const fire = this.scene.add
      .image(x, y, 'glow')
      .setTint(COLORS.gold)
      .setAlpha(0.95)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(DEPTH.fx)
      .setScale((radius / 64) * 0.5);
    this.scene.tweens.add({
      targets: fire,
      scale: (radius / 64) * 2.4,
      alpha: 0,
      duration: 480,
      ease: 'Cubic.easeOut',
      onComplete: () => fire.destroy(),
    });

    // Staggered shockwave rings — white, then cyan, then orange.
    this.shockwave(x, y, radius, 0xffffff, 0, 2.6, 340);
    this.shockwave(x, y, radius, COLORS.accent, 80, 3.0, 460);
    this.shockwave(x, y, radius, COLORS.enemy, 150, 2.3, 420);

    // Fire debris + slow lingering embers.
    this.burst(x, y, COLORS.gold, 32, 280, 600, 0.85, 540);
    this.burst(x, y, COLORS.enemy, 24, 180, 440, 0.95, 580);
    this.burst(x, y, 0xffffff, 12, 90, 220, 0.5, 760);

    // Smoke puff that drifts and dissipates.
    const smoke = this.scene.add.particles(x, y, 'dot', {
      tint: 0x2a3042,
      speed: { min: 30, max: 130 },
      scale: { start: 1.3, end: 2.6 },
      alpha: { start: 0.35, end: 0 },
      lifespan: 900,
      emitting: false,
    });
    smoke.setDepth(DEPTH.fx - 1);
    smoke.explode(16);
    this.scene.time.delayedCall(1000, () => smoke.destroy());
  }

  // One expanding, fading shockwave ring (optionally delayed for layering).
  private shockwave(
    x: number,
    y: number,
    radius: number,
    color: number,
    delay: number,
    endScale: number,
    duration: number,
  ): void {
    this.scene.time.delayedCall(delay, () => {
      const ring = this.scene.add
        .image(x, y, 'ring')
        .setTint(color)
        .setAlpha(0.9)
        .setBlendMode(Phaser.BlendModes.ADD)
        .setDepth(DEPTH.fx)
        .setScale((radius / 64) * 0.2);
      this.scene.tweens.add({
        targets: ring,
        scale: (radius / 64) * endScale,
        alpha: 0,
        duration,
        ease: 'Cubic.easeOut',
        onComplete: () => ring.destroy(),
      });
    });
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
