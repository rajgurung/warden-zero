import Phaser from 'phaser';
import { SCENES, COLORS, CSS, GAME_WIDTH, GAME_HEIGHT } from '../config/constants';
import { createButton } from '../ui/Button';
import { createPanel } from '../ui/Panel';
import { createInitialRunState } from '../config/playerStats';
import type { RunState } from '../types/game';

type GameOverData = { runState: RunState; retryScene?: string };

export class GameOverScene extends Phaser.Scene {
  constructor() {
    super(SCENES.GAME_OVER);
  }

  private retryScene: string = SCENES.GAME;

  create(data: GameOverData): void {
    const run = data.runState;
    this.retryScene = data.retryScene ?? SCENES.GAME;
    this.input.setDefaultCursor('default');
    const cx = GAME_WIDTH / 2;

    this.add
      .rectangle(0, 0, GAME_WIDTH, GAME_HEIGHT, COLORS.bgDeep, 0.78)
      .setOrigin(0, 0);

    createPanel(this, cx, GAME_HEIGHT / 2, 560, 470, { accent: COLORS.magenta });

    this.add
      .text(cx, GAME_HEIGHT * 0.26, 'GAME OVER', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '72px',
        fontStyle: 'bold',
        color: CSS.textBright,
      })
      .setOrigin(0.5)
      .setLetterSpacing(6)
      .setShadow(0, 0, CSS.magenta, 24, true, true);

    const stats = [
      `WAVE REACHED   ${run.currentWave}`,
      `SCORE          ${run.score.toLocaleString()}`,
      `KILLS          ${run.kills}`,
    ].join('\n');

    this.add
      .text(cx, GAME_HEIGHT * 0.46, stats, {
        fontFamily: 'monospace',
        fontSize: '22px',
        color: CSS.textBright,
        align: 'center',
        lineSpacing: 12,
      })
      .setOrigin(0.5);

    createButton(this, cx, GAME_HEIGHT * 0.7, 'RETRY', () => this.retry(), {
      accent: COLORS.accent,
    });
    createButton(
      this,
      cx,
      GAME_HEIGHT * 0.7 + 76,
      'MAIN MENU',
      () => this.scene.start(SCENES.MAIN_MENU),
      { accent: COLORS.panelEdge },
    );
  }

  private retry(): void {
    this.registry.set('runState', createInitialRunState());
    this.scene.start(this.retryScene);
  }
}
