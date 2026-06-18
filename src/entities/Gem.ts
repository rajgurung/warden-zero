import Phaser from 'phaser';
import { COLORS } from '../config/constants';

export const GEM_SCORE = 50;

// Wave-objective gem dropped by enemies. Sits and bobs until the player (or
// the end-of-wave vacuum) pulls it in; GameScene drives the magnet.
export class Gem extends Phaser.Physics.Arcade.Sprite {
  constructor(scene: Phaser.Scene, x: number, y: number) {
    super(scene, x, y, 'gem');
    scene.add.existing(this);
    scene.physics.add.existing(this);
    (this.body as Phaser.Physics.Arcade.Body).setCircle(this.width / 2);
    this.setDepth(y);

    const glow = scene.add
      .image(x, y, 'glow')
      .setScale(0.5)
      .setTint(COLORS.accent)
      .setAlpha(0.5)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(y - 1);
    this.on('destroy', () => glow.destroy());

    // Gentle bob + the glow tracks it.
    scene.tweens.add({
      targets: [this, glow],
      y: y - 8,
      duration: 650,
      yoyo: true,
      repeat: -1,
      ease: 'Sine.easeInOut',
    });

    // Spawn pop.
    this.setScale(0);
    scene.tweens.add({ targets: this, scale: 1, duration: 200, ease: 'Back.easeOut' });
  }
}
