import Phaser from 'phaser';
import { COLORS, CSS } from '../config/constants';

export type ButtonOptions = {
  width?: number;
  height?: number;
  accent?: number;
  fontSize?: number;
};

// Reusable rounded button with hover-glow + press feedback. Returned as a
// Container so callers can position/animate it freely.
export function createButton(
  scene: Phaser.Scene,
  x: number,
  y: number,
  label: string,
  onClick: () => void,
  opts: ButtonOptions = {},
): Phaser.GameObjects.Container {
  const w = opts.width ?? 280;
  const h = opts.height ?? 56;
  const accent = opts.accent ?? COLORS.accent;

  const bg = scene.add.graphics();
  const draw = (fill: number, alpha: number) => {
    bg.clear();
    bg.fillStyle(fill, alpha);
    bg.fillRoundedRect(-w / 2, -h / 2, w, h, 12);
    bg.lineStyle(2, accent, 0.9);
    bg.strokeRoundedRect(-w / 2, -h / 2, w, h, 12);
  };
  draw(COLORS.panel, 0.85);

  const text = scene.add
    .text(0, 0, label, {
      fontFamily: 'system-ui, sans-serif',
      fontSize: `${opts.fontSize ?? 22}px`,
      fontStyle: 'bold',
      color: CSS.textBright,
    })
    .setOrigin(0.5)
    .setLetterSpacing(4);

  const container = scene.add.container(x, y, [bg, text]);
  container.setSize(w, h);
  container.setInteractive(
    new Phaser.Geom.Rectangle(-w / 2, -h / 2, w, h),
    Phaser.Geom.Rectangle.Contains,
  );

  container.on('pointerover', () => {
    draw(accent, 0.22);
    scene.tweens.add({ targets: container, scale: 1.05, duration: 120 });
  });
  container.on('pointerout', () => {
    draw(COLORS.panel, 0.85);
    scene.tweens.add({ targets: container, scale: 1, duration: 120 });
  });
  container.on('pointerdown', () => {
    scene.tweens.add({
      targets: container,
      scale: 0.97,
      duration: 70,
      yoyo: true,
      onComplete: onClick,
    });
  });

  return container;
}
