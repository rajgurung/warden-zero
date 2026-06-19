import Phaser from 'phaser';
import { SCENES, COLORS, CSS, GAME_WIDTH, GAME_HEIGHT } from '../config/constants';
import { createButton } from '../ui/Button';
import { createPanel } from '../ui/Panel';

// Overlay launched on top of a paused GameScene. Resume puts you straight
// back; Quit tears down the run and returns to the menu.
export class PauseScene extends Phaser.Scene {
  constructor() {
    super(SCENES.PAUSE);
  }

  create(): void {
    const cx = GAME_WIDTH / 2;

    this.add
      .rectangle(0, 0, GAME_WIDTH, GAME_HEIGHT, COLORS.bgDeep, 0.7)
      .setOrigin(0, 0);

    createPanel(this, cx, GAME_HEIGHT * 0.46, 520, 420);

    this.add
      .text(cx, GAME_HEIGHT * 0.3, 'PAUSED', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '64px',
        fontStyle: 'bold',
        color: CSS.textBright,
      })
      .setOrigin(0.5)
      .setLetterSpacing(8)
      .setShadow(0, 0, CSS.accent, 20, true, true);

    this.add
      .text(cx, GAME_HEIGHT * 0.3 + 60, 'WASD move · Mouse aim · Click shoot · Space dash · E bomb', {
        fontFamily: 'monospace',
        fontSize: '15px',
        color: CSS.textDim,
      })
      .setOrigin(0.5);

    createButton(this, cx, GAME_HEIGHT * 0.55, 'RESUME', () => this.resumeGame(), {
      accent: COLORS.accent,
    });
    createButton(
      this,
      cx,
      GAME_HEIGHT * 0.55 + 76,
      'QUIT TO MENU',
      () => this.quit(),
      { accent: COLORS.panelEdge },
    );

    this.input.keyboard?.on('keydown-ESC', () => this.resumeGame());
  }

  private resumeGame(): void {
    this.scene.stop();
    this.scene.resume(SCENES.GAME);
  }

  private quit(): void {
    this.scene.stop(SCENES.GAME);
    this.scene.start(SCENES.MAIN_MENU);
  }
}
