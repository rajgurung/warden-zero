import Phaser from 'phaser';
import { COLORS } from '../config/constants';

export type PanelOptions = {
  accent?: number;
  fillAlpha?: number;
  radius?: number;
};

// Framed "tech" panel: dark rounded fill, neon border, and L-shaped corner
// accents. Centered at (x, y). Returns the Graphics so callers can depth it.
export function createPanel(
  scene: Phaser.Scene,
  x: number,
  y: number,
  w: number,
  h: number,
  opts: PanelOptions = {},
): Phaser.GameObjects.Graphics {
  const accent = opts.accent ?? COLORS.accent;
  const fillAlpha = opts.fillAlpha ?? 0.92;
  const radius = opts.radius ?? 16;
  const hw = w / 2;
  const hh = h / 2;

  const g = scene.add.graphics({ x, y });
  g.fillStyle(COLORS.bgDeep, fillAlpha);
  g.fillRoundedRect(-hw, -hh, w, h, radius);
  // Subtle inner panel tone.
  g.fillStyle(COLORS.panel, 0.35);
  g.fillRoundedRect(-hw + 4, -hh + 4, w - 8, h - 8, radius - 2);
  // Border.
  g.lineStyle(2, accent, 0.7);
  g.strokeRoundedRect(-hw, -hh, w, h, radius);

  // L-shaped corner accents.
  const c = 18;
  g.lineStyle(3, accent, 1);
  const corners: Array<[number, number, number, number]> = [
    [-hw, -hh, 1, 1],
    [hw, -hh, -1, 1],
    [-hw, hh, 1, -1],
    [hw, hh, -1, -1],
  ];
  for (const [cx, cy, sx, sy] of corners) {
    g.beginPath();
    g.moveTo(cx + sx * c, cy);
    g.lineTo(cx, cy);
    g.lineTo(cx, cy + sy * c);
    g.strokePath();
  }

  return g;
}
