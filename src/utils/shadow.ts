import Phaser from 'phaser';

// Soft drop shadow that follows a unit's feet and sorts just beneath it.
// Auto-destroys with its owner.
export function makeShadow(
  scene: Phaser.Scene,
  owner: Phaser.GameObjects.Sprite,
): Phaser.GameObjects.Image {
  const shadow = scene.add.image(owner.x, owner.y, 'shadow');
  shadow.setDisplaySize(owner.displayWidth * 0.55, owner.displayWidth * 0.22);
  shadow.setAlpha(0.9);
  owner.once('destroy', () => shadow.destroy());
  return shadow;
}

export function syncShadow(
  shadow: Phaser.GameObjects.Image,
  owner: Phaser.GameObjects.Sprite,
  footFactor = 0.46,
): void {
  shadow.x = owner.x;
  shadow.y = owner.y + owner.displayHeight * footFactor;
  shadow.setDepth(owner.y - 1);
}
