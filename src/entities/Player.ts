import Phaser from 'phaser';
import type { PlayerStats } from '../types/game';
import { COLORS } from '../config/constants';
import { makeShadow, syncShadow } from '../utils/shadow';

type MoveKeys = {
  W: Phaser.Input.Keyboard.Key;
  A: Phaser.Input.Keyboard.Key;
  S: Phaser.Input.Keyboard.Key;
  D: Phaser.Input.Keyboard.Key;
};

const SCALE = 1.3;
const BODY_RADIUS = 16; // effective collision radius (world px)
const MUZZLE_OFFSET = 20; // bullet spawn distance from player centre

export class Player extends Phaser.Physics.Arcade.Sprite {
  readonly stats: PlayerStats;
  private keys: MoveKeys;
  private dashKey: Phaser.Input.Keyboard.Key;
  private shadow: Phaser.GameObjects.Image;
  private invulnUntil = 0;
  private aimAngle = 0;
  private blinkTween?: Phaser.Tweens.Tween;
  private dashing = false;
  private dashReadyAt = 0;
  private bombReadyAt = 0;

  constructor(scene: Phaser.Scene, x: number, y: number, stats: PlayerStats) {
    super(scene, x, y, 'player_idle');
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

    this.shadow = makeShadow(scene, this);

    this.keys = scene.input.keyboard!.addKeys('W,A,S,D') as MoveKeys;
    this.dashKey = scene.input.keyboard!.addKey('Q');
  }

  get isInvulnerable(): boolean {
    return this.scene.time.now < this.invulnUntil;
  }

  get aim(): number {
    return this.aimAngle;
  }

  // Point in front of the soldier where bullets/muzzle flash originate.
  get muzzleX(): number {
    return this.x + Math.cos(this.aimAngle) * MUZZLE_OFFSET;
  }

  get muzzleY(): number {
    return this.y + Math.sin(this.aimAngle) * MUZZLE_OFFSET;
  }

  get moveDir(): Phaser.Math.Vector2 {
    const v = new Phaser.Math.Vector2(0, 0);
    if (this.keys.A.isDown) v.x -= 1;
    if (this.keys.D.isDown) v.x += 1;
    if (this.keys.W.isDown) v.y -= 1;
    if (this.keys.S.isDown) v.y += 1;
    return v.normalize();
  }

  update(pointer: Phaser.Input.Pointer): void {
    this.aimAngle = Phaser.Math.Angle.Between(
      this.x,
      this.y,
      pointer.worldX,
      pointer.worldY,
    );
    // Face the aim direction (characters only turn left/right in 3/4 view).
    this.setFlipX(pointer.worldX < this.x);

    if (Phaser.Input.Keyboard.JustDown(this.dashKey)) this.tryDash();

    const dir = this.moveDir;
    if (!this.dashing) {
      this.setVelocity(dir.x * this.stats.speed, dir.y * this.stats.speed);
    }

    // Walk / idle animation.
    const moving = dir.lengthSq() > 0 || this.dashing;
    if (moving) {
      this.anims.play('player_walk', true);
    } else if (this.anims.isPlaying) {
      this.anims.stop();
      this.setTexture('player_idle');
    }

    this.setDepth(this.y);
    syncShadow(this.shadow, this);
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

    this.setTint(0x9fe8ff);
    this.spawnDashTrail();
    this.scene.time.delayedCall(this.stats.dashDurationMs, () => {
      this.dashing = false;
      this.clearTint();
    });
  }

  // Fading after-images along the dash path for a sense of speed.
  private spawnDashTrail(): void {
    for (let i = 0; i < 4; i++) {
      this.scene.time.delayedCall(i * 32, () => {
        const ghost = this.scene.add
          .image(this.x, this.y, this.texture.key)
          .setScale(SCALE)
          .setFlipX(this.flipX)
          .setTint(0x9fe8ff)
          .setAlpha(0.45)
          .setDepth(this.y - 2)
          .setBlendMode(Phaser.BlendModes.ADD);
        this.scene.tweens.add({
          targets: ghost,
          alpha: 0,
          duration: 240,
          onComplete: () => ghost.destroy(),
        });
      });
    }
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
