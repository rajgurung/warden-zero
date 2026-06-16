import Phaser from 'phaser';
import { COLORS, CSS } from '../config/constants';
import type { Upgrade } from '../config/upgrades';

const W = 260;
const H = 320;

// A selectable upgrade card with hover-lift and a hotkey badge (1/2/3).
export function createUpgradeCard(
  scene: Phaser.Scene,
  x: number,
  y: number,
  upgrade: Upgrade,
  index: number,
  onSelect: () => void,
): Phaser.GameObjects.Container {
  const bg = scene.add.graphics();
  const draw = (fill: number, alpha: number, edgeAlpha: number) => {
    bg.clear();
    bg.fillStyle(fill, alpha);
    bg.fillRoundedRect(-W / 2, -H / 2, W, H, 16);
    bg.lineStyle(2, COLORS.accent, edgeAlpha);
    bg.strokeRoundedRect(-W / 2, -H / 2, W, H, 16);
  };
  draw(COLORS.panel, 0.9, 0.5);

  const glow = scene.add
    .image(0, -H / 2 + 70, 'glow')
    .setScale(1.6)
    .setTint(COLORS.accent)
    .setAlpha(0.35)
    .setBlendMode(Phaser.BlendModes.ADD);

  const hotkey = scene.add
    .text(0, -H / 2 + 48, `${index + 1}`, {
      fontFamily: 'system-ui, sans-serif',
      fontSize: '40px',
      fontStyle: 'bold',
      color: CSS.accent,
    })
    .setOrigin(0.5);

  const title = scene.add
    .text(0, -20, upgrade.title, {
      fontFamily: 'system-ui, sans-serif',
      fontSize: '24px',
      fontStyle: 'bold',
      color: CSS.textBright,
      align: 'center',
      wordWrap: { width: W - 40 },
    })
    .setOrigin(0.5);

  const desc = scene.add
    .text(0, 60, upgrade.description, {
      fontFamily: 'system-ui, sans-serif',
      fontSize: '16px',
      color: CSS.textDim,
      align: 'center',
      wordWrap: { width: W - 48 },
      lineSpacing: 6,
    })
    .setOrigin(0.5);

  const container = scene.add.container(x, y, [bg, glow, hotkey, title, desc]);
  container.setSize(W, H);
  container.setInteractive(
    new Phaser.Geom.Rectangle(-W / 2, -H / 2, W, H),
    Phaser.Geom.Rectangle.Contains,
  );

  container.on('pointerover', () => {
    draw(COLORS.panelEdge, 0.95, 1);
    scene.tweens.add({ targets: container, y: y - 12, scale: 1.04, duration: 120 });
  });
  container.on('pointerout', () => {
    draw(COLORS.panel, 0.9, 0.5);
    scene.tweens.add({ targets: container, y, scale: 1, duration: 120 });
  });
  container.on('pointerdown', onSelect);

  return container;
}
