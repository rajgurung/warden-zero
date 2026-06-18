import Phaser from 'phaser';
import { SCENES, COLORS, CSS, GAME_WIDTH, GAME_HEIGHT } from '../config/constants';

// Enemies: smooth Kenney Toon monsters (grunt=zombie, runner=robot). Each has
// an idle frame + a 4-frame walk cycle as individual PNGs. The player uses a
// dedicated hero sprite sheet loaded separately (see preload).
const CHARACTERS = [
  { key: 'grunt', walk: 4, fps: 10 },
  { key: 'runner', walk: 4, fps: 10 },
] as const;

// Player hero (384x512 transparent PNGs in assets/sprites/hero/, from the
// full-res Recraft Warden master). Three 6-frame directional run cycles (down/up/side);
// the side cycle is mirrored for left. Other states are single crisp poses.
const run = (dir: string) =>
  [0, 1, 2, 3, 4, 5].map((i) => `run_${dir}_${i}`);
// SFX keys — loaded as assets/audio/<key>.mp3 and played via SoundSystem.
const SFX_KEYS = [
  'shoot',
  'enemy_hit',
  'enemy_die',
  'player_hurt',
  'pickup',
  'dash',
  'bomb',
  'upgrade_select',
  'wave_start',
  'game_over',
];

const HERO_STATES = [
  { key: 'hero-idle', frames: ['idle'], fps: 1, repeat: -1 },
  { key: 'hero-run-down', frames: run('down'), fps: 13, repeat: -1 },
  { key: 'hero-run-up', frames: run('up'), fps: 13, repeat: -1 },
  { key: 'hero-run-side', frames: run('side'), fps: 13, repeat: -1 },
  { key: 'hero-shoot', frames: ['shoot'], fps: 1, repeat: -1 },
  { key: 'hero-shoot-up', frames: ['shoot_up'], fps: 1, repeat: -1 },
  { key: 'hero-shoot-down', frames: ['shoot_down'], fps: 1, repeat: -1 },
  { key: 'hero-dash', frames: ['dash'], fps: 1, repeat: 0 },
  { key: 'hero-death', frames: ['death'], fps: 1, repeat: 0 },
] as const;

// Pixel-art monster enemies (Kenney Tiny Dungeon, CC0) — single 16px frame
// each, kept crisp with NEAREST filtering.
const PIXEL_MONSTERS = ['skeleton', 'spider', 'demon'];

// Loads character art + generates utility textures, then builds animations.
export class PreloadScene extends Phaser.Scene {
  constructor() {
    super(SCENES.PRELOAD);
  }

  preload(): void {
    this.showLoadingText();

    this.load.setPath('assets/sprites');
    // Enemy art (Kenney Toon Characters, CC0).
    for (const c of CHARACTERS) {
      this.load.image(`${c.key}_idle`, `${c.key}_idle.png`);
      for (let i = 0; i < c.walk; i++) {
        this.load.image(`${c.key}_walk${i}`, `${c.key}_walk${i}.png`);
      }
    }
    // Pixel-art monster enemies (single idle frame each).
    for (const m of PIXEL_MONSTERS) this.load.image(`${m}_idle`, `${m}_idle.png`);

    // Full-resolution hero art for the menu showcase (crisp at large size).
    this.load.image('warden_full', 'warden_full.png');

    // Player hero pose frames (individual transparent PNGs).
    this.load.setPath('assets/sprites/hero');
    for (const s of HERO_STATES) {
      for (const f of s.frames) this.load.image(f, `${f}.png`);
    }

    // Sound effects (Kenney CC0, converted to MP3 for broad browser support).
    this.load.setPath('assets/audio');
    for (const key of SFX_KEYS) this.load.audio(key, `${key}.mp3`);

    this.load.setPath();
  }

  create(): void {
    this.generateTextures();
    this.createAnimations();
    this.createHeroAnimations();
    this.createMonsterAnimations();
    this.scene.start(SCENES.MAIN_MENU);
  }

  // Single-frame walk anim + crisp NEAREST filtering for the pixel monsters.
  private createMonsterAnimations(): void {
    for (const m of PIXEL_MONSTERS) {
      this.textures.get(`${m}_idle`)?.setFilter(Phaser.Textures.FilterMode.NEAREST);
      if (this.anims.exists(`${m}_walk`)) continue;
      this.anims.create({
        key: `${m}_walk`,
        frames: [{ key: `${m}_idle` }],
        frameRate: 1,
        repeat: -1,
      });
    }
  }

  private createHeroAnimations(): void {
    for (const s of HERO_STATES) {
      if (this.anims.exists(s.key)) continue;
      this.anims.create({
        key: s.key,
        frames: s.frames.map((f) => ({ key: f })),
        frameRate: s.fps,
        repeat: s.repeat,
      });
    }
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
    this.makeRingTexture('ring', 128);
    this.makeDotTexture('dot', 8);
    this.makeBulletTexture('bullet');
    this.makeShadowTexture('shadow', 64);
    this.makeHeartTexture('heart');
    this.makeCoinTexture('coin');
    this.makeGemTexture('gem');
  }

  // Glowing cyan crystal — the wave-objective gem.
  private makeGemTexture(key: string): void {
    if (this.textures.exists(key)) return;
    const g = this.add.graphics();
    // Outer diamond (darker cyan), inner highlight.
    g.fillStyle(0x1aa3b8, 1);
    g.beginPath();
    g.moveTo(12, 0);
    g.lineTo(24, 13);
    g.lineTo(12, 28);
    g.lineTo(0, 13);
    g.closePath();
    g.fillPath();
    g.fillStyle(0x6df0ff, 1);
    g.beginPath();
    g.moveTo(12, 4);
    g.lineTo(19, 13);
    g.lineTo(12, 22);
    g.lineTo(5, 13);
    g.closePath();
    g.fillPath();
    g.fillStyle(0xffffff, 0.9);
    g.fillCircle(12, 11, 2.5);
    g.generateTexture(key, 24, 28);
    g.destroy();
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

  // Hollow white ring — tinted/scaled/faded for explosion shockwaves.
  private makeRingTexture(key: string, size: number): void {
    if (this.textures.exists(key)) return;
    const g = this.add.graphics();
    const r = size / 2;
    g.lineStyle(7, 0xffffff, 1);
    g.strokeCircle(r, r, r - 5);
    g.generateTexture(key, size, size);
    g.destroy();
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
