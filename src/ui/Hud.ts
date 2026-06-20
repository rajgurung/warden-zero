import Phaser from 'phaser';
import { COLORS, CSS, GAME_WIDTH, GAME_HEIGHT, DEPTH } from '../config/constants';

const D = DEPTH.hud;
const HP_PER_HEART = 20;
const MAX_HEARTS = 10;
const BAR_X = 24;
const BAR_Y = 52;
const BAR_W = 240;
const BAR_H = 16;
const ABILITY_SIZE = 56;
const ABILITY_Y = GAME_HEIGHT - 78;

// Fixed-to-camera overlay styled toward the mockup: hearts + health bar
// (top-left), wave (top-centre), score + coins (top-right), ability icons
// (bottom-left), weapon block (bottom-right).
export class Hud {
  private scene: Phaser.Scene;
  private hearts: Phaser.GameObjects.Image[] = [];
  private heartCount = 0;
  private healthBar: Phaser.GameObjects.Graphics;
  private healthText: Phaser.GameObjects.Text;
  private waveText: Phaser.GameObjects.Text;
  private levelText: Phaser.GameObjects.Text;
  private xpBar: Phaser.GameObjects.Graphics;
  private scoreText: Phaser.GameObjects.Text;
  private coinText: Phaser.GameObjects.Text;
  private dashCd: Phaser.GameObjects.Graphics;
  private bombCd: Phaser.GameObjects.Graphics;
  private bossBar: Phaser.GameObjects.Graphics;
  private bossLabel: Phaser.GameObjects.Text;

  constructor(scene: Phaser.Scene) {
    this.scene = scene;

    this.healthBar = scene.add.graphics().setScrollFactor(0).setDepth(D);
    this.healthText = scene.add
      .text(BAR_X + BAR_W + 12, BAR_Y + BAR_H / 2, '', {
        fontFamily: 'monospace',
        fontSize: '15px',
        color: CSS.textBright,
      })
      .setOrigin(0, 0.5)
      .setScrollFactor(0)
      .setDepth(D);

    this.waveText = scene.add
      .text(GAME_WIDTH / 2, 28, 'WAVE 1', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '26px',
        fontStyle: 'bold',
        color: CSS.textBright,
      })
      .setOrigin(0.5, 0)
      .setLetterSpacing(4)
      .setScrollFactor(0)
      .setDepth(D)
      .setShadow(0, 0, CSS.accent, 12, true, true);

    // XP bar + level under the wave label (the gem-collection progress loop).
    this.levelText = scene.add
      .text(GAME_WIDTH / 2 - 150, 64, 'LV 1', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '15px',
        fontStyle: 'bold',
        color: CSS.accent,
      })
      .setOrigin(1, 0.5)
      .setScrollFactor(0)
      .setDepth(D);
    this.xpBar = scene.add.graphics().setScrollFactor(0).setDepth(D);

    this.scoreText = scene.add
      .text(GAME_WIDTH - 24, 24, 'SCORE 0', {
        fontFamily: 'monospace',
        fontSize: '20px',
        color: CSS.gold,
      })
      .setOrigin(1, 0)
      .setScrollFactor(0)
      .setDepth(D);
    this.coinText = scene.add
      .text(GAME_WIDTH - 24, 52, 'COINS 0', {
        fontFamily: 'monospace',
        fontSize: '16px',
        color: CSS.textDim,
      })
      .setOrigin(1, 0)
      .setScrollFactor(0)
      .setDepth(D);

    this.buildAbilityIcon(scene, 24, 'SPACE', 'DASH');
    this.buildAbilityIcon(scene, 24 + ABILITY_SIZE + 14, 'RMB', 'BOMB');
    this.dashCd = scene.add.graphics().setScrollFactor(0).setDepth(D + 1);
    this.bombCd = scene.add.graphics().setScrollFactor(0).setDepth(D + 1);

    this.buildWeaponBlock(scene);

    // Boss health bar (hidden until a boss spawns).
    this.bossLabel = scene.add
      .text(GAME_WIDTH / 2, 92, 'BOSS — WARDEN COLOSSUS', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '16px',
        fontStyle: 'bold',
        color: CSS.magenta,
      })
      .setOrigin(0.5, 0.5)
      .setLetterSpacing(3)
      .setScrollFactor(0)
      .setDepth(D)
      .setVisible(false);
    this.bossBar = scene.add
      .graphics()
      .setScrollFactor(0)
      .setDepth(D)
      .setVisible(false);

    this.setHealth(100, 100);
  }

  setBoss(hp: number, max: number): void {
    this.bossLabel.setVisible(true);
    this.bossBar.setVisible(true);
    const w = 640;
    const h = 18;
    const x = GAME_WIDTH / 2 - w / 2;
    const y = 104;
    const pct = Phaser.Math.Clamp(hp / max, 0, 1);
    const g = this.bossBar;
    g.clear();
    g.fillStyle(COLORS.healthBack, 0.92);
    g.fillRoundedRect(x, y, w, h, 6);
    g.fillStyle(COLORS.health, 1);
    if (pct > 0) g.fillRoundedRect(x, y, Math.max(6, w * pct), h, 6);
    g.lineStyle(2, COLORS.magenta, 0.9);
    g.strokeRoundedRect(x, y, w, h, 6);
  }

  clearBoss(): void {
    this.bossLabel.setVisible(false);
    this.bossBar.setVisible(false);
    this.bossBar.clear();
  }

  setHealth(current: number, max: number): void {
    this.syncHearts(current, max);

    const pct = Phaser.Math.Clamp(current / max, 0, 1);
    const g = this.healthBar;
    g.clear();
    g.fillStyle(COLORS.healthBack, 0.9);
    g.fillRoundedRect(BAR_X, BAR_Y, BAR_W, BAR_H, 6);
    const fillColor = pct > 0.5 ? COLORS.pickup : COLORS.health;
    g.fillStyle(fillColor, 1);
    if (pct > 0) {
      g.fillRoundedRect(BAR_X, BAR_Y, Math.max(6, BAR_W * pct), BAR_H, 6);
    }
    g.lineStyle(2, COLORS.accent, 0.6);
    g.strokeRoundedRect(BAR_X, BAR_Y, BAR_W, BAR_H, 6);

    this.healthText.setText(`${Math.ceil(current)} / ${max}`);
  }

  // Heart icons fill/deplete; the row rebuilds if max health changes.
  private syncHearts(current: number, max: number): void {
    const total = Phaser.Math.Clamp(Math.round(max / HP_PER_HEART), 1, MAX_HEARTS);
    if (total !== this.heartCount) {
      this.hearts.forEach((h) => h.destroy());
      this.hearts = [];
      for (let i = 0; i < total; i++) {
        const heart = this.scene.add
          .image(BAR_X + 11 + i * 26, 26, 'heart')
          .setScale(0.85)
          .setScrollFactor(0)
          .setDepth(D);
        this.hearts.push(heart);
      }
      this.heartCount = total;
    }
    this.hearts.forEach((heart, i) => {
      const filled = current > i * HP_PER_HEART;
      heart.setTint(filled ? 0xffffff : COLORS.healthBack);
      heart.setAlpha(filled ? 1 : 0.6);
    });
  }

  setScore(score: number): void {
    this.scoreText.setText(`SCORE ${score.toLocaleString()}`);
  }

  setCoins(coins: number): void {
    this.coinText.setText(`COINS ${coins.toLocaleString()}`);
  }

  setWave(wave: number): void {
    this.waveText.setText(`WAVE ${wave}`);
  }

  // XP bar (centred under the wave label) + level number.
  setXp(xp: number, toNext: number, level: number): void {
    this.levelText.setText(`LV ${level}`);
    const w = 280;
    const h = 10;
    const x = GAME_WIDTH / 2 - w / 2;
    const y = 60;
    const pct = Phaser.Math.Clamp(xp / toNext, 0, 1);
    const g = this.xpBar;
    g.clear();
    g.fillStyle(COLORS.healthBack, 0.9);
    g.fillRoundedRect(x, y, w, h, 5);
    g.fillStyle(COLORS.accent, 1);
    if (pct > 0) g.fillRoundedRect(x, y, Math.max(5, w * pct), h, 5);
    g.lineStyle(1, COLORS.accent, 0.6);
    g.strokeRoundedRect(x, y, w, h, 5);
  }

  private buildAbilityIcon(
    scene: Phaser.Scene,
    x: number,
    key: string,
    label: string,
  ): void {
    const g = scene.add.graphics().setScrollFactor(0).setDepth(D);
    g.fillStyle(COLORS.panel, 0.85);
    g.fillRoundedRect(x, ABILITY_Y, ABILITY_SIZE, ABILITY_SIZE, 10);
    g.lineStyle(2, COLORS.accent, 0.7);
    g.strokeRoundedRect(x, ABILITY_Y, ABILITY_SIZE, ABILITY_SIZE, 10);

    scene.add
      .text(x + ABILITY_SIZE / 2, ABILITY_Y + ABILITY_SIZE / 2 - 6, key, {
        fontFamily: 'system-ui, sans-serif',
        fontSize: key.length > 2 ? '13px' : '24px',
        fontStyle: 'bold',
        color: CSS.textBright,
      })
      .setOrigin(0.5)
      .setScrollFactor(0)
      .setDepth(D);
    scene.add
      .text(x + ABILITY_SIZE / 2, ABILITY_Y + ABILITY_SIZE - 12, label, {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '10px',
        color: CSS.textDim,
      })
      .setOrigin(0.5)
      .setScrollFactor(0)
      .setDepth(D)
      .setLetterSpacing(2);
  }

  // Static stylised weapon panel (no ammo mechanic in MVP).
  private buildWeaponBlock(scene: Phaser.Scene): void {
    const w = 168;
    const h = 56;
    const x = GAME_WIDTH - 24 - w;
    const y = GAME_HEIGHT - 78;
    const g = scene.add.graphics().setScrollFactor(0).setDepth(D);
    g.fillStyle(COLORS.panel, 0.85);
    g.fillRoundedRect(x, y, w, h, 10);
    g.lineStyle(2, COLORS.accent, 0.6);
    g.strokeRoundedRect(x, y, w, h, 10);

    scene.add
      .text(x + 14, y + 12, 'WARDEN SMG', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '14px',
        fontStyle: 'bold',
        color: CSS.textBright,
      })
      .setScrollFactor(0)
      .setDepth(D)
      .setLetterSpacing(2);
    scene.add
      .text(x + 14, y + 32, 'AMMO  ∞', {
        fontFamily: 'monospace',
        fontSize: '14px',
        color: CSS.gold,
      })
      .setScrollFactor(0)
      .setDepth(D);
  }

  setAbilityCooldowns(dash: number, bomb: number): void {
    this.drawCooldown(this.dashCd, 24, dash);
    this.drawCooldown(this.bombCd, 24 + ABILITY_SIZE + 14, bomb);
  }

  private drawCooldown(
    g: Phaser.GameObjects.Graphics,
    x: number,
    progress: number,
  ): void {
    g.clear();
    if (progress >= 1) return;
    const hgt = ABILITY_SIZE * (1 - progress);
    g.fillStyle(COLORS.bgDeep, 0.7);
    g.fillRoundedRect(x, ABILITY_Y + (ABILITY_SIZE - hgt), ABILITY_SIZE, hgt, 10);
  }
}
