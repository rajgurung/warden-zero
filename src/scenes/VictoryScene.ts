import Phaser from 'phaser';
import { SCENES, COLORS, CSS, GAME_WIDTH, GAME_HEIGHT } from '../config/constants';
import { createButton } from '../ui/Button';
import { createPanel } from '../ui/Panel';
import { createInitialRunState } from '../config/playerStats';
import type { RunState } from '../types/game';

type VictoryData = { runState: RunState; subtitle?: string; againScene?: string };

// Festive palette for confetti + fireworks.
const PARTY = [
  COLORS.accent,
  COLORS.gold,
  COLORS.magenta,
  COLORS.pickup,
  COLORS.enemyAccent,
  0xffffff,
];

export class VictoryScene extends Phaser.Scene {
  constructor() {
    super(SCENES.VICTORY);
  }

  private againScene: string = SCENES.GAME;

  create(data: VictoryData): void {
    const run = data.runState;
    this.againScene = data.againScene ?? SCENES.GAME;
    const cx = GAME_WIDTH / 2;

    this.add
      .rectangle(0, 0, GAME_WIDTH, GAME_HEIGHT, COLORS.bgDeep, 0.82)
      .setOrigin(0, 0);

    this.startConfetti();
    this.startFireworks();

    createPanel(this, cx, GAME_HEIGHT / 2, 600, 520, { accent: COLORS.accent });

    this.add
      .image(cx, GAME_HEIGHT * 0.22, 'glow')
      .setScale(7, 4.5)
      .setTint(COLORS.gold)
      .setAlpha(0.32)
      .setBlendMode(Phaser.BlendModes.ADD);

    const title = this.add
      .text(cx, GAME_HEIGHT * 0.22, 'VICTORY', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '84px',
        fontStyle: 'bold',
        color: CSS.textBright,
      })
      .setOrigin(0.5)
      .setLetterSpacing(8)
      .setShadow(0, 0, CSS.gold, 30, true, true);

    // Triumphant pop-in, then a gentle pulse.
    title.setScale(0.2);
    this.tweens.add({ targets: title, scale: 1, duration: 600, ease: 'Back.easeOut' });
    this.tweens.add({
      targets: title,
      scale: { from: 1, to: 1.04 },
      duration: 1400,
      delay: 600,
      yoyo: true,
      repeat: -1,
      ease: 'Sine.easeInOut',
    });

    this.add
      .text(cx, GAME_HEIGHT * 0.35, data.subtitle ?? 'The horde is broken. The Colossus has fallen.', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '19px',
        color: CSS.textDim,
      })
      .setOrigin(0.5);

    this.buildStats(run, cx);

    // Buttons fade in after the stats have had a beat to land.
    const playAgain = createButton(
      this,
      cx,
      GAME_HEIGHT * 0.78,
      'PLAY AGAIN',
      () => this.again(),
      { accent: COLORS.accent },
    );
    const menu = createButton(
      this,
      cx,
      GAME_HEIGHT * 0.78 + 70,
      'MAIN MENU',
      () => this.scene.start(SCENES.MAIN_MENU),
      { accent: COLORS.panelEdge },
    );
    for (const b of [playAgain, menu]) {
      b.setAlpha(0);
      this.tweens.add({ targets: b, alpha: 1, duration: 400, delay: 900 });
    }
  }

  // Animated stat rows with count-up numbers.
  private buildStats(run: RunState, cx: number): void {
    const rows: Array<[string, number, (n: number) => string]> = [
      ['FINAL SCORE', run.score, (n) => Math.round(n).toLocaleString()],
      ['ENEMIES SLAIN', run.kills, (n) => `${Math.round(n)}`],
      ['COINS', run.coins, (n) => `${Math.round(n)}`],
      ['LEVEL REACHED', run.level, (n) => `${Math.round(n)}`],
      ['TIME SURVIVED', run.lifetimeMs, (n) => this.fmtTime(n)],
    ];

    const top = GAME_HEIGHT * 0.44;
    const gap = 34;
    rows.forEach(([label, value, fmt], i) => {
      const y = top + i * gap;
      this.add
        .text(cx - 150, y, label, {
          fontFamily: 'monospace',
          fontSize: '17px',
          color: CSS.textDim,
        })
        .setOrigin(0, 0.5);
      const valText = this.add
        .text(cx + 150, y, fmt(0), {
          fontFamily: 'monospace',
          fontSize: '19px',
          fontStyle: 'bold',
          color: CSS.textBright,
        })
        .setOrigin(1, 0.5);

      const counter = { v: 0 };
      this.tweens.add({
        targets: counter,
        v: value,
        duration: 900,
        delay: 300 + i * 120,
        ease: 'Cubic.easeOut',
        onUpdate: () => valText.setText(fmt(counter.v)),
        onComplete: () => valText.setText(fmt(value)),
      });
    });
  }

  private fmtTime(ms: number): string {
    const s = Math.floor(ms / 1000);
    const m = Math.floor(s / 60);
    return `${m}:${String(s % 60).padStart(2, '0')}`;
  }

  // Confetti raining from the top edge, recycled continuously.
  private startConfetti(): void {
    this.add
      .particles(0, -20, 'dot', {
        x: { min: 0, max: GAME_WIDTH },
        y: -20,
        quantity: 3,
        frequency: 60,
        lifespan: 4200,
        speedY: { min: 120, max: 260 },
        speedX: { min: -60, max: 60 },
        scale: { min: 0.6, max: 1.4 },
        rotate: { min: 0, max: 360 },
        gravityY: 40,
        tint: PARTY,
        alpha: { start: 1, end: 0.6 },
      })
      .setDepth(50);
  }

  // Periodic firework bursts at random points in the upper screen.
  private startFireworks(): void {
    this.time.addEvent({
      delay: 650,
      loop: true,
      callback: () => {
        const x = Phaser.Math.Between(120, GAME_WIDTH - 120);
        const y = Phaser.Math.Between(80, GAME_HEIGHT * 0.5);
        this.firework(x, y, Phaser.Utils.Array.GetRandom(PARTY));
      },
    });
    // Immediate bursts so it kicks off instantly.
    this.firework(GAME_WIDTH * 0.3, GAME_HEIGHT * 0.3, COLORS.gold);
    this.firework(GAME_WIDTH * 0.7, GAME_HEIGHT * 0.28, COLORS.accent);
  }

  private firework(x: number, y: number, color: number): void {
    if (this.cache.audio.exists('pickup')) {
      this.sound.play('pickup', { volume: 0.25 });
    }

    const flash = this.add
      .image(x, y, 'glow')
      .setTint(color)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setScale(0.4)
      .setDepth(49);
    this.tweens.add({
      targets: flash,
      scale: 1.6,
      alpha: 0,
      duration: 400,
      ease: 'Cubic.easeOut',
      onComplete: () => flash.destroy(),
    });

    const ring = this.add
      .image(x, y, 'ring')
      .setTint(color)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setAlpha(0.9)
      .setScale(0.2)
      .setDepth(49);
    this.tweens.add({
      targets: ring,
      scale: 1.8,
      alpha: 0,
      duration: 520,
      ease: 'Cubic.easeOut',
      onComplete: () => ring.destroy(),
    });

    const sparks = this.add.particles(x, y, 'dot', {
      tint: [color, 0xffffff],
      speed: { min: 160, max: 420 },
      angle: { min: 0, max: 360 },
      scale: { start: 0.9, end: 0 },
      lifespan: { min: 500, max: 900 },
      gravityY: 120,
      blendMode: Phaser.BlendModes.ADD,
      emitting: false,
    });
    sparks.setDepth(49);
    sparks.explode(40);
    this.time.delayedCall(1000, () => sparks.destroy());
  }

  private again(): void {
    this.registry.set('runState', createInitialRunState());
    this.scene.start(this.againScene);
  }
}
