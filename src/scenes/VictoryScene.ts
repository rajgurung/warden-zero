import Phaser from 'phaser';
import { SCENES, COLORS, CSS, GAME_WIDTH, GAME_HEIGHT } from '../config/constants';
import { createButton } from '../ui/Button';
import { createPanel } from '../ui/Panel';
import { createInitialRunState } from '../config/playerStats';
import type { RunState } from '../types/game';

type VictoryData = { runState: RunState };

export class VictoryScene extends Phaser.Scene {
  constructor() {
    super(SCENES.VICTORY);
  }

  create(data: VictoryData): void {
    const run = data.runState;
    const cx = GAME_WIDTH / 2;

    this.add
      .rectangle(0, 0, GAME_WIDTH, GAME_HEIGHT, COLORS.bgDeep, 0.8)
      .setOrigin(0, 0);

    createPanel(this, cx, GAME_HEIGHT / 2, 600, 500, { accent: COLORS.accent });

    // Triumphant hero glowing behind the panel title.
    this.add
      .image(cx, GAME_HEIGHT * 0.24, 'glow')
      .setScale(6, 4)
      .setTint(COLORS.accent)
      .setAlpha(0.3)
      .setBlendMode(Phaser.BlendModes.ADD);

    this.add
      .text(cx, GAME_HEIGHT * 0.24, 'VICTORY', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '80px',
        fontStyle: 'bold',
        color: CSS.textBright,
      })
      .setOrigin(0.5)
      .setLetterSpacing(8)
      .setShadow(0, 0, CSS.accent, 28, true, true);

    this.add
      .text(cx, GAME_HEIGHT * 0.38, 'All waves cleared, Warden.', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '20px',
        color: CSS.textDim,
      })
      .setOrigin(0.5);

    const stats = [
      `FINAL SCORE   ${run.score.toLocaleString()}`,
      `TOTAL KILLS   ${run.kills}`,
    ].join('\n');

    this.add
      .text(cx, GAME_HEIGHT * 0.52, stats, {
        fontFamily: 'monospace',
        fontSize: '22px',
        color: CSS.textBright,
        align: 'center',
        lineSpacing: 12,
      })
      .setOrigin(0.5);

    createButton(this, cx, GAME_HEIGHT * 0.74, 'PLAY AGAIN', () => this.again(), {
      accent: COLORS.accent,
    });
    createButton(
      this,
      cx,
      GAME_HEIGHT * 0.74 + 76,
      'MAIN MENU',
      () => this.scene.start(SCENES.MAIN_MENU),
      { accent: COLORS.panelEdge },
    );
  }

  private again(): void {
    this.registry.set('runState', createInitialRunState());
    this.scene.start(SCENES.GAME);
  }
}
