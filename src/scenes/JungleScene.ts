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

// Operation Greenfang — a 3-minute jungle assault with a real arc:
// INSERTION -> PUSH (secure Beacon Alpha) -> ADVANCE (secure Beacon Bravo) ->
// WARLORD (kill the mini-boss) -> EXTRACTION (hold the LZ for 40s) -> VICTORY.
// Strikes are the star: artillery (cooldown) + air strike (cooldown + charges,
// rearmed at each beacon), both deal friendly-fire self-damage.

type Phase = 'insertion' | 'push' | 'advance' | 'warlord' | 'extraction' | 'ended';

const J_WORLD_W = 2800;
const J_WORLD_H = 1900;

const SKY = 0x0a1a0f;
const GROUND_BASE = 0x16331c;
const CANOPY_DARK = 0x123a1d;
const CANOPY_MID = 0x1d5329;
const CANOPY_LIGHT = 0x2f7a3c;
const TRUNK = 0x4a3422;

const PLAYER_START = { x: 480, y: 1520 };
// Capture beacons (A, B) then the extraction LZ (C), on a diagonal advance.
const BEACONS = [
  { x: 1050, y: 1240, name: 'ALPHA' },
  { x: 1700, y: 820, name: 'BRAVO' },
  { x: 2320, y: 430, name: 'LZ' },
];
const BEACON_RADIUS = 150;
const CAPTURE_MS = 4000;
const EXTRACTION_MS = 40000;
const SELF_DAMAGE = 25;

export class JungleScene extends Phaser.Scene {
  private player!: Player;
  private playerShadow!: Phaser.GameObjects.Image;
  private reticle!: Phaser.GameObjects.Image;
  private trunks!: Phaser.Physics.Arcade.StaticGroup;
  private enemies!: Phaser.Physics.Arcade.Group;
  private spits!: Phaser.Physics.Arcade.Group;
  private effects!: EffectsSystem;
  private weapon!: WeaponSystem;
  private strikes!: StrikeSystem;
  private hud!: JungleHud;

  private phase: Phase = 'insertion';
  private objectiveIndex = 0;
  private captureMs = 0;
  private spawnedThisPhase = 0;
  private killed = 0;
  private gameEnded = false;
  private startMs = 0;
  private extractionEndsAt = 0;
  private warlord: Enemy | null = null;
  private warlordPoundAt = 0;
  private warlordSummonAt = 0;
  private strikeKillAccum = 0;
  private strikePopup?: Phaser.Time.TimerEvent;

  private beaconRings: Phaser.GameObjects.Image[] = [];
  private arrow!: Phaser.GameObjects.Image;

  constructor() {
    super(SCENES.JUNGLE);
  }

  create(): void {
    this.physics.world.resume();
    this.phase = 'insertion';
    this.objectiveIndex = 0;
    this.captureMs = 0;
    this.spawnedThisPhase = 0;
    this.killed = 0;
    this.gameEnded = false;
    this.warlord = null;
    this.strikeKillAccum = 0;
    this.startMs = this.time.now;

    this.generateTextures();
    this.cameras.main.setBackgroundColor(SKY);
    this.cameras.main.fadeIn(300, 5, 12, 7);
    this.physics.world.setBounds(0, 0, J_WORLD_W, J_WORLD_H);

    this.buildGround();
    this.trunks = this.physics.add.staticGroup();
    this.scatterFoliage();
    this.buildBeacons();

    this.effects = new EffectsSystem(this);
    this.buildPlayer();
    this.weapon = new WeaponSystem(this, this.player, this.effects);
    this.enemies = this.physics.add.group();
    this.spits = this.physics.add.group();
    this.strikes = new StrikeSystem(
      this,
      this.effects,
      (x, y, r, dmg) => this.damageStrike(x, y, r, dmg),
      (x, y, r) => this.strikeSelfDamage(x, y, r),
    );

    this.hud = new JungleHud(this);
    this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);

    this.arrow = this.add
      .image(0, 0, 'arrow')
      .setScrollFactor(0)
      .setDepth(9400)
      .setTint(0x9bff67)
      .setVisible(false);

    this.setupColliders();
    this.setupInput();
    this.buildAtmosphere();

    // Insertion: brief calm, then the push begins.
    this.hud.banner('OPERATION GREENFANG', CSS.gold);
    this.hud.setObjective('INSERTION');
    this.time.delayedCall(2400, () => this.enterPush());

    this.time.addEvent({ delay: 1200, loop: true, callback: () => this.spawnTick() });
    this.time.addEvent({ delay: 1200, loop: true, callback: () => this.regenTick() });
  }

  update(_time: number, delta: number): void {
    if (this.gameEnded) return;
    const pointer = this.input.activePointer;
    this.player.update(pointer);
    this.reticle.setPosition(pointer.x, pointer.y);
    this.playerShadow.setPosition(this.player.x, this.player.y + 16);
    this.weapon.update(this.time.now, pointer);

    for (const obj of this.enemies.getChildren()) {
      const e = obj as Enemy;
      if (!e.active || e.getData('dead')) continue;
      if (e.config.type === 'spitter') this.updateSpitter(e);
      else e.chase(this.player.x, this.player.y);
    }

    this.updatePhase(delta);
    this.updateWaypoint();
    this.updateWarlord();

    this.strikes.update();
    this.hud.setStrikes(
      this.strikes.armed,
      {
        artillery: this.strikes.cooldownProgress('artillery'),
        air: this.strikes.cooldownProgress('air'),
      },
      this.strikes.airCharges,
    );
  }

  // ---- phase machine --------------------------------------------------------

  private enterPush(): void {
    this.phase = 'push';
    this.objectiveIndex = 0;
    this.spawnedThisPhase = 0;
    this.captureMs = 0;
    this.hud.banner('PHASE 1 · BREACH THE TREELINE', '#9bff67');
    this.hud.setObjective('SECURE BEACON ALPHA');
  }

  private enterAdvance(): void {
    this.phase = 'advance';
    this.objectiveIndex = 1;
    this.spawnedThisPhase = 0;
    this.captureMs = 0;
    this.hud.banner('PHASE 2 · CUT THE CORRIDOR', '#9bff67');
    this.hud.setObjective('SECURE BEACON BRAVO');
    this.beaconRings[1].setVisible(true);
  }

  private enterWarlord(): void {
    this.phase = 'warlord';
    this.hud.setCapture(null);
    this.hud.banner("WARLORD · GREENFANG'S CHAMPION", CSS.magenta);
    this.hud.setObjective('ELIMINATE THE WARLORD');
    // Spawn the Warlord between Bravo and the LZ, plus a small guard.
    const wp = { x: 1980, y: 640 };
    this.warlord = this.spawnEnemy('warlord', wp.x, wp.y);
    this.warlordPoundAt = this.time.now + 4000;
    this.warlordSummonAt = this.time.now + 6000;
    for (let i = 0; i < 6; i++) {
      this.spawnEnemy(
        'grunt',
        wp.x + Phaser.Math.Between(-140, 140),
        wp.y + Phaser.Math.Between(-120, 120),
      );
    }
  }

  private enterExtraction(): void {
    this.phase = 'extraction';
    this.objectiveIndex = 2;
    this.spawnedThisPhase = 0;
    this.warlord = null;
    this.hud.clearWarlord();
    this.extractionEndsAt = this.time.now + EXTRACTION_MS;
    this.hud.banner('EXTRACTION INBOUND · HOLD THE LZ', CSS.gold);
    this.beaconRings[2].setVisible(true);
    this.play('strike_ready', 0.6);
  }

  private updatePhase(delta: number): void {
    if (this.phase === 'push' || this.phase === 'advance') {
      this.updateCapture(delta);
    } else if (this.phase === 'extraction') {
      const remaining = Math.max(0, this.extractionEndsAt - this.time.now);
      const s = Math.ceil(remaining / 1000);
      this.hud.setObjective(`HOLD THE LZ · 0:${String(s).padStart(2, '0')}`);
      if (remaining <= 0) this.endGame(true);
    }
  }

  // Beacon capture: stand in the ring with no live enemy inside it.
  private updateCapture(delta: number): void {
    const b = BEACONS[this.objectiveIndex];
    const inRing =
      Phaser.Math.Distance.Between(this.player.x, this.player.y, b.x, b.y) < BEACON_RADIUS;
    const clear = inRing && !this.enemyInRing(b.x, b.y, BEACON_RADIUS);
    if (clear) this.captureMs += delta;
    else this.captureMs = Math.max(0, this.captureMs - delta * 1.5);

    if (this.captureMs >= CAPTURE_MS) {
      this.secureBeacon();
      return;
    }
    this.hud.setCapture(this.captureMs > 0 ? this.captureMs / CAPTURE_MS : inRing ? 0 : null);
  }

  private secureBeacon(): void {
    const b = BEACONS[this.objectiveIndex];
    this.hud.setCapture(null);
    this.hud.banner(`BEACON ${b.name} SECURED`, '#9bff67');
    this.play('strike_ready', 0.6);
    this.strikes.addAirCharges(2); // rearm at the objective
    this.effects.bombBlast(b.x, b.y, 60); // flare
    this.beaconRings[this.objectiveIndex].setVisible(false);

    if (this.phase === 'push') this.enterAdvance();
    else this.enterWarlord();
  }

  private enemyInRing(x: number, y: number, r: number): boolean {
    for (const obj of this.enemies.getChildren()) {
      const e = obj as Enemy;
      if (!e.active || e.getData('dead')) continue;
      if (Phaser.Math.Distance.Between(x, y, e.x, e.y) < r) return true;
    }
    return false;
  }

  // ---- waypoint -------------------------------------------------------------

  private currentObjectivePos(): { x: number; y: number } | null {
    if (this.phase === 'push') return BEACONS[0];
    if (this.phase === 'advance') return BEACONS[1];
    if (this.phase === 'warlord' && this.warlord?.active) {
      return { x: this.warlord.x, y: this.warlord.y };
    }
    if (this.phase === 'extraction') return BEACONS[2];
    return null;
  }

  private updateWaypoint(): void {
    const target = this.currentObjectivePos();
    if (!target) {
      this.arrow.setVisible(false);
      return;
    }
    const cam = this.cameras.main;
    const sx = target.x - cam.scrollX;
    const sy = target.y - cam.scrollY;
    const margin = 46;
    const onScreen =
      sx > margin && sx < GAME_WIDTH - margin && sy > margin && sy < GAME_HEIGHT - margin;
    if (onScreen) {
      this.arrow.setVisible(false);
      return;
    }
    const cx = GAME_WIDTH / 2;
    const cy = GAME_HEIGHT / 2;
    const ang = Math.atan2(sy - cy, sx - cx);
    const ex = Phaser.Math.Clamp(cx + Math.cos(ang) * 9999, margin, GAME_WIDTH - margin);
    const ey = Phaser.Math.Clamp(cy + Math.sin(ang) * 9999, margin, GAME_HEIGHT - margin);
    this.arrow.setVisible(true).setPosition(ex, ey).setRotation(ang + Math.PI / 2);
  }

  // ---- warlord --------------------------------------------------------------

  private updateWarlord(): void {
    const w = this.warlord;
    if (!w || !w.active || w.getData('dead')) return;
    w.chase(this.player.x, this.player.y);
    this.hud.setWarlord(w.health, ENEMY_CONFIGS.warlord.maxHealth);

    const now = this.time.now;
    if (now >= this.warlordPoundAt) {
      this.warlordPoundAt = now + 6000;
      this.warlordPound(w);
    }
    if (now >= this.warlordSummonAt) {
      this.warlordSummonAt = now + 8000;
      if (this.enemies.countActive(true) < 16) {
        for (let i = 0; i < 3; i++) {
          this.spawnEnemy(
            'grunt',
            w.x + Phaser.Math.Between(-90, 90),
            w.y + Phaser.Math.Between(-90, 90),
          );
        }
      }
    }
  }

  private warlordPound(w: Enemy): void {
    // Telegraph ring, then a radial shockwave knockback after 0.7s.
    const tell = this.add
      .image(w.x, w.y, 'ring')
      .setTint(0xff5a4a)
      .setAlpha(0.8)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(-650)
      .setScale(0.4);
    this.tweens.add({ targets: tell, scale: (200 / 64) * 1.0, duration: 700, ease: 'Cubic.easeOut' });
    this.time.delayedCall(700, () => {
      tell.destroy();
      if (!w.active || this.gameEnded) return;
      this.effects.bombBlast(w.x, w.y, 200);
      this.cameras.main.shake(260, 0.012);
      const d = Phaser.Math.Distance.Between(w.x, w.y, this.player.x, this.player.y);
      if (d < 200 && !this.player.isInvulnerable) {
        const a = Phaser.Math.Angle.Between(w.x, w.y, this.player.x, this.player.y);
        const body = this.player.body as Phaser.Physics.Arcade.Body;
        body.velocity.x += Math.cos(a) * 380;
        body.velocity.y += Math.sin(a) * 380;
        if (this.player.takeDamage(18)) {
          this.effects.playerHurt();
          this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);
          if (this.player.isDead) this.endGame(false);
        }
      }
    });
  }

  // ---- spitter --------------------------------------------------------------

  private updateSpitter(e: Enemy): void {
    const d = Phaser.Math.Distance.Between(e.x, e.y, this.player.x, this.player.y);
    const body = e.body as Phaser.Physics.Arcade.Body;
    if (d < 340) {
      body.setVelocity(0, 0);
      e.setFlipX(this.player.x < e.x);
      e.setDepth(e.y);
      const next = (e.getData('fireAt') as number) ?? 0;
      if (this.time.now >= next) {
        e.setData('fireAt', this.time.now + 2200);
        this.spitFire(e);
      }
    } else {
      e.chase(this.player.x, this.player.y);
    }
  }

  private spitFire(e: Enemy): void {
    e.setTintFill(0xffffff);
    this.time.delayedCall(160, () => {
      if (!e.active || e.getData('dead') || this.gameEnded) return;
      e.setTint(ENEMY_CONFIGS.spitter.tint!);
      const spit = this.spits.get(e.x, e.y, 'spit') as Phaser.Physics.Arcade.Image | null;
      if (!spit) return;
      spit.setActive(true).setVisible(true).setDepth(900);
      const a = Phaser.Math.Angle.Between(e.x, e.y, this.player.x, this.player.y);
      this.physics.velocityFromRotation(a, 300, (spit.body as Phaser.Physics.Arcade.Body).velocity);
      this.time.delayedCall(2600, () => {
        if (spit.active) {
          spit.destroy();
        }
      });
    });
  }

  private onSpitHitPlayer: Phaser.Types.Physics.Arcade.ArcadePhysicsCallback = (_p, spitObj) => {
    const spit = spitObj as Phaser.Physics.Arcade.Image;
    if (!spit.active) return;
    spit.destroy();
    if (this.player.isInvulnerable) return;
    if (this.player.takeDamage(12)) {
      this.effects.playerHurt();
      this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);
      if (this.player.isDead) this.endGame(false);
    }
  };

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
    if (fired) {
      this.cameras.main.shake(120, 0.004);
      this.hud.banner(
        this.strikes.armed === 'artillery' ? 'FIRE MISSION · ARTILLERY' : 'AIR SUPPORT INBOUND',
        this.strikes.armed === 'artillery' ? CSS.gold : CSS.accent,
      );
    }
  }

  private setupColliders(): void {
    this.physics.add.overlap(this.weapon.group, this.enemies, this.onBulletHitEnemy);
    this.physics.add.overlap(this.player, this.enemies, this.onPlayerHitEnemy);
    this.physics.add.overlap(this.player, this.spits, this.onSpitHitPlayer);
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

  private onPlayerHitEnemy: Phaser.Types.Physics.Arcade.ArcadePhysicsCallback = (_p, enemyObj) => {
    const enemy = enemyObj as Enemy;
    if (!enemy.active || enemy.getData('dead') || this.player.isInvulnerable) return;
    if (!enemy.canDealContactDamage(this.time.now)) return;
    if (!this.player.takeDamage(enemy.config.contactDamage)) return;
    this.effects.playerHurt();
    this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);
    if (this.player.isDead) this.endGame(false);
  };

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

  // Friendly fire: standing in a blast (and not dashing) hurts.
  private strikeSelfDamage(x: number, y: number, radius: number): void {
    if (this.gameEnded || this.player.isInvulnerable) return;
    if (Phaser.Math.Distance.Between(x, y, this.player.x, this.player.y) > radius) return;
    if (!this.player.takeDamage(SELF_DAMAGE)) return;
    this.effects.playerHurt();
    this.cameras.main.flash(120, 255, 80, 40);
    this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);
    if (this.player.isDead) this.endGame(false);
  }

  private killByStrike(enemy: Enemy, ix: number, iy: number): void {
    enemy.setData('dead', true);
    this.onEnemyKilled(enemy);
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
    this.onEnemyKilled(enemy);
    enemy.die();
  }

  private onEnemyKilled(enemy: Enemy): void {
    this.killed += 1;
    if (enemy.config.type === 'warlord') {
      this.warlord = null;
      this.hud.clearWarlord();
      this.effects.screenShake(500, 0.02);
      if (this.phase === 'warlord') this.enterExtraction();
    }
  }

  // ---- spawning -------------------------------------------------------------

  private spawnTick(): void {
    if (this.gameEnded) return;
    if (this.phase === 'push' || this.phase === 'advance') {
      const budget = this.phase === 'push' ? 14 : 24;
      const maxAlive = this.phase === 'push' ? 10 : 14;
      if (this.spawnedThisPhase >= budget) return;
      if (this.combatAlive() >= maxAlive) return;
      this.spawnClusterAhead(Phaser.Math.Between(3, 5));
    } else if (this.phase === 'extraction') {
      if (this.spawnedThisPhase >= 30) return;
      if (this.combatAlive() >= 16) return;
      this.spawnClusterAround(Phaser.Math.Between(3, 5));
    }
  }

  private combatAlive(): number {
    let n = 0;
    for (const obj of this.enemies.getChildren()) {
      const e = obj as Enemy;
      if (e.active && !e.getData('dead') && e.config.type !== 'warlord') n++;
    }
    return n;
  }

  // Spawn a cluster ahead of the player, toward the current objective.
  private spawnClusterAhead(n: number): void {
    const target = this.currentObjectivePos() ?? BEACONS[this.objectiveIndex];
    const ang = Phaser.Math.Angle.Between(this.player.x, this.player.y, target.x, target.y);
    const dist = Phaser.Math.Between(650, 880);
    const cx = this.player.x + Math.cos(ang) * dist;
    const cy = this.player.y + Math.sin(ang) * dist;
    this.spawnClusterAt(cx, cy, n);
  }

  private spawnClusterAround(n: number): void {
    const ang = Phaser.Math.FloatBetween(0, Math.PI * 2);
    const dist = Phaser.Math.Between(700, 900);
    const cx = this.player.x + Math.cos(ang) * dist;
    const cy = this.player.y + Math.sin(ang) * dist;
    this.spawnClusterAt(cx, cy, n);
  }

  private spawnClusterAt(cx: number, cy: number, n: number): void {
    for (let i = 0; i < n; i++) {
      const x = Phaser.Math.Clamp(cx + Phaser.Math.Between(-120, 120), 50, J_WORLD_W - 50);
      const y = Phaser.Math.Clamp(cy + Phaser.Math.Between(-100, 100), 90, J_WORLD_H - 50);
      this.spawnEnemy(this.pickType(), x, y);
      this.spawnedThisPhase += 1;
    }
  }

  private spawnEnemy(type: EnemyType, x: number, y: number): Enemy {
    const e = new Enemy(this, x, y, ENEMY_CONFIGS[type], this.effects);
    this.enemies.add(e);
    return e;
  }

  private pickType(): EnemyType {
    const r = Math.random();
    if (this.phase === 'push') return r < 0.7 ? 'grunt' : 'runner';
    if (this.phase === 'advance') {
      if (r < 0.4) return 'grunt';
      if (r < 0.65) return 'runner';
      if (r < 0.85) return 'brute';
      return 'spitter';
    }
    // extraction: everything
    if (r < 0.3) return 'grunt';
    if (r < 0.5) return 'runner';
    if (r < 0.68) return 'brute';
    if (r < 0.84) return 'spitter';
    return 'tank';
  }

  private regenTick(): void {
    if (this.gameEnded) return;
    const s = this.player.stats;
    if (s.health <= 0 || s.health >= s.maxHealth) return;
    this.player.heal(4);
    this.hud.setHealth(s.health, s.maxHealth);
  }

  // ---- end states -----------------------------------------------------------

  private endGame(won: boolean): void {
    if (this.gameEnded) return;
    this.gameEnded = true;
    this.phase = 'ended';
    this.input.setDefaultCursor('default');
    const elapsed = this.time.now - this.startMs;
    const runState = {
      ...createInitialRunState(),
      kills: this.killed,
      score: this.killed * 100,
      lifetimeMs: elapsed,
    };

    if (won) {
      this.hud.banner('EXTRACTION COMPLETE', '#9bff67');
      this.cameras.main.flash(400, 255, 255, 255);
      this.effects.screenShake(600, 0.02);
      this.time.delayedCall(1600, () =>
        this.scene.start(SCENES.VICTORY, {
          runState,
          subtitle: 'Extraction complete. The sector belongs to the Warden.',
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

  private play(key: string, volume: number): void {
    if (this.cache.audio.exists(key)) this.sound.play(key, { volume });
  }

  // ---- world build ----------------------------------------------------------

  private buildPlayer(): void {
    this.playerShadow = this.add
      .image(PLAYER_START.x, PLAYER_START.y + 16, 'shadow')
      .setDepth(-800)
      .setScale(1.1, 0.6)
      .setAlpha(0.5);
    this.player = new Player(this, PLAYER_START.x, PLAYER_START.y, {
      ...DEFAULT_PLAYER_STATS,
      maxHealth: 130,
      health: 130,
    });
    this.cameras.main.startFollow(this.player, true, 0.12, 0.12);
    this.cameras.main.setBounds(0, 0, J_WORLD_W, J_WORLD_H);
  }

  private buildBeacons(): void {
    this.beaconRings = BEACONS.map((b, i) => {
      const ring = this.add
        .image(b.x, b.y, 'ring')
        .setTint(i === 2 ? 0xffd75a : 0x9bff67)
        .setAlpha(0.8)
        .setBlendMode(Phaser.BlendModes.ADD)
        .setDepth(-700)
        .setScale((BEACON_RADIUS / 64) * 0.9)
        .setVisible(i === 0);
      this.tweens.add({
        targets: ring,
        scale: (BEACON_RADIUS / 64) * 1.0,
        alpha: 0.45,
        duration: 900,
        yoyo: true,
        repeat: -1,
        ease: 'Sine.easeInOut',
      });
      return ring;
    });
    // Bravo + LZ are revealed as the objective advances (enterAdvance / enterExtraction).
    this.beaconRings[1].setVisible(false);
    this.beaconRings[2].setVisible(false);
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
    let placed = 0;
    let attempts = 0;
    while (placed < 70 && attempts < 500) {
      attempts++;
      const x = Phaser.Math.Between(80, J_WORLD_W - 80);
      const y = Phaser.Math.Between(120, J_WORLD_H - 60);
      // Keep clearings around the player start and each beacon.
      if (Phaser.Math.Distance.Between(x, y, PLAYER_START.x, PLAYER_START.y) < 280) continue;
      if (BEACONS.some((b) => Phaser.Math.Distance.Between(x, y, b.x, b.y) < 220)) continue;
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
        const trunk = this.add.rectangle(x, y - 6, 30 * scale, 14 * scale).setVisible(false);
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
    this.makeSpit();
    this.makeArrow();
  }

  private makeSpit(): void {
    const key = 'spit';
    if (this.textures.exists(key)) return;
    const g = this.add.graphics();
    g.fillStyle(0x6fbf2e, 1);
    g.fillCircle(8, 8, 7);
    g.fillStyle(0xc8ff8a, 1);
    g.fillCircle(6, 6, 3);
    g.generateTexture(key, 16, 16);
    g.destroy();
  }

  private makeArrow(): void {
    const key = 'arrow';
    if (this.textures.exists(key)) return;
    const g = this.add.graphics();
    g.fillStyle(0xffffff, 1);
    g.fillTriangle(14, 0, 28, 28, 0, 28);
    g.generateTexture(key, 28, 28);
    g.destroy();
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
