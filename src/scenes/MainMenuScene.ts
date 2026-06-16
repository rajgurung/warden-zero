import Phaser from 'phaser';
import {
  SCENES,
  COLORS,
  CSS,
  GAME_WIDTH,
  GAME_HEIGHT,
} from '../config/constants';
import { createButton } from '../ui/Button';
import { createInitialRunState } from '../config/playerStats';

// Polished landing screen: cinematic gradient backdrop, drifting motes,
// glowing wordmark, and clean buttons. Starts the run on Play / Enter.
export class MainMenuScene extends Phaser.Scene {
  private controlsPanel?: Phaser.GameObjects.Container;

  constructor() {
    super(SCENES.MAIN_MENU);
  }

  create(): void {
    this.buildBackdrop();
    this.buildHero();
    this.buildTitle();
    this.buildButtons();
    this.buildControlsPanel();

    this.input.keyboard?.once('keydown-ENTER', () => this.startGame());
  }

  // Hero showcase on the right: the player character on a glowing platform,
  // with a shadow and a gentle idle bob — ties the menu to the in-game look.
  private buildHero(): void {
    const hx = GAME_WIDTH * 0.72;
    const hy = GAME_HEIGHT * 0.54;

    this.add
      .image(hx, hy + 30, 'glow')
      .setScale(7, 5)
      .setTint(COLORS.accent)
      .setAlpha(0.22)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(-7);

    const platform = this.add
      .image(hx, hy + 150, 'glow')
      .setTint(COLORS.accent)
      .setAlpha(0.5)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(-6);
    platform.setDisplaySize(320, 90);

    this.add
      .image(hx, hy + 150, 'shadow')
      .setDisplaySize(200, 56)
      .setAlpha(0.8)
      .setDepth(-6);

    const hero = this.add
      .image(hx, hy, 'player_idle')
      .setScale(3)
      .setDepth(-5);
    this.tweens.add({
      targets: hero,
      y: hy - 12,
      duration: 1700,
      yoyo: true,
      repeat: -1,
      ease: 'Sine.easeInOut',
    });
  }

  private buildBackdrop(): void {
    const cx = GAME_WIDTH / 2;
    const cy = GAME_HEIGHT / 2;

    // Vertical gradient via stacked bands (cheap, no shader needed).
    const top = Phaser.Display.Color.IntegerToColor(COLORS.bgMid);
    const bottom = Phaser.Display.Color.IntegerToColor(COLORS.bgDeep);
    const bands = 32;
    for (let i = 0; i < bands; i++) {
      const t = i / (bands - 1);
      const col = Phaser.Display.Color.Interpolate.ColorWithColor(
        top,
        bottom,
        bands - 1,
        i,
      );
      this.add
        .rectangle(
          0,
          (GAME_HEIGHT / bands) * i,
          GAME_WIDTH,
          GAME_HEIGHT / bands + 1,
          Phaser.Display.Color.GetColor(col.r, col.g, col.b),
        )
        .setOrigin(0, 0)
        .setDepth(-10);
      void t;
    }

    // Faint accent glows top-left and bottom-right for depth.
    this.add
      .image(cx - 360, cy - 200, 'glow')
      .setScale(6)
      .setAlpha(0.18)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(-9);
    this.add
      .image(cx + 380, cy + 220, 'glow')
      .setScale(7)
      .setTint(COLORS.magenta)
      .setAlpha(0.14)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(-9);

    // Drifting motes.
    for (let i = 0; i < 40; i++) {
      const x = Phaser.Math.Between(0, GAME_WIDTH);
      const y = Phaser.Math.Between(0, GAME_HEIGHT);
      const mote = this.add
        .image(x, y, 'dot')
        .setScale(Phaser.Math.FloatBetween(0.15, 0.5))
        .setAlpha(Phaser.Math.FloatBetween(0.05, 0.25))
        .setTint(COLORS.accent)
        .setDepth(-8);
      this.tweens.add({
        targets: mote,
        y: y - Phaser.Math.Between(40, 120),
        alpha: 0,
        duration: Phaser.Math.Between(4000, 9000),
        delay: Phaser.Math.Between(0, 4000),
        repeat: -1,
        onRepeat: () => {
          mote.y = GAME_HEIGHT + 10;
          mote.x = Phaser.Math.Between(0, GAME_WIDTH);
          mote.alpha = Phaser.Math.FloatBetween(0.05, 0.25);
        },
      });
    }
  }

  private buildTitle(): void {
    const cx = GAME_WIDTH * 0.32;
    const cy = GAME_HEIGHT * 0.36;

    const glow = this.add
      .image(cx, cy, 'glow')
      .setScale(10, 4)
      .setAlpha(0.25)
      .setTint(COLORS.accent)
      .setBlendMode(Phaser.BlendModes.ADD);
    this.tweens.add({
      targets: glow,
      alpha: 0.4,
      scaleX: 11,
      duration: 2200,
      yoyo: true,
      repeat: -1,
      ease: 'Sine.easeInOut',
    });

    const warden = this.add
      .text(cx, cy - 30, 'WARDEN', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '92px',
        fontStyle: 'bold',
        color: CSS.textBright,
      })
      .setOrigin(0.5)
      .setLetterSpacing(6);
    warden.setShadow(0, 0, CSS.accent, 24, true, true);

    const zero = this.add
      .text(cx, cy + 44, 'ZERO', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '92px',
        fontStyle: 'bold',
        color: CSS.accent,
      })
      .setOrigin(0.5)
      .setLetterSpacing(20);
    zero.setShadow(0, 0, CSS.accent, 24, true, true);

    this.add
      .text(cx, cy + 110, 'ARENA SHOOTER', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '18px',
        color: CSS.textDim,
      })
      .setOrigin(0.5)
      .setLetterSpacing(12);
  }

  private buildButtons(): void {
    const cx = GAME_WIDTH * 0.32;
    const baseY = GAME_HEIGHT * 0.66;

    createButton(this, cx, baseY, 'PLAY', () => this.startGame(), {
      accent: COLORS.accent,
    });
    createButton(this, cx, baseY + 76, 'CONTROLS', () => this.toggleControls(), {
      accent: COLORS.panelEdge,
    });

    this.add
      .text(GAME_WIDTH / 2, GAME_HEIGHT - 32, 'A browser game by Raj Gurung', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '14px',
        color: CSS.textDim,
      })
      .setOrigin(0.5);
  }

  private buildControlsPanel(): void {
    const cx = GAME_WIDTH / 2;
    const cy = GAME_HEIGHT / 2;
    const w = 420;
    const h = 300;

    const bg = this.add.graphics();
    bg.fillStyle(COLORS.bgDeep, 0.92);
    bg.fillRoundedRect(-w / 2, -h / 2, w, h, 16);
    bg.lineStyle(2, COLORS.accent, 0.8);
    bg.strokeRoundedRect(-w / 2, -h / 2, w, h, 16);

    const heading = this.add
      .text(0, -h / 2 + 34, 'CONTROLS', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '24px',
        fontStyle: 'bold',
        color: CSS.accent,
      })
      .setOrigin(0.5)
      .setLetterSpacing(4);

    const lines = [
      'WASD          Move',
      'Mouse         Aim',
      'Left Click    Shoot',
      'Q             Dash',
      'E             Bomb',
      'Esc           Pause',
    ].join('\n');

    const body = this.add
      .text(0, 10, lines, {
        fontFamily: 'monospace',
        fontSize: '18px',
        color: CSS.textBright,
        align: 'left',
        lineSpacing: 10,
      })
      .setOrigin(0.5);

    const close = this.add
      .text(0, h / 2 - 30, '[ close ]', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '16px',
        color: CSS.textDim,
      })
      .setOrigin(0.5)
      .setInteractive({ useHandCursor: true })
      .on('pointerdown', () => this.toggleControls());

    this.controlsPanel = this.add
      .container(cx, cy, [bg, heading, body, close])
      .setDepth(100)
      .setVisible(false);
  }

  private toggleControls(): void {
    if (!this.controlsPanel) return;
    this.controlsPanel.setVisible(!this.controlsPanel.visible);
  }

  private startGame(): void {
    this.registry.set('runState', createInitialRunState());
    this.cameras.main.fadeOut(250, 5, 7, 15);
    this.cameras.main.once(Phaser.Cameras.Scene2D.Events.FADE_OUT_COMPLETE, () => {
      this.scene.start(SCENES.GAME);
    });
  }
}
