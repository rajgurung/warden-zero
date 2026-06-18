import Phaser from 'phaser';
import { SCENES, COLORS, CSS, GAME_WIDTH, GAME_HEIGHT } from '../config/constants';
import { createUpgradeCard } from '../ui/UpgradeCard';
import { createPanel } from '../ui/Panel';
import { UpgradeSystem } from '../systems/UpgradeSystem';
import type { Upgrade } from '../config/upgrades';
import type { RunState } from '../types/game';

type UpgradeData = { runState: RunState; level: number };

// Level-up overlay. Launched on top of a paused GameScene when gem XP fills the
// bar; picking a card applies the upgrade and resumes the wave.
export class UpgradeScene extends Phaser.Scene {
  private run!: RunState;
  private picked = false;

  constructor() {
    super(SCENES.UPGRADE);
  }

  create(data: UpgradeData): void {
    this.run = data.runState;
    this.picked = false;

    this.add
      .rectangle(0, 0, GAME_WIDTH, GAME_HEIGHT, COLORS.bgDeep, 0.82)
      .setOrigin(0, 0);

    createPanel(this, GAME_WIDTH / 2, GAME_HEIGHT * 0.42, 1060, 540);

    this.add
      .text(GAME_WIDTH / 2, GAME_HEIGHT * 0.18, 'LEVEL UP', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '40px',
        fontStyle: 'bold',
        color: CSS.textBright,
      })
      .setOrigin(0.5)
      .setLetterSpacing(6)
      .setShadow(0, 0, CSS.accent, 18, true, true);

    this.add
      .text(
        GAME_WIDTH / 2,
        GAME_HEIGHT * 0.18 + 46,
        `LEVEL ${data.level} — CHOOSE AN UPGRADE`,
        {
          fontFamily: 'monospace',
          fontSize: '16px',
          color: CSS.textDim,
        },
      )
      .setOrigin(0.5)
      .setLetterSpacing(4);

    const choices = UpgradeSystem.pickThree(this.run);
    const spacing = 300;
    const startX = GAME_WIDTH / 2 - ((choices.length - 1) * spacing) / 2;
    const cardY = GAME_HEIGHT * 0.55;

    choices.forEach((upgrade, i) => {
      createUpgradeCard(
        this,
        startX + i * spacing,
        cardY,
        upgrade,
        i,
        () => this.select(upgrade),
      );
    });

    // Number-key hotkeys.
    choices.forEach((upgrade, i) => {
      this.input.keyboard?.on(`keydown-${['ONE', 'TWO', 'THREE'][i]}`, () =>
        this.select(upgrade),
      );
    });
  }

  private select(upgrade: Upgrade): void {
    if (this.picked) return;
    this.picked = true;
    if (this.cache.audio.exists('upgrade_select')) {
      this.sound.play('upgrade_select', { volume: 0.5 });
    }
    UpgradeSystem.apply(this.run, upgrade.id);
    this.scene.stop();
    this.scene.resume(SCENES.GAME);
  }
}
