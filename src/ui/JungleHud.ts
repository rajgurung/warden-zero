import Phaser from 'phaser';
import { COLORS, CSS, GAME_WIDTH, GAME_HEIGHT, DEPTH } from '../config/constants';
import { STRIKES, type StrikeType } from '../systems/StrikeSystem';

const D = DEPTH.hud;
const FONT = 'system-ui, sans-serif';

// HUD for Operation Greenfang: health, objective callout, beacon capture bar,
// Warlord health bar, and the two call-in strike indicators (armed highlight,
// cooldown sweep, air-strike charges).
export class JungleHud {
  private scene: Phaser.Scene;
  private healthBar: Phaser.GameObjects.Graphics;
  private objLabel: Phaser.GameObjects.Text;
  private captureLabel: Phaser.GameObjects.Text;
  private captureBar: Phaser.GameObjects.Graphics;
  private warlordLabel: Phaser.GameObjects.Text;
  private warlordBar: Phaser.GameObjects.Graphics;
  private strikeBoxes: Record<StrikeType, Phaser.GameObjects.Container>;
  private strikeCd: Record<StrikeType, Phaser.GameObjects.Graphics>;

  constructor(scene: Phaser.Scene) {
    this.scene = scene;

    scene.add
      .text(24, 20, 'INTEGRITY', { fontFamily: FONT, fontSize: '11px', color: CSS.textDim })
      .setScrollFactor(0)
      .setDepth(D)
      .setLetterSpacing(3);
    this.healthBar = scene.add.graphics().setScrollFactor(0).setDepth(D);

    this.objLabel = scene.add
      .text(GAME_WIDTH / 2, 22, '', {
        fontFamily: FONT,
        fontSize: '15px',
        fontStyle: 'bold',
        color: CSS.textBright,
      })
      .setOrigin(0.5, 0)
      .setScrollFactor(0)
      .setDepth(D)
      .setLetterSpacing(3);

    // Warlord boss bar (hidden until set).
    this.warlordLabel = scene.add
      .text(GAME_WIDTH / 2, 48, 'JUNGLE WARLORD', {
        fontFamily: FONT,
        fontSize: '11px',
        color: '#ff8a7a',
      })
      .setOrigin(0.5, 0)
      .setScrollFactor(0)
      .setDepth(D)
      .setLetterSpacing(3)
      .setVisible(false);
    this.warlordBar = scene.add.graphics().setScrollFactor(0).setDepth(D).setVisible(false);

    // Beacon capture bar (centre, hidden until capturing).
    this.captureLabel = scene.add
      .text(GAME_WIDTH / 2, GAME_HEIGHT * 0.66, 'SECURING…', {
        fontFamily: FONT,
        fontSize: '13px',
        color: '#9bff67',
      })
      .setOrigin(0.5)
      .setScrollFactor(0)
      .setDepth(D)
      .setLetterSpacing(3)
      .setVisible(false);
    this.captureBar = scene.add.graphics().setScrollFactor(0).setDepth(D).setVisible(false);

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

  setObjective(text: string): void {
    this.objLabel.setText(text);
  }

  setCapture(progress: number | null): void {
    const show = progress !== null;
    this.captureLabel.setVisible(show);
    this.captureBar.setVisible(show);
    if (!show) return;
    const w = 260;
    const h = 10;
    const x = GAME_WIDTH / 2 - w / 2;
    const y = GAME_HEIGHT * 0.66 + 16;
    const g = this.captureBar;
    g.clear();
    g.fillStyle(COLORS.panelEdge, 1);
    g.fillRoundedRect(x, y, w, h, 4);
    g.fillStyle(COLORS.pickup, 1);
    g.fillRoundedRect(x, y, w * Phaser.Math.Clamp(progress, 0, 1), h, 4);
  }

  setWarlord(cur: number, max: number): void {
    this.warlordLabel.setVisible(true);
    this.warlordBar.setVisible(true);
    const w = 420;
    const h = 14;
    const x = GAME_WIDTH / 2 - w / 2;
    const y = 64;
    const pct = Phaser.Math.Clamp(cur / max, 0, 1);
    const g = this.warlordBar;
    g.clear();
    g.fillStyle(0x2a0e12, 1);
    g.fillRoundedRect(x, y, w, h, 4);
    g.fillStyle(COLORS.health, 1);
    g.fillRoundedRect(x, y, w * pct, h, 4);
  }

  clearWarlord(): void {
    this.warlordLabel.setVisible(false);
    this.warlordBar.setVisible(false);
  }

  setStrikes(
    armed: StrikeType,
    progress: Record<StrikeType, number>,
    airCharges: number,
  ): void {
    (['artillery', 'air'] as StrikeType[]).forEach((type) => {
      const box = this.strikeBoxes[type];
      const def = STRIKES[type];
      const isArmed = type === armed;
      box.setScale(isArmed ? 1.04 : 1);
      box.setAlpha(isArmed ? 1 : 0.7);

      const status = box.getByName('status') as Phaser.GameObjects.Text;
      const cdReady = progress[type] >= 1;
      const outOfCharges = type === 'air' && airCharges <= 0;
      let text: string;
      let color: string;
      if (outOfCharges) {
        text = 'NO CHARGES';
        color = CSS.textDim;
      } else if (!cdReady) {
        text = 'RELOADING';
        color = CSS.textDim;
      } else {
        text = isArmed ? '◀ ARMED' : 'READY';
        color = isArmed ? CSS.gold : CSS.textBright;
      }
      if (type === 'air') text += `  ×${airCharges}`;
      status.setText(text);
      status.setColor(color);

      const g = this.strikeCd[type];
      g.clear();
      const blocked = outOfCharges || !cdReady;
      if (blocked) {
        const shade = outOfCharges ? 0.6 : 0.55;
        const fillFrom = outOfCharges ? 0 : progress[type];
        g.fillStyle(0x000000, shade);
        g.fillRoundedRect(box.x, box.y + 56 * fillFrom, 138, 56 * (1 - fillFrom), 8);
      }
      if (isArmed) {
        g.lineStyle(2.5, def.color, 1);
        g.strokeRoundedRect(box.x - 2, box.y - 2, 142, 60, 9);
      }
    });
  }

  showEliminated(n: number): void {
    if (n <= 0) return;
    const t = this.scene.add
      .text(GAME_WIDTH / 2, GAME_HEIGHT * 0.3, `${n} ELIMINATED`, {
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
      y: GAME_HEIGHT * 0.24,
      delay: 700,
      duration: 600,
      onComplete: () => t.destroy(),
    });
  }

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
    this.scene.tweens.add({
      targets: t,
      alpha: 1,
      duration: 300,
      yoyo: true,
      hold: 1400,
      onComplete: () => t.destroy(),
    });
  }
}
