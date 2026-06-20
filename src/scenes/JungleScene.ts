import Phaser from 'phaser';
import { SCENES, GAME_WIDTH, GAME_HEIGHT } from '../config/constants';
import { Player } from '../entities/Player';
import { DEFAULT_PLAYER_STATS } from '../config/playerStats';

// "Operation Greenfang" — Phase 1 prototype. A jungle sandbox to evaluate the
// isometric-ish 2.5D camera and full-body hero movement BEFORE building the
// strike systems and objectives. Reuses the existing Player (it owns its own
// WASD/Space keys, takes a pointer, and y-sorts via setDepth(this.y)).
//
// Art is procedural placeholder (layered green canopies + ground noise) so we
// can judge the camera feel without a download/asset pipeline. Depth model:
//   ground tile      -1000
//   ground detail     -900
//   shadows           -800
//   foliage + player  = their y (so lower-on-screen draws in front)
//   fireflies/overlay  fixed to camera, very high depth

const J_WORLD_W = 4200;
const J_WORLD_H = 3000;

const SKY = 0x0a1a0f;
const GROUND_BASE = 0x16331c;
const CANOPY_DARK = 0x123a1d;
const CANOPY_MID = 0x1d5329;
const CANOPY_LIGHT = 0x2f7a3c;
const TRUNK = 0x4a3422;

export class JungleScene extends Phaser.Scene {
  private player!: Player;
  private playerShadow!: Phaser.GameObjects.Image;
  private trunks!: Phaser.Physics.Arcade.StaticGroup;

  constructor() {
    super(SCENES.JUNGLE);
  }

  create(): void {
    this.generateTextures();
    this.cameras.main.setBackgroundColor(SKY);
    this.cameras.main.fadeIn(300, 5, 12, 7);
    this.physics.world.setBounds(0, 0, J_WORLD_W, J_WORLD_H);

    this.buildGround();
    this.trunks = this.physics.add.staticGroup();
    this.scatterFoliage();
    this.buildPlayer();
    this.buildAtmosphere();
    this.buildOverlay();

    this.input.keyboard!.addKey('ESC').on('down', () => {
      this.cameras.main.fadeOut(200, 0, 0, 0);
      this.time.delayedCall(200, () => this.scene.start(SCENES.MAIN_MENU));
    });
  }

  update(): void {
    this.player.update(this.input.activePointer);
    this.playerShadow.setPosition(this.player.x, this.player.y + 16);
  }

  // ---- world build ----------------------------------------------------------

  private buildGround(): void {
    this.add
      .tileSprite(0, 0, J_WORLD_W, J_WORLD_H, 'j_ground')
      .setOrigin(0, 0)
      .setDepth(-1000);

    // Scattered flat detail (grass tufts, dirt) lying on the floor.
    for (let i = 0; i < 260; i++) {
      const x = Phaser.Math.Between(0, J_WORLD_W);
      const y = Phaser.Math.Between(0, J_WORLD_H);
      const key = Phaser.Math.Between(0, 3) === 0 ? 'j_rock' : 'j_grass';
      this.add
        .image(x, y, key)
        .setDepth(-900)
        .setScale(Phaser.Math.FloatBetween(0.6, 1.3))
        .setAlpha(Phaser.Math.FloatBetween(0.5, 0.95))
        .setFlipX(Math.random() < 0.5);
    }
  }

  private scatterFoliage(): void {
    const cx = J_WORLD_W / 2;
    const cy = J_WORLD_H / 2;
    let placed = 0;
    let attempts = 0;
    while (placed < 90 && attempts < 600) {
      attempts++;
      const x = Phaser.Math.Between(80, J_WORLD_W - 80);
      const y = Phaser.Math.Between(120, J_WORLD_H - 60);
      // Keep a clearing around the spawn point.
      if (Phaser.Math.Distance.Between(x, y, cx, cy) < 360) continue;

      const isTree = Phaser.Math.Between(0, 2) > 0; // ~2/3 trees, 1/3 bushes
      const key = isTree ? 'j_tree' : 'j_bush';
      const scale = isTree
        ? Phaser.Math.FloatBetween(0.85, 1.35)
        : Phaser.Math.FloatBetween(0.7, 1.1);

      // Shadow first (sits on the ground beneath the foliage).
      this.add
        .image(x, y, 'shadow')
        .setDepth(-800)
        .setScale(scale * (isTree ? 1.6 : 1.1), scale * (isTree ? 0.8 : 0.6))
        .setAlpha(0.4);

      this.add
        .image(x, y, key)
        .setOrigin(0.5, 1)
        .setScale(scale)
        .setDepth(y)
        .setFlipX(Math.random() < 0.5);

      // Trees block movement at the trunk base; bushes are walk-through.
      if (isTree) {
        const trunk = this.add
          .rectangle(x, y - 6, 30 * scale, 14 * scale)
          .setVisible(false);
        this.trunks.add(trunk);
      }
      placed++;
    }
  }

  private buildPlayer(): void {
    const cx = J_WORLD_W / 2;
    const cy = J_WORLD_H / 2;

    this.playerShadow = this.add
      .image(cx, cy + 16, 'shadow')
      .setDepth(-800)
      .setScale(1.1, 0.6)
      .setAlpha(0.5);

    this.player = new Player(this, cx, cy, { ...DEFAULT_PLAYER_STATS });
    this.physics.add.collider(this.player, this.trunks);

    this.cameras.main.startFollow(this.player, true, 0.12, 0.12);
    this.cameras.main.setBounds(0, 0, J_WORLD_W, J_WORLD_H);
  }

  // Drifting pollen / fireflies fixed to the camera for foreground atmosphere.
  private buildAtmosphere(): void {
    this.add
      .particles(0, 0, 'dot', {
        x: { min: 0, max: GAME_WIDTH },
        y: { min: 0, max: GAME_HEIGHT },
        lifespan: 6000,
        speedX: { min: -12, max: 12 },
        speedY: { min: -18, max: 6 },
        scale: { min: 0.3, max: 0.9 },
        alpha: { start: 0.7, end: 0 }, // twinkle in and fade out
        frequency: 220,
        quantity: 1,
        tint: [0x9bff67, 0xd9ffa8, 0xfff0b0],
        blendMode: Phaser.BlendModes.ADD,
      })
      .setScrollFactor(0)
      .setDepth(9000);
  }

  private buildOverlay(): void {
    this.add
      .text(GAME_WIDTH / 2, 28, 'OPERATION GREENFANG', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '20px',
        fontStyle: 'bold',
        color: '#9bff67',
      })
      .setOrigin(0.5, 0)
      .setScrollFactor(0)
      .setDepth(9001)
      .setLetterSpacing(6)
      .setShadow(0, 0, '#000000', 8);

    this.add
      .text(GAME_WIDTH / 2, 56, 'PROTOTYPE · WASD move · Space dash · Esc menu', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '12px',
        color: '#cfe8d2',
      })
      .setOrigin(0.5, 0)
      .setScrollFactor(0)
      .setDepth(9001)
      .setAlpha(0.7)
      .setLetterSpacing(2);
  }

  // ---- procedural placeholder art -------------------------------------------

  private generateTextures(): void {
    this.makeGround();
    this.makeTree();
    this.makeBush();
    this.makeGrass();
    this.makeRock();
  }

  private makeGround(): void {
    const key = 'j_ground';
    if (this.textures.exists(key)) return;
    const size = 256;
    const tex = this.textures.createCanvas(key, size, size);
    if (!tex) return;
    const ctx = tex.getContext();
    const base = Phaser.Display.Color.IntegerToColor(GROUND_BASE);
    ctx.fillStyle = `rgb(${base.red},${base.green},${base.blue})`;
    ctx.fillRect(0, 0, size, size);
    // Speckled mottling: darker and lighter green dabs.
    const dabs: Array<[number, number]> = [
      [0x0f2814, 700],
      [0x1f5a2c, 500],
      [0x2a4a1c, 300],
    ];
    for (const [col, count] of dabs) {
      const c = Phaser.Display.Color.IntegerToColor(col);
      ctx.fillStyle = `rgba(${c.red},${c.green},${c.blue},0.5)`;
      for (let i = 0; i < count; i++) {
        const x = Math.random() * size;
        const y = Math.random() * size;
        const r = Math.random() * 3 + 0.6;
        ctx.beginPath();
        ctx.arc(x, y, r, 0, Math.PI * 2);
        ctx.fill();
      }
    }
    tex.refresh();
  }

  private makeTree(): void {
    const key = 'j_tree';
    if (this.textures.exists(key)) return;
    const w = 150;
    const h = 200;
    const g = this.add.graphics();
    // Trunk.
    g.fillStyle(TRUNK, 1);
    g.fillRect(w / 2 - 9, h - 70, 18, 70);
    g.fillStyle(0x3a2818, 1);
    g.fillRect(w / 2 - 9, h - 70, 6, 70);
    // Canopy: stacked overlapping circles, dark to light.
    const blobs: Array<[number, number, number, number]> = [
      [w / 2, 70, 58, CANOPY_DARK],
      [w / 2 - 34, 92, 42, CANOPY_DARK],
      [w / 2 + 34, 92, 42, CANOPY_DARK],
      [w / 2, 60, 48, CANOPY_MID],
      [w / 2 - 22, 80, 34, CANOPY_MID],
      [w / 2 + 22, 80, 34, CANOPY_MID],
      [w / 2 - 10, 56, 26, CANOPY_LIGHT],
      [w / 2 + 16, 64, 22, CANOPY_LIGHT],
    ];
    for (const [bx, by, br, col] of blobs) {
      g.fillStyle(col, 1);
      g.fillCircle(bx, by, br);
    }
    g.generateTexture(key, w, h);
    g.destroy();
  }

  private makeBush(): void {
    const key = 'j_bush';
    if (this.textures.exists(key)) return;
    const w = 100;
    const h = 76;
    const g = this.add.graphics();
    const blobs: Array<[number, number, number, number]> = [
      [w / 2, h - 22, 30, CANOPY_DARK],
      [w / 2 - 26, h - 16, 22, CANOPY_DARK],
      [w / 2 + 26, h - 16, 22, CANOPY_DARK],
      [w / 2, h - 30, 24, CANOPY_MID],
      [w / 2 - 14, h - 22, 18, CANOPY_LIGHT],
      [w / 2 + 16, h - 24, 14, CANOPY_LIGHT],
    ];
    for (const [bx, by, br, col] of blobs) {
      g.fillStyle(col, 1);
      g.fillCircle(bx, by, br);
    }
    g.generateTexture(key, w, h);
    g.destroy();
  }

  private makeGrass(): void {
    const key = 'j_grass';
    if (this.textures.exists(key)) return;
    const w = 44;
    const h = 36;
    const g = this.add.graphics();
    g.fillStyle(CANOPY_MID, 1);
    for (let i = 0; i < 6; i++) {
      const bx = 6 + i * 6;
      g.fillTriangle(bx, h, bx - 3, h - (10 + (i % 3) * 6), bx + 3, h);
    }
    g.fillStyle(CANOPY_LIGHT, 1);
    for (let i = 0; i < 4; i++) {
      const bx = 10 + i * 8;
      g.fillTriangle(bx, h, bx - 2, h - (8 + (i % 2) * 6), bx + 2, h);
    }
    g.generateTexture(key, w, h);
    g.destroy();
  }

  private makeRock(): void {
    const key = 'j_rock';
    if (this.textures.exists(key)) return;
    const w = 56;
    const h = 38;
    const g = this.add.graphics();
    g.fillStyle(0x44484a, 1);
    g.fillEllipse(w / 2, h / 2 + 4, 48, 26);
    g.fillStyle(0x5a5f61, 1);
    g.fillEllipse(w / 2 - 4, h / 2, 32, 16);
    g.generateTexture(key, w, h);
    g.destroy();
  }
}
