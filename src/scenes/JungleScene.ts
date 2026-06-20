import Phaser from 'phaser';
import { SCENES, CSS, GAME_WIDTH, GAME_HEIGHT } from '../config/constants';
import { Player } from '../entities/Player';
import { DEFAULT_PLAYER_STATS, createInitialRunState } from '../config/playerStats';
import { Enemy } from '../entities/Enemy';
import { ENEMY_CONFIGS, type EnemyType } from '../config/enemies';
import { EffectsSystem } from '../systems/EffectsSystem';
import { WeaponSystem } from '../systems/WeaponSystem';
import { StrikeSystem } from '../systems/StrikeSystem';
import { Bullet } from '../entities/Bullet';
import { JungleHud } from '../ui/JungleHud';

// Operation Greenfang — jungle assault. Clear the sector of hostiles using your
// rifle plus call-in ARTILLERY and AIR STRIKES (Q to switch, right-click to
// call). 2.5D depth: the hero walks behind tall canopies. Reuses Player,
// WeaponSystem, EffectsSystem and the end screens.

const J_WORLD_W = 2800;
const J_WORLD_H = 1900;
const TARGET_KILLS = 60;
const MAX_ALIVE = 26;

const SKY = 0x0a1a0f;
const GROUND_BASE = 0x16331c;
const CANOPY_DARK = 0x123a1d;
const CANOPY_MID = 0x1d5329;
const CANOPY_LIGHT = 0x2f7a3c;
const TRUNK = 0x4a3422;

export class JungleScene extends Phaser.Scene {
  private player!: Player;
  private playerShadow!: Phaser.GameObjects.Image;
  private reticle!: Phaser.GameObjects.Image;
  private trunks!: Phaser.Physics.Arcade.StaticGroup;
  private enemies!: Phaser.Physics.Arcade.Group;
  private effects!: EffectsSystem;
  private weapon!: WeaponSystem;
  private strikes!: StrikeSystem;
  private hud!: JungleHud;

  private killed = 0;
  private spawned = 0;
  private gameEnded = false;
  private startMs = 0;
  private strikeKillAccum = 0;
  private strikePopup?: Phaser.Time.TimerEvent;
  private reinforced = false;

  constructor() {
    super(SCENES.JUNGLE);
  }

  create(): void {
    this.killed = 0;
    this.spawned = 0;
    this.gameEnded = false;
    this.reinforced = false;
    this.strikeKillAccum = 0;
    this.startMs = this.time.now;

    // Defensive: a hit-stop pause from a previous run could otherwise carry
    // over into this fresh entry and freeze everything.
    this.physics.world.resume();

    this.generateTextures();
    this.cameras.main.setBackgroundColor(SKY);
    this.cameras.main.fadeIn(300, 5, 12, 7);
    this.physics.world.setBounds(0, 0, J_WORLD_W, J_WORLD_H);

    this.buildGround();
    this.trunks = this.physics.add.staticGroup();
    this.scatterFoliage();

    this.effects = new EffectsSystem(this);
    this.buildPlayer();
    this.weapon = new WeaponSystem(this, this.player, this.effects);
    this.enemies = this.physics.add.group();
    this.strikes = new StrikeSystem(this, this.effects, (x, y, r, dmg) =>
      this.damageStrike(x, y, r, dmg),
    );

    this.hud = new JungleHud(this);
    this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);
    this.hud.setObjective(0, TARGET_KILLS);

    this.setupColliders();
    this.setupInput();
    this.buildAtmosphere();

    // Intro callouts + first spawn.
    this.hud.banner('OPERATION GREENFANG', CSS.gold);
    this.time.delayedCall(1900, () =>
      this.hud.banner('CLEAR THE SECTOR — STRIKES ONLINE', '#9bff67'),
    );

    this.time.addEvent({ delay: 1300, loop: true, callback: () => this.spawnTick() });
    this.spawnCluster(9);

    // Gentle out-of-the-arena regen (no upgrade economy here) so the no-regen
    // late mix isn't a one-mistake death.
    this.time.addEvent({
      delay: 1200,
      loop: true,
      callback: () => {
        if (this.gameEnded) return;
        const s = this.player.stats;
        if (s.health <= 0 || s.health >= s.maxHealth) return;
        this.player.heal(4);
        this.hud.setHealth(s.health, s.maxHealth);
      },
    });
  }

  update(): void {
    if (this.gameEnded) return;
    const pointer = this.input.activePointer;
    this.player.update(pointer);
    this.reticle.setPosition(pointer.x, pointer.y);
    this.playerShadow.setPosition(this.player.x, this.player.y + 16);
    this.weapon.update(this.time.now, pointer);

    for (const obj of this.enemies.getChildren()) {
      const e = obj as Enemy;
      if (e.active && !e.getData('dead')) e.chase(this.player.x, this.player.y);
    }

    this.strikes.update();
    this.hud.setStrikes(this.strikes.armed, {
      artillery: this.strikes.cooldownProgress('artillery'),
      air: this.strikes.cooldownProgress('air'),
    });

    // Anti-softlock: if every spawned hostile is gone but the kill count
    // somehow lagged the target, the sector is still clear — win.
    if (this.spawned >= TARGET_KILLS && this.enemies.countActive(true) === 0) {
      this.endGame(true);
    }
  }

  // ---- input ----------------------------------------------------------------

  private setupInput(): void {
    this.input.mouse?.disableContextMenu();
    this.input.setDefaultCursor('none');
    this.reticle = this.add
      .image(GAME_WIDTH / 2, GAME_HEIGHT / 2, 'reticle')
      .setScrollFactor(0)
      .setDepth(9500);

    this.input.keyboard!.addKey('Q').on('down', () => this.strikes.cycle());
    this.input.on('pointerdown', (p: Phaser.Input.Pointer) => {
      if (this.gameEnded) return;
      if (p.rightButtonDown()) this.tryStrike(p);
    });
    this.input.keyboard!.addKey('ESC').on('down', () => {
      if (this.gameEnded) return;
      this.input.setDefaultCursor('default');
      this.cameras.main.fadeOut(200, 0, 0, 0);
      this.time.delayedCall(200, () => this.scene.start(SCENES.MAIN_MENU));
    });
    this.events.once('shutdown', () => this.input.setDefaultCursor('default'));
  }

  private tryStrike(p: Phaser.Input.Pointer): void {
    const fired = this.strikes.fire(p.worldX, p.worldY, this.player.x, this.player.y);
    if (fired) this.cameras.main.shake(120, 0.004);
  }

  private setupColliders(): void {
    this.physics.add.overlap(this.weapon.group, this.enemies, this.onBulletHitEnemy);
    this.physics.add.overlap(this.player, this.enemies, this.onPlayerHitEnemy);
    this.physics.add.collider(this.player, this.trunks);
  }

  // ---- combat ---------------------------------------------------------------

  private onBulletHitEnemy: Phaser.Types.Physics.Arcade.ArcadePhysicsCallback = (
    bulletObj,
    enemyObj,
  ) => {
    const bullet = bulletObj as Bullet;
    const enemy = enemyObj as Enemy;
    if (!bullet.active || !enemy.active || enemy.getData('dead')) return;
    this.effects.bulletImpact(bullet.x, bullet.y);
    const lethal = enemy.takeDamage(bullet.damage);
    if (!bullet.piercing) bullet.deactivate();
    if (lethal) this.killEnemy(enemy);
  };

  private onPlayerHitEnemy: Phaser.Types.Physics.Arcade.ArcadePhysicsCallback = (
    _p,
    enemyObj,
  ) => {
    const enemy = enemyObj as Enemy;
    if (!enemy.active || enemy.getData('dead') || this.player.isInvulnerable) return;
    if (!enemy.canDealContactDamage(this.time.now)) return;
    if (!this.player.takeDamage(enemy.config.contactDamage)) return;
    this.effects.playerHurt();
    this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);
    if (this.player.isDead) this.endGame(false);
  };

  // Strike area damage: returns kills, launches the crowd, batches the popup.
  private damageStrike(x: number, y: number, radius: number, damage: number): number {
    let kills = 0;
    for (const obj of this.enemies.getChildren()) {
      const e = obj as Enemy;
      if (!e.active || e.getData('dead')) continue;
      if (Phaser.Math.Distance.Between(x, y, e.x, e.y) > radius) continue;
      if (e.takeDamage(damage)) {
        this.killByStrike(e, x, y);
        kills++;
      }
    }
    if (kills > 0) {
      this.strikeKillAccum += kills;
      this.strikePopup?.remove();
      this.strikePopup = this.time.delayedCall(1100, () => {
        this.hud.showEliminated(this.strikeKillAccum);
        this.strikeKillAccum = 0;
      });
    }
    return kills;
  }

  private killByStrike(enemy: Enemy, ix: number, iy: number): void {
    enemy.setData('dead', true);
    this.registerKill();
    const a = Math.atan2(enemy.y - iy, enemy.x - ix);
    (enemy.body as Phaser.Physics.Arcade.Body).enable = false;
    this.tweens.add({
      targets: enemy,
      x: enemy.x + Math.cos(a) * 90,
      y: enemy.y + Math.sin(a) * 90,
      angle: Phaser.Math.Between(-140, 140),
      alpha: 0.15,
      duration: 220,
      ease: 'Quad.easeOut',
      onComplete: () => enemy.die(),
    });
  }

  private killEnemy(enemy: Enemy): void {
    enemy.setData('dead', true);
    this.registerKill();
    enemy.die();
  }

  private registerKill(): void {
    this.killed += 1;
    this.hud.setObjective(this.killed, TARGET_KILLS);
    if (!this.reinforced && this.killed >= Math.floor(TARGET_KILLS * 0.55)) {
      this.reinforced = true;
      this.hud.banner('REINFORCEMENTS INBOUND', CSS.magenta);
    }
    if (this.killed >= TARGET_KILLS) this.endGame(true);
  }

  // ---- spawning -------------------------------------------------------------

  private spawnTick(): void {
    if (this.gameEnded || this.spawned >= TARGET_KILLS) return;
    if (this.enemies.countActive(true) >= MAX_ALIVE) return;
    const remaining = TARGET_KILLS - this.spawned;
    const base = Phaser.Math.Between(3, 6) + Math.floor(this.killed / 18);
    this.spawnCluster(Math.min(remaining, base));
  }

  private spawnCluster(n: number): void {
    const center = this.pickEdgePoint();
    for (let i = 0; i < n && this.spawned < TARGET_KILLS; i++) {
      const x = Phaser.Math.Clamp(
        center.x + Phaser.Math.Between(-120, 120),
        50,
        J_WORLD_W - 50,
      );
      const y = Phaser.Math.Clamp(
        center.y + Phaser.Math.Between(-100, 100),
        90,
        J_WORLD_H - 50,
      );
      const enemy = new Enemy(this, x, y, ENEMY_CONFIGS[this.pickType()], this.effects);
      this.enemies.add(enemy);
      this.spawned += 1;
    }
  }

  private pickType(): EnemyType {
    const r = Math.random();
    if (this.killed < 18) return r < 0.7 ? 'grunt' : 'runner';
    if (this.killed < 42) {
      if (r < 0.5) return 'grunt';
      if (r < 0.8) return 'runner';
      return 'brute';
    }
    if (r < 0.4) return 'runner';
    if (r < 0.7) return 'grunt';
    if (r < 0.9) return 'brute';
    return 'tank';
  }

  // A point near an edge, at least ~560px from the player. Reuses the previous
  // edge ~50% of the time so clusters merge into denser, strike-worthy crowds.
  private lastEdge = 0;
  private pickEdgePoint(): { x: number; y: number } {
    for (let i = 0; i < 8; i++) {
      const edge =
        i === 0 && Math.random() < 0.5 ? this.lastEdge : Phaser.Math.Between(0, 3);
      this.lastEdge = edge;
      let x = Phaser.Math.Between(120, J_WORLD_W - 120);
      let y = Phaser.Math.Between(120, J_WORLD_H - 120);
      if (edge === 0) y = 140;
      else if (edge === 1) y = J_WORLD_H - 140;
      else if (edge === 2) x = 140;
      else x = J_WORLD_W - 140;
      if (Phaser.Math.Distance.Between(x, y, this.player.x, this.player.y) > 560) {
        return { x, y };
      }
    }
    return { x: 140, y: 140 };
  }

  // ---- end states -----------------------------------------------------------

  private endGame(won: boolean): void {
    if (this.gameEnded) return;
    this.gameEnded = true;
    this.input.setDefaultCursor('default');
    const elapsed = this.time.now - this.startMs;
    const runState = {
      ...createInitialRunState(),
      kills: this.killed,
      score: this.killed * 100,
      lifetimeMs: elapsed,
    };

    if (won) {
      this.hud.banner('SECTOR SECURED', '#9bff67');
      this.effects.screenShake(500, 0.02);
      this.time.delayedCall(1500, () =>
        this.scene.start(SCENES.VICTORY, {
          runState,
          subtitle: 'The sector is clear. Extraction inbound, Warden.',
          againScene: SCENES.JUNGLE,
        }),
      );
    } else {
      this.player.die();
      this.time.delayedCall(1000, () =>
        this.scene.start(SCENES.GAME_OVER, { runState, retryScene: SCENES.JUNGLE }),
      );
    }
  }

  // ---- world build ----------------------------------------------------------

  private buildPlayer(): void {
    const cx = J_WORLD_W / 2;
    const cy = J_WORLD_H / 2;
    this.playerShadow = this.add
      .image(cx, cy + 16, 'shadow')
      .setDepth(-800)
      .setScale(1.1, 0.6)
      .setAlpha(0.5);
    // Jungle hero is a touch beefier (no upgrade/regen economy in this mode).
    this.player = new Player(this, cx, cy, {
      ...DEFAULT_PLAYER_STATS,
      maxHealth: 130,
      health: 130,
    });
    this.cameras.main.startFollow(this.player, true, 0.12, 0.12);
    this.cameras.main.setBounds(0, 0, J_WORLD_W, J_WORLD_H);
  }

  private buildGround(): void {
    this.add
      .tileSprite(0, 0, J_WORLD_W, J_WORLD_H, 'j_ground')
      .setOrigin(0, 0)
      .setDepth(-1000);
    for (let i = 0; i < 220; i++) {
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
    while (placed < 70 && attempts < 500) {
      attempts++;
      const x = Phaser.Math.Between(80, J_WORLD_W - 80);
      const y = Phaser.Math.Between(120, J_WORLD_H - 60);
      if (Phaser.Math.Distance.Between(x, y, cx, cy) < 320) continue;
      const isTree = Phaser.Math.Between(0, 2) > 0;
      const key = isTree ? 'j_tree' : 'j_bush';
      const scale = isTree
        ? Phaser.Math.FloatBetween(0.85, 1.35)
        : Phaser.Math.FloatBetween(0.7, 1.1);
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
      if (isTree) {
        const trunk = this.add
          .rectangle(x, y - 6, 30 * scale, 14 * scale)
          .setVisible(false);
        this.trunks.add(trunk);
      }
      placed++;
    }
  }

  private buildAtmosphere(): void {
    this.add
      .particles(0, 0, 'dot', {
        x: { min: 0, max: GAME_WIDTH },
        y: { min: 0, max: GAME_HEIGHT },
        lifespan: 6000,
        speedX: { min: -12, max: 12 },
        speedY: { min: -18, max: 6 },
        scale: { min: 0.3, max: 0.9 },
        alpha: { start: 0.6, end: 0 },
        frequency: 240,
        quantity: 1,
        tint: [0x9bff67, 0xd9ffa8, 0xfff0b0],
        blendMode: Phaser.BlendModes.ADD,
      })
      .setScrollFactor(0)
      .setDepth(9000);
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
    const dabs: Array<[number, number]> = [
      [0x0f2814, 700],
      [0x1f5a2c, 500],
      [0x2a4a1c, 300],
    ];
    for (const [col, count] of dabs) {
      const c = Phaser.Display.Color.IntegerToColor(col);
      ctx.fillStyle = `rgba(${c.red},${c.green},${c.blue},0.5)`;
      for (let i = 0; i < count; i++) {
        ctx.beginPath();
        ctx.arc(Math.random() * size, Math.random() * size, Math.random() * 3 + 0.6, 0, Math.PI * 2);
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
    g.fillStyle(TRUNK, 1);
    g.fillRect(w / 2 - 9, h - 70, 18, 70);
    g.fillStyle(0x3a2818, 1);
    g.fillRect(w / 2 - 9, h - 70, 6, 70);
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
