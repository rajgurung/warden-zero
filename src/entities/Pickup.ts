import Phaser from 'phaser';
import { COLORS } from '../config/constants';

export type PickupType = 'heart' | 'coin';

export const HEART_HEAL = 18;
export const COIN_VALUE = 25;

// Collectible dropped on enemy death. Bobs gently, glows, and expires after a
// few seconds. The scene handles the stat change on overlap.
export class Pickup extends Phaser.Physics.Arcade.Sprite {
  readonly pickupType: PickupType;

  constructor(scene: Phaser.Scene, x: number, y: number, type: PickupType) {
    super(scene, x, y, type);
    this.pickupType = type;

    scene.add.existing(this);
    scene.physics.add.existing(this);
    (this.body as Phaser.Physics.Arcade.Body).setCircle(this.width / 2);
    this.setDepth(y);

    const glow = scene.add
      .image(x, y, 'glow')
      .setScale(0.5)
      .setTint(type === 'heart' ? COLORS.health : COLORS.gold)
      .setAlpha(0.4)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(this.depth - 1);
    // Keep the glow pinned to this pickup and cleaned up with it.
    this.on('destroy', () => glow.destroy());
    scene.tweens.add({
      targets: [this, glow],
      y: y - 6,
      duration: 700,
      yoyo: true,
      repeat: -1,
      ease: 'Sine.easeInOut',
    });

    // Expire after 8s with a blink warning in the last second.
    scene.time.delayedCall(7000, () => {
      if (!this.active) return;
      scene.tweens.add({
        targets: this,
        alpha: 0.2,
        duration: 150,
        yoyo: true,
        repeat: 3,
        onComplete: () => this.destroy(),
      });
    });
  }
}
