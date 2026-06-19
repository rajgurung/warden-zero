import Phaser from 'phaser';
import type { PlayerStats } from '../types/game';
import { COLORS } from '../config/constants';

type MoveKeys = {
  W: Phaser.Input.Keyboard.Key;
  A: Phaser.Input.Keyboard.Key;
  S: Phaser.Input.Keyboard.Key;
  D: Phaser.Input.Keyboard.Key;
};

// Hero uses 384x512 transparent frames (2x master art) for crispness at
// high-DPI. SCALE keeps it sized among the horde; the circular hitbox is
// scale-independent (r = BODY_RADIUS / SCALE), so tuning SCALE never changes
// collision feel.
const SCALE = 0.25;
const BODY_RADIUS = 18; // effective collision radius (world px)
const MUZZLE_OFFSET = 30; // bullet spawn distance from player centre

export class Player extends Phaser.Physics.Arcade.Sprite {
  readonly stats: PlayerStats;
  private keys: MoveKeys;
  private dashKey: Phaser.Input.Keyboard.Key;
  private invulnUntil = 0;
  private aimAngle = 0;
  private blinkTween?: Phaser.Tweens.Tween;
  private dashing = false;
  private dashReadyAt = 0;
  private bombReadyAt = 0;

  constructor(scene: Phaser.Scene, x: number, y: number, stats: PlayerStats) {
    super(scene, x, y, 'idle');
    this.stats = stats;

    scene.add.existing(this);
    scene.physics.add.existing(this);

    this.setScale(SCALE);
    this.setCollideWorldBounds(true);
    this.setDamping(true);
    this.setDrag(0.0001);

    // Centred circular hitbox (radius in unscaled texture px; scales with sprite).
    const r = BODY_RADIUS / SCALE;
    (this.body as Phaser.Physics.Arcade.Body).setCircle(
      r,
      this.width / 2 - r,
      this.height / 2 - r,
    );

    this.keys = scene.input.keyboard!.addKeys('W,A,S,D') as MoveKeys;
    this.dashKey = scene.input.keyboard!.addKey('SPACE');

    this.play('hero-idle');
  }

  get isInvulnerable(): boolean {
    return this.scene.time.now < this.invulnUntil;
  }

  get aim(): number {
    return this.aimAngle;
  }

  // Point in front of the hero where bullets/muzzle flash originate.
  get muzzleX(): number {
    return this.x + Math.cos(this.aimAngle) * MUZZLE_OFFSET;
  }

  get muzzleY(): number {
    return this.y + Math.sin(this.aimAngle) * MUZZLE_OFFSET;
  }

  // Current normalized movement direction (zero vector if idle).
  get moveDir(): Phaser.Math.Vector2 {
    const v = new Phaser.Math.Vector2(0, 0);
    if (this.keys.A.isDown) v.x -= 1;
    if (this.keys.D.isDown) v.x += 1;
    if (this.keys.W.isDown) v.y -= 1;
    if (this.keys.S.isDown) v.y += 1;
    return v.normalize();
  }

  // GameScene calls this each frame. (WASD/dash keys are owned by the Player,
  // so only the pointer is needed for aiming.)
  update(pointer: Phaser.Input.Pointer): void {
    this.aimAngle = Phaser.Math.Angle.Between(
      this.x,
      this.y,
      pointer.worldX,
      pointer.worldY,
    );

    if (Phaser.Input.Keyboard.JustDown(this.dashKey)) this.tryDash();

    const dir = this.moveDir;
    if (!this.dashing) {
      this.setVelocity(dir.x * this.stats.speed, dir.y * this.stats.speed);
    }

    const firing = pointer.isDown;
    const body = this.body as Phaser.Physics.Arcade.Body;
    const vx = body.velocity.x;
    const vy = body.velocity.y;
    const moving = dir.lengthSq() > 0;

    // Pick the animation: dash > directional run > stationary fire > idle.
    // Keeping run above shoot means legs keep cycling while you run-and-gun.
    let key: string;
    if (this.dashing) key = 'hero-dash';
    else if (moving) {
      if (Math.abs(vy) >= Math.abs(vx)) key = vy >= 0 ? 'hero-run-down' : 'hero-run-up';
      else key = 'hero-run-side';
    } else if (firing) {
      // Stationary fire: pick the pose by aim angle (up / down / side).
      const a = this.aimAngle;
      if (a < -Math.PI / 4 && a > (-3 * Math.PI) / 4) key = 'hero-shoot-up';
      else if (a > Math.PI / 4 && a < (3 * Math.PI) / 4) key = 'hero-shoot-down';
      else key = 'hero-shoot';
    } else key = 'hero-idle';

    // Facing: side-run/dash/side-shoot flip by direction; up/down/idle forward.
    if (key === 'hero-run-side' || key === 'hero-dash') this.setFlipX(vx < 0);
    else if (key === 'hero-shoot') this.setFlipX(pointer.worldX < this.x);
    else this.setFlipX(false);

    this.play(key, true);
    this.applyJuice(moving, firing);
    this.setDepth(this.y);
  }

  // Procedural motion so the single static poses feel alive: idle breathing,
  // a walk bounce + sway, a shoot tension shimmer, a dash stretch.
  private applyJuice(moving: boolean, firing: boolean): void {
    const t = this.scene.time.now;
    let sx = SCALE;
    let sy = SCALE;

    if (this.dashing) {
      sx = SCALE * 1.12; // stretch into the dash
      sy = SCALE * 0.9;
    } else if (moving) {
      // Real run cycle does the work; keep just a faint bob.
      sy = SCALE * (1 + 0.015 * Math.sin(t / 80));
    } else if (firing) {
      sy = SCALE * (1 - 0.02 * Math.abs(Math.sin(t / 45))); // recoil shimmer
    } else {
      sy = SCALE * (1 + 0.025 * Math.sin(t / 500)); // idle breathing
    }

    this.setScale(sx, sy);
    this.setAngle(0);
  }

  private tryDash(): void {
    const now = this.scene.time.now;
    if (now < this.dashReadyAt || this.dashing) return;

    let dir = this.moveDir;
    if (dir.lengthSq() === 0) {
      dir = new Phaser.Math.Vector2(
        Math.cos(this.aimAngle),
        Math.sin(this.aimAngle),
      );
    }
    this.dashing = true;
    this.invulnUntil = now + this.stats.dashDurationMs;
    this.dashReadyAt = now + this.stats.dashCooldownMs;
    this.setVelocity(dir.x * this.stats.dashSpeed, dir.y * this.stats.dashSpeed);
    if (this.scene.cache.audio.exists('dash')) {
      this.scene.sound.play('dash', { volume: 0.4 });
    }

    // Brief tint flash to signal the dash (no ghost copies — those read as a
    // second character).
    this.setTint(0x9fe8ff);
    this.scene.time.delayedCall(this.stats.dashDurationMs, () => {
      this.dashing = false;
      this.clearTint();
    });
  }

  canBomb(): boolean {
    return this.scene.time.now >= this.bombReadyAt;
  }

  markBombUsed(): void {
    this.bombReadyAt = this.scene.time.now + this.stats.bombCooldownMs;
  }

  dashReady(): number {
    return this.cooldownProgress(this.dashReadyAt, this.stats.dashCooldownMs);
  }

  bombReady(): number {
    return this.cooldownProgress(this.bombReadyAt, this.stats.bombCooldownMs);
  }

  private cooldownProgress(readyAt: number, duration: number): number {
    const remaining = readyAt - this.scene.time.now;
    if (remaining <= 0) return 1;
    return Phaser.Math.Clamp(1 - remaining / duration, 0, 1);
  }

  takeDamage(amount: number): boolean {
    if (this.isInvulnerable) return false;
    this.stats.health = Math.max(0, this.stats.health - amount);
    this.invulnUntil = this.scene.time.now + 700;
    this.startBlink();
    return true;
  }

  heal(amount: number): void {
    this.stats.health = Math.min(
      this.stats.maxHealth,
      this.stats.health + amount,
    );
  }

  get isDead(): boolean {
    return this.stats.health <= 0;
  }

  // Plays the death sequence and stops the hero. Called on game over (after
  // which GameScene stops updating the player, so the anim runs uninterrupted).
  die(): void {
    this.blinkTween?.stop();
    this.clearTint();
    this.setAlpha(1);
    this.setScale(SCALE);
    this.setAngle(0);
    this.setVelocity(0, 0);
    this.play('hero-death');
  }

  private startBlink(): void {
    this.blinkTween?.stop();
    this.setTintFill(COLORS.health);
    this.blinkTween = this.scene.tweens.add({
      targets: this,
      alpha: { from: 0.3, to: 1 },
      duration: 120,
      repeat: 4,
      onComplete: () => {
        this.clearTint();
        this.setAlpha(1);
      },
    });
  }
}
