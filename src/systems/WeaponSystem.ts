import Phaser from 'phaser';
import { Bullet } from '../entities/Bullet';
import type { Player } from '../entities/Player';
import type { EffectsSystem } from './EffectsSystem';
import type { PlayerStats } from '../types/game';

const MAX_BULLETS = 80;

// Owns the player bullet pool and converts trigger-pulls into shots,
// respecting fire rate, bullet count (double-shot), size and piercing.
export class WeaponSystem {
  readonly group: Phaser.Physics.Arcade.Group;
  private player: Player;
  private effects: EffectsSystem;
  private lastFired = 0;

  constructor(scene: Phaser.Scene, player: Player, effects: EffectsSystem) {
    this.player = player;
    this.effects = effects;
    this.group = scene.physics.add.group({
      classType: Bullet,
      maxSize: MAX_BULLETS,
      runChildUpdate: true,
    });
  }

  update(time: number, pointer: Phaser.Input.Pointer): void {
    if (!pointer.isDown) return;
    const stats = this.player.stats;
    if (time - this.lastFired < stats.fireRateMs) return;
    this.lastFired = time;
    this.fire(stats);
  }

  private fire(stats: PlayerStats): void {
    const baseAngle = this.player.aim;
    const count = stats.bulletCount;
    // Spread shots symmetrically around the aim direction.
    const spread = Phaser.Math.DegToRad(8);
    const start = -((count - 1) / 2) * spread;

    // Spawn from the tip of the held rifle.
    const muzzleX = this.player.muzzleX;
    const muzzleY = this.player.muzzleY;

    for (let i = 0; i < count; i++) {
      const angle = baseAngle + start + i * spread;
      const bullet = this.group.get() as Bullet | null;
      if (!bullet) break;
      bullet.fire(
        muzzleX,
        muzzleY,
        angle,
        {
          damage: stats.bulletDamage,
          speed: stats.bulletSpeed,
          lifetimeMs: 900,
          piercing: stats.bulletPiercing,
        },
        stats.bulletSize,
      );
    }

    this.effects.muzzleFlash(muzzleX, muzzleY, baseAngle);
  }
}
