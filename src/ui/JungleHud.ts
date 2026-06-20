import Phaser from 'phaser';
import { COLORS, CSS, GAME_WIDTH, GAME_HEIGHT, DEPTH } from '../config/constants';
import { STRIKES, type StrikeType } from '../systems/StrikeSystem';

const D = DEPTH.hud;
const FONT = 'system-ui, sans-serif';

// Minimal HUD for Operation Greenfang: health, objective progress, and the two
// call-in strike indicators (armed highlight + cooldown sweep).
export class JungleHud {
  private scene: Phaser.Scene;
  private healthBar: Phaser.GameObjects.Graphics;
  private objLabel: Phaser.GameObjects.Text;
  private objBar: Phaser.GameObjects.Graphics;
  private strikeBoxes: Record<StrikeType, Phaser.GameObjects.Container>;
  private strikeCd: Record<StrikeType, Phaser.GameObjects.Graphics>;

  constructor(scene: Phaser.Scene) {
    this.scene = scene;

    // Health (top-left).
    scene.add
      .text(24, 20, 'INTEGRITY', { fontFamily: FONT, fontSize: '11px', color: CSS.textDim })
      .setScrollFactor(0)
      .setDepth(D)
      .setLetterSpacing(3);
    this.healthBar = scene.add.graphics().setScrollFactor(0).setDepth(D);

    // Objective (top-centre).
    this.objLabel = scene.add
      .text(GAME_WIDTH / 2, 20, '', { fontFamily: FONT, fontSize: '13px', color: CSS.textBright })
      .setOrigin(0.5, 0)
      .setScrollFactor(0)
      .setDepth(D)
      .setLetterSpacing(2);
    this.objBar = scene.add.graphics().setScrollFactor(0).setDepth(D);

    // Strike indicators (bottom-left).
    this.strikeBoxes = {
      artillery: this.buildStrikeBox(24, GAME_HEIGHT - 92, 'artillery'),
      air: this.buildStrikeBox(24 + 150, GAME_HEIGHT - 92, 'air'),
    };
    this.strikeCd = {
      artillery: scene.add.graphics().setScrollFactor(0).setDepth(D + 1),
      air: scene.add.graphics().setScrollFactor(0).setDepth(D + 1),
    };

    scene.add
      .text(24, GAME_HEIGHT - 24, 'Q switch  ·  Right-click call', {
        fontFamily: FONT,
        fontSize: '11px',
        color: CSS.textDim,
      })
      .setScrollFactor(0)
      .setDepth(D)
      .setLetterSpacing(2);
  }

  private buildStrikeBox(x: number, y: number, type: StrikeType): Phaser.GameObjects.Container {
    const w = 138;
    const h = 56;
    const def = STRIKES[type];
    const g = this.scene.add.graphics();
    g.fillStyle(COLORS.panel, 0.85);
    g.fillRoundedRect(0, 0, w, h, 8);
    g.lineStyle(2, def.color, 0.6);
    g.strokeRoundedRect(0, 0, w, h, 8);
    const label = this.scene.add
      .text(12, 10, def.label, {
        fontFamily: FONT,
        fontSize: '13px',
        fontStyle: 'bold',
        color: CSS.textBright,
      })
      .setLetterSpacing(1);
    const status = this.scene.add
      .text(12, 32, 'READY', { fontFamily: FONT, fontSize: '11px', color: CSS.textDim })
      .setName('status');
    return this.scene.add
      .container(x, y, [g, label, status])
      .setScrollFactor(0)
      .setDepth(D);
  }

  setHealth(cur: number, max: number): void {
    const w = 220;
    const h = 16;
    const x = 24;
    const y = 38;
    const pct = Phaser.Math.Clamp(cur / max, 0, 1);
    const g = this.healthBar;
    g.clear();
    g.fillStyle(COLORS.healthBack, 1);
    g.fillRoundedRect(x, y, w, h, 4);
    g.fillStyle(pct > 0.3 ? COLORS.pickup : COLORS.health, 1);
    g.fillRoundedRect(x, y, w * pct, h, 4);
  }

  setObjective(killed: number, target: number): void {
    this.objLabel.setText(`SECTOR HOSTILES  ${killed} / ${target}`);
    const w = 300;
    const h = 8;
    const x = GAME_WIDTH / 2 - w / 2;
    const y = 42;
    const pct = Phaser.Math.Clamp(killed / target, 0, 1);
    const g = this.objBar;
    g.clear();
    g.fillStyle(COLORS.panelEdge, 1);
    g.fillRoundedRect(x, y, w, h, 3);
    g.fillStyle(COLORS.gold, 1);
    g.fillRoundedRect(x, y, w * pct, h, 3);
  }

  // Update armed highlight + cooldown sweep each frame.
  setStrikes(
    armed: StrikeType,
    progress: Record<StrikeType, number>,
  ): void {
    (['artillery', 'air'] as StrikeType[]).forEach((type) => {
      const box = this.strikeBoxes[type];
      const def = STRIKES[type];
      const isArmed = type === armed;
      box.setScale(isArmed ? 1.04 : 1);
      box.setAlpha(isArmed ? 1 : 0.7);

      const status = box.getByName('status') as Phaser.GameObjects.Text;
      const ready = progress[type] >= 1;
      status.setText(ready ? (isArmed ? '◀ ARMED' : 'READY') : 'RELOADING');
      status.setColor(ready ? (isArmed ? CSS.gold : CSS.textBright) : CSS.textDim);

      // Cooldown shade over the box (shrinks as it fills).
      const g = this.strikeCd[type];
      g.clear();
      if (!ready) {
        const w = 138;
        const h = 56;
        g.fillStyle(0x000000, 0.55);
        g.fillRoundedRect(box.x, box.y + h * progress[type], w, h * (1 - progress[type]), 8);
      }
      // Armed accent border.
      if (isArmed) {
        g.lineStyle(2.5, def.color, 1);
        g.strokeRoundedRect(box.x - 2, box.y - 2, 142, 60, 9);
      }
    });
  }

  // Floating "N ELIMINATED" popup after a strike.
  showEliminated(n: number): void {
    if (n <= 0) return;
    const t = this.scene.add
      .text(GAME_WIDTH / 2, GAME_HEIGHT * 0.32, `${n} ELIMINATED`, {
        fontFamily: FONT,
        fontSize: '34px',
        fontStyle: 'bold',
        color: CSS.gold,
      })
      .setOrigin(0.5)
      .setScrollFactor(0)
      .setDepth(D + 5)
      .setShadow(0, 0, '#000', 10);
    t.setScale(0.6);
    this.scene.tweens.add({ targets: t, scale: 1, duration: 220, ease: 'Back.easeOut' });
    this.scene.tweens.add({
      targets: t,
      alpha: 0,
      y: GAME_HEIGHT * 0.26,
      delay: 700,
      duration: 600,
      onComplete: () => t.destroy(),
    });
  }

  // Big centre banner for phase callouts.
  banner(text: string, color: string = CSS.textBright): void {
    const t = this.scene.add
      .text(GAME_WIDTH / 2, GAME_HEIGHT * 0.42, text, {
        fontFamily: FONT,
        fontSize: '44px',
        fontStyle: 'bold',
        color,
      })
      .setOrigin(0.5)
      .setScrollFactor(0)
      .setDepth(D + 5)
      .setLetterSpacing(4)
      .setShadow(0, 0, '#000', 12)
      .setAlpha(0);
    this.scene.tweens.add({ targets: t, alpha: 1, duration: 300, yoyo: true, hold: 1400, onComplete: () => t.destroy() });
  }
}
