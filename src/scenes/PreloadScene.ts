import Phaser from 'phaser';
import { SCENES, COLORS, CSS, GAME_WIDTH, GAME_HEIGHT } from '../config/constants';

// Mixed cast: a pixel-art CC0 military soldier (player, kept crisp via NEAREST
// filtering) + smooth Kenney Toon monsters (grunt=zombie, runner=robot).
// Each has an idle frame + an N-frame walk cycle.
const CHARACTERS = [
  { key: 'player', walk: 4, fps: 12 },
  { key: 'grunt', walk: 4, fps: 10 },
  { key: 'runner', walk: 4, fps: 10 },
] as const;

// Player frames are pixel-art; keep them sharp under the global smooth filter.
const PIXEL_TEXTURES = [
  'player_idle',
  'player_walk0',
  'player_walk1',
  'player_walk2',
  'player_walk3',
];

// Loads character art + generates utility textures, then builds animations.
export class PreloadScene extends Phaser.Scene {
  constructor() {
    super(SCENES.PRELOAD);
  }

  preload(): void {
    this.showLoadingText();

    // Animated front/side-facing character art (Kenney Toon Characters, CC0).
    this.load.setPath('assets/sprites');
    for (const c of CHARACTERS) {
      this.load.image(`${c.key}_idle`, `${c.key}_idle.png`);
      for (let i = 0; i < c.walk; i++) {
        this.load.image(`${c.key}_walk${i}`, `${c.key}_walk${i}.png`);
      }
    }
    this.load.setPath();
  }

  create(): void {
    this.generateTextures();
    this.createAnimations();
    // Keep the pixel soldier crisp while everything else stays smooth.
    for (const key of PIXEL_TEXTURES) {
      this.textures.get(key)?.setFilter(Phaser.Textures.FilterMode.NEAREST);
    }
    this.scene.start(SCENES.MAIN_MENU);
  }

  private showLoadingText(): void {
    this.add
      .text(GAME_WIDTH / 2, GAME_HEIGHT / 2, 'LOADING', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '20px',
        color: CSS.textDim,
      })
      .setOrigin(0.5)
      .setLetterSpacing(6);
  }

  private createAnimations(): void {
    for (const c of CHARACTERS) {
      if (this.anims.exists(`${c.key}_walk`)) continue;
      const frames = Array.from({ length: c.walk }, (_, i) => ({
        key: `${c.key}_walk${i}`,
      }));
      this.anims.create({
        key: `${c.key}_walk`,
        frames,
        frameRate: c.fps,
        repeat: -1,
      });
    }
  }

  // Procedural utility textures: glow, particle dot, bullet, shadow, pickups.
  private generateTextures(): void {
    this.makeGlowTexture('glow', 128, COLORS.accent);
    this.makeDotTexture('dot', 8);
    this.makeBulletTexture('bullet');
    this.makeShadowTexture('shadow', 64);
    this.makeHeartTexture('heart');
    this.makeCoinTexture('coin');
  }

  private makeGlowTexture(key: string, size: number, color: number): void {
    if (this.textures.exists(key)) return;
    const tex = this.textures.createCanvas(key, size, size);
    if (!tex) return;
    const ctx = tex.getContext();
    const r = size / 2;
    const c = Phaser.Display.Color.IntegerToColor(color);
    const grad = ctx.createRadialGradient(r, r, 0, r, r, r);
    grad.addColorStop(0, `rgba(${c.red},${c.green},${c.blue},0.9)`);
    grad.addColorStop(0.4, `rgba(${c.red},${c.green},${c.blue},0.35)`);
    grad.addColorStop(1, `rgba(${c.red},${c.green},${c.blue},0)`);
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, size, size);
    tex.refresh();
  }

  private makeDotTexture(key: string, size: number): void {
    if (this.textures.exists(key)) return;
    const g = this.add.graphics();
    g.fillStyle(0xffffff, 1);
    g.fillCircle(size / 2, size / 2, size / 2);
    g.generateTexture(key, size, size);
    g.destroy();
  }

  // Soft elliptical drop shadow (black radial fading out).
  private makeShadowTexture(key: string, size: number): void {
    if (this.textures.exists(key)) return;
    const tex = this.textures.createCanvas(key, size, size);
    if (!tex) return;
    const ctx = tex.getContext();
    const r = size / 2;
    const grad = ctx.createRadialGradient(r, r, 0, r, r, r);
    grad.addColorStop(0, 'rgba(0,0,0,0.55)');
    grad.addColorStop(0.7, 'rgba(0,0,0,0.25)');
    grad.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, size, size);
    tex.refresh();
  }

  private makeBulletTexture(key: string): void {
    if (this.textures.exists(key)) return;
    const w = 18;
    const h = 8;
    const g = this.add.graphics();
    g.fillStyle(COLORS.gold, 1);
    g.fillRoundedRect(0, 0, w, h, h / 2);
    g.fillStyle(0xfff4c2, 1);
    g.fillRoundedRect(2, 2, w - 8, h - 4, (h - 4) / 2);
    g.generateTexture(key, w, h);
    g.destroy();
  }

  private makeHeartTexture(key: string): void {
    if (this.textures.exists(key)) return;
    const g = this.add.graphics();
    g.fillStyle(COLORS.health, 1);
    g.fillCircle(8, 9, 6);
    g.fillCircle(18, 9, 6);
    g.beginPath();
    g.moveTo(2, 11);
    g.lineTo(24, 11);
    g.lineTo(13, 24);
    g.closePath();
    g.fillPath();
    g.generateTexture(key, 26, 26);
    g.destroy();
  }

  private makeCoinTexture(key: string): void {
    if (this.textures.exists(key)) return;
    const g = this.add.graphics();
    g.fillStyle(COLORS.gold, 1);
    g.fillCircle(12, 12, 11);
    g.fillStyle(0xfff0b0, 1);
    g.fillCircle(12, 12, 7);
    g.fillStyle(COLORS.gold, 1);
    g.fillCircle(12, 12, 4);
    g.generateTexture(key, 24, 24);
    g.destroy();
  }
}
