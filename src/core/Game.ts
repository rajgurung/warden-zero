import { Vector3, type InstancedMesh, type Mesh } from '../render/babylon';
import { COLORS } from '../config/constants';
import { ENEMY_CONFIGS, type EnemyConfig, type EnemyType } from '../config/enemies';
import { WAVES, FINAL_WAVE } from '../config/waves';
import { createInitialRunState } from '../config/playerStats';
import type { Upgrade } from '../config/upgrades';
import { PX, HALF_W, HALF_D, resolveCircle, pointInWall } from '../config/world';
import type { RunState } from '../types/game';
import { IS_TOUCH, hex, type Stage } from '../render/Stage';
import {
  buildEnemyTemplates,
  buildWarden,
  buildBolt,
  buildCritBolt,
  buildGem,
  buildHeart,
  buildCoin,
  type WardenModel,
} from '../render/models';
import type { Effects } from '../systems/Effects';
import type { Input } from '../systems/Input';
import { UpgradeSystem } from '../systems/UpgradeSystem';
import type { Hud } from '../ui/Hud';

// Tuning carried over from v1 (WaveSystem / EnemySpawnSystem / GameScene).
const SPAWN_INTERVAL = 0.16;
const SPAWN_BATCH = 3;
const MAX_ENEMIES = 80;
const SPAWN_MIN = 720 * PX;
const SPAWN_MAX = 920 * PX;
const PLAYER_R = 18 * PX;
const BULLET_LIFE = 0.9;
const HURT_INVULN = 0.7;
const CONTACT_COOLDOWN = 0.5;
const GEM_SCORE = 50;
const HEART_HEAL = 18;
const COIN_VALUE = 25;
const PICKUP_LIFE = 8;
const BOSS_SUMMON_EVERY = 4;
const AUTO_AIM_RANGE = 22;

type Enemy = {
  type: EnemyType;
  cfg: EnemyConfig;
  mesh: InstancedMesh;
  hp: number;
  r: number;
  speed: number;
  scale: number;
  grow: number;
  hit: number;
  lastContact: number;
  phase: number;
};
type Bullet = { mesh: InstancedMesh; vel: Vector3; life: number; damage: number; piercing: boolean; radius: number; hits: Set<Enemy> };
type Gem = { mesh: InstancedMesh; t: number; vel: Vector3 | null };
type Pickup = { mesh: InstancedMesh; kind: 'heart' | 'coin'; t: number };

type Mode = 'menu' | 'play' | 'upgrade' | 'paused' | 'dying' | 'won' | 'over';

export class Game {
  private mode: Mode = 'menu';
  private run!: RunState;
  private t = 0; // game clock (seconds), frozen while paused
  private enemies: Enemy[] = [];
  private bullets: Bullet[] = [];
  private gems: Gem[] = [];
  private pickups: Pickup[] = [];
  private queue: EnemyType[] = [];
  private spawnT = 0;
  private transitioning = false;
  private boss: Enemy | null = null;
  private bossSpawned = false;
  private bossSummonT = 0;
  private regenT = 0;
  private lastFired = -1;
  private invulnUntil = 0;
  private dashUntil = 0;
  private dashReadyAt = 0;
  private bombReadyAt = 0;
  private dashDir = new Vector3(0, 0, 1);
  private aimAngle = 0;
  private recoil = 0;
  private dieT = 0;
  private startedAt = 0;
  private timers: { at: number; fn: () => void }[] = [];

  private warden: WardenModel;
  private templates: Record<EnemyType, Mesh>;
  private boltT: Mesh;
  private critT: Mesh;
  private gemT: Mesh;
  private heartT: Mesh;
  private coinT: Mesh;

  constructor(
    private stage: Stage,
    private hud: Hud,
    private input: Input,
    private fx: Effects,
  ) {
    this.templates = buildEnemyTemplates(stage.scene);
    this.warden = buildWarden(stage);
    this.boltT = buildBolt(stage);
    this.critT = buildCritBolt(stage);
    this.gemT = buildGem(stage);
    this.heartT = buildHeart(stage);
    this.coinT = buildCoin(stage);
    this.warden.root.setEnabled(false);

    stage.scene.onBeforeRenderObservable.add(() => {
      const dt = Math.min(stage.engine.getDeltaTime() / 1000, 0.05);
      this.frame(dt);
    });
    addEventListener('keydown', (e) => this.onKey(e));
  }

  get playing(): boolean {
    return this.mode === 'play';
  }

  // ---------------------------------------------------------------- flow

  start(): void {
    this.clearWorld();
    this.run = createInitialRunState();
    this.t = 0;
    this.startedAt = performance.now();
    this.transitioning = false;
    this.boss = null;
    this.bossSpawned = false;
    this.lastFired = -1;
    this.invulnUntil = this.dashUntil = this.dashReadyAt = this.bombReadyAt = 0;
    this.timers = [];
    const w = this.warden;
    w.root.setEnabled(true);
    w.root.position.set(0, 0, 0);
    w.root.rotation.set(0, 0, 0);
    this.stage.follow(w.root.position, 0, true);
    this.input.clearPresses();
    this.mode = 'play';
    this.hud.setBoss(null);
    this.refreshHud();
    document.body.classList.add('in-run');
    this.hud.el.reticle.hidden = IS_TOUCH;

    if (new URLSearchParams(location.search).has('boss')) {
      this.run.currentWave = FINAL_WAVE;
      this.refreshHud();
      this.startBossFight();
    } else {
      this.beginWave(this.run.currentWave);
    }
  }

  quitToMenu(): void {
    this.clearWorld();
    this.mode = 'menu';
    this.warden.root.setEnabled(false);
    document.body.classList.remove('in-run');
    this.hud.el.reticle.hidden = true;
  }

  pause(on: boolean): void {
    if (on && this.mode !== 'play') return;
    if (!on && this.mode !== 'paused') return;
    this.mode = on ? 'paused' : 'play';
    this.hud.el.pause.hidden = !on;
    this.input.clearPresses();
    document.body.classList.toggle('in-run', !on);
  }

  private onKey(e: KeyboardEvent): void {
    if (e.code === 'Escape') {
      if (this.mode === 'play') this.pause(true);
      else if (this.mode === 'paused') this.pause(false);
    }
    if (this.mode === 'upgrade' && /^Digit[123]$/.test(e.code)) {
      const card = this.hud.el.cards.children[Number(e.code.slice(-1)) - 1] as HTMLElement | undefined;
      card?.click();
    }
  }

  private after(sec: number, fn: () => void): void {
    this.timers.push({ at: this.t + sec, fn });
  }

  private clearWorld(): void {
    for (const e of this.enemies) this.disposeEnemy(e);
    for (const o of [...this.bullets, ...this.gems, ...this.pickups]) o.mesh.dispose();
    this.enemies = [];
    this.bullets = [];
    this.gems = [];
    this.pickups = [];
    this.queue = [];
  }

  private refreshHud(): void {
    const s = this.run.playerStats;
    this.hud.setHealth(s.health, s.maxHealth);
    this.hud.setWave(this.run.currentWave, FINAL_WAVE);
    this.hud.setScore(this.run.score, this.run.coins);
    this.hud.setXp(this.run.xp, this.run.xpToNext, this.run.level);
  }

  private beginWave(n: number): void {
    this.transitioning = false;
    this.hud.setWave(n, FINAL_WAVE);
    this.hud.banner(`WAVE ${n}`);
    this.fx.sound.play('wave_start', 0.4);
    const wave = WAVES[n - 1];
    this.queue = [];
    if (wave) for (const g of wave.enemies) for (let i = 0; i < g.count; i++) this.queue.push(g.type);
    for (let i = this.queue.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [this.queue[i], this.queue[j]] = [this.queue[j], this.queue[i]];
    }
    this.spawnT = 0.4;
  }

  private checkWaveCleared(): void {
    if (this.transitioning || this.queue.length || this.enemies.length || this.gems.length) return;
    if (this.run.currentWave >= FINAL_WAVE) {
      if (!this.bossSpawned) this.startBossFight();
      return;
    }
    this.transitioning = true;
    this.hud.banner('WAVE CLEAR');
    this.after(1.4, () => {
      this.run.currentWave += 1;
      for (const b of this.bullets) b.mesh.dispose();
      this.bullets = [];
      this.warden.root.position.set(0, 0, 0);
      this.beginWave(this.run.currentWave);
    });
  }

  private startBossFight(): void {
    this.bossSpawned = true;
    this.hud.banner('COLOSSUS INBOUND', true);
    this.fx.sound.play('wave_start', 0.6);
    this.stage.addShake(0.6);
    this.boss = this.spawn('boss');
    this.bossSummonT = BOSS_SUMMON_EVERY;
    if (this.boss) this.hud.setBoss(this.boss.hp, this.boss.cfg.maxHealth);
  }

  private levelUp(): void {
    const run = this.run;
    run.xp -= run.xpToNext;
    run.level += 1;
    run.xpToNext = 8 + (run.level - 1) * 4;
    this.hud.setXp(run.xp, run.xpToNext, run.level);
    this.fx.sound.play('upgrade_select', 0.4);
    const choices = UpgradeSystem.pickThree(run);
    if (choices.length === 0) {
      this.heal(25);
      return;
    }
    this.mode = 'upgrade';
    this.input.clearPresses();
    document.body.classList.remove('in-run');
    this.hud.showUpgrades(run.level, choices, (u) => UpgradeSystem.stacks(run, u.id), (u) => this.pickUpgrade(u));
  }

  private pickUpgrade(u: Upgrade): void {
    UpgradeSystem.apply(this.run, u.id);
    this.hud.el.upgrade.hidden = true;
    this.fx.sound.play('upgrade_select', 0.5);
    this.mode = 'play';
    document.body.classList.add('in-run');
    this.refreshHud();
    // Spark ring around the Warden on pick.
    this.fx.burst(this.warden.root.position.add(new Vector3(0, 1, 0)), hexAccent(), 60, [3, 8]);
    // A level-up can be triggered mid gem pickup — catch chained level-ups.
    if (this.run.xp >= this.run.xpToNext) this.levelUp();
  }

  private gameOver(): void {
    this.mode = 'dying';
    this.dieT = 0;
    this.queue = [];
    this.fx.sound.play('game_over', 0.6);
    this.fx.burst(this.warden.root.position.add(new Vector3(0, 1, 0)), hexHealth(), 80, [4, 12]);
    document.body.classList.remove('in-run');
    this.hud.el.reticle.hidden = true;
  }

  private finish(win: boolean): void {
    this.mode = 'over';
    this.run.lifetimeMs = performance.now() - this.startedAt;
    this.hud.showResult(win, {
      wave: this.run.currentWave,
      score: this.run.score,
      kills: this.run.kills,
      level: this.run.level,
      ms: this.run.lifetimeMs,
    });
  }

  // ---------------------------------------------------------------- entities

  private spawn(type: EnemyType): Enemy | null {
    if (this.enemies.length >= MAX_ENEMIES && type !== 'boss') return null;
    const cfg = ENEMY_CONFIGS[type];
    const p = this.warden.root.position;
    const a = Math.random() * Math.PI * 2;
    const d = SPAWN_MIN + Math.random() * (SPAWN_MAX - SPAWN_MIN);
    const pos = { x: p.x + Math.cos(a) * d, z: p.z + Math.sin(a) * d };
    const r = cfg.radius * PX;
    pos.x = Math.max(-HALF_W + 40 * PX, Math.min(HALF_W - 40 * PX, pos.x));
    pos.z = Math.max(-HALF_D + 40 * PX, Math.min(HALF_D - 40 * PX, pos.z));
    resolveCircle(pos, r);
    const mesh = this.templates[type].createInstance(type);
    mesh.position.set(pos.x, 0, pos.z);
    mesh.scaling.setAll(0.01);
    this.stage.addCaster(mesh);
    const e: Enemy = {
      type, cfg, mesh, hp: cfg.maxHealth, r,
      speed: cfg.speed * PX,
      scale: (this.templates[type].metadata as { baseScale: number }).baseScale,
      grow: 0, hit: 0, lastContact: -9, phase: Math.random() * 10,
    };
    this.enemies.push(e);
    return e;
  }

  private disposeEnemy(e: Enemy): void {
    this.stage.shadows.removeShadowCaster(e.mesh, false);
    e.mesh.dispose();
  }

  private damageEnemy(e: Enemy, amount: number): void {
    e.hp -= amount;
    e.hit = 0.1;
    this.fx.sound.play('enemy_hit', 0.5, (Math.random() - 0.5) * 300);
    if (e === this.boss) this.hud.setBoss(e.hp, e.cfg.maxHealth);
    if (e.hp <= 0) this.killEnemy(e);
  }

  private killEnemy(e: Enemy): void {
    const i = this.enemies.indexOf(e);
    if (i < 0) return;
    this.enemies.splice(i, 1);
    const pos = e.mesh.position.clone();
    const big = e === this.boss;
    this.fx.enemyDeath(pos.add(new Vector3(0, e.r * 1.5, 0)), e.cfg.color, big);
    this.fx.sound.play('enemy_die', big ? 1 : 0.4, (Math.random() - 0.5) * 400);
    this.disposeEnemy(e);
    this.run.score += e.cfg.scoreValue;
    this.run.kills += 1;
    if (big) {
      this.boss = null;
      this.hud.setBoss(null);
      this.hud.banner('COLOSSUS DOWN');
      this.mode = 'won';
      this.dieT = 0;
      document.body.classList.remove('in-run');
      this.hud.el.reticle.hidden = true;
      this.hud.setScore(this.run.score, this.run.coins);
      return;
    }
    this.dropGem(pos);
    if (Math.random() <= 0.25) this.dropPickup(pos, Math.random() < 0.35 ? 'heart' : 'coin');
    if (this.run.playerStats.lifesteal > 0) this.heal(this.run.playerStats.lifesteal);
    this.hud.setScore(this.run.score, this.run.coins);
  }

  private dropGem(pos: Vector3): void {
    const mesh = this.gemT.createInstance('gem');
    mesh.position.set(pos.x, 0.6, pos.z);
    this.gems.push({ mesh, t: Math.random() * 6, vel: new Vector3((Math.random() - 0.5) * 3, 5, (Math.random() - 0.5) * 3) });
  }

  private dropPickup(pos: Vector3, kind: 'heart' | 'coin'): void {
    const mesh = (kind === 'heart' ? this.heartT : this.coinT).createInstance(kind);
    mesh.position.set(pos.x + (Math.random() - 0.5) * 0.8, 0.7, pos.z + (Math.random() - 0.5) * 0.8);
    this.pickups.push({ mesh, kind, t: 0 });
  }

  private heal(n: number): void {
    const s = this.run.playerStats;
    s.health = Math.min(s.maxHealth, s.health + n);
    this.hud.setHealth(s.health, s.maxHealth);
  }

  private hurt(n: number): void {
    const s = this.run.playerStats;
    s.health = Math.max(0, s.health - n);
    this.invulnUntil = this.t + HURT_INVULN;
    this.fx.playerHurt(this.warden.root.position);
    this.hud.setHealth(s.health, s.maxHealth);
    if (s.health <= 0) this.gameOver();
  }

  private fire(): void {
    const s = this.run.playerStats;
    const w = this.warden;
    w.root.computeWorldMatrix(true);
    const muzzle = Vector3.TransformCoordinates(w.muzzleLocal, w.root.getWorldMatrix());
    const spread = (8 * Math.PI) / 180;
    const start = -((s.bulletCount - 1) / 2) * spread;
    for (let i = 0; i < s.bulletCount; i++) {
      const ang = this.aimAngle + start + i * spread;
      const crit = Math.random() < s.critChance;
      const mesh = (crit ? this.critT : this.boltT).createInstance('b');
      mesh.position.copyFrom(muzzle);
      mesh.rotation.y = ang;
      mesh.scaling.setAll(s.bulletSize);
      this.bullets.push({
        mesh,
        vel: new Vector3(Math.sin(ang), 0, Math.cos(ang)).scale(s.bulletSpeed * PX),
        life: BULLET_LIFE,
        damage: crit ? Math.round(s.bulletDamage * s.critMult) : s.bulletDamage,
        piercing: s.bulletPiercing,
        radius: 0.15 * s.bulletSize,
        hits: new Set(),
      });
    }
    this.recoil = 1;
    this.stage.playerLight.intensity = 2.4;
    this.fx.sound.play('shoot', 0.22, (Math.random() - 0.5) * 200);
  }

  private tryDash(move: Vector3): void {
    const s = this.run.playerStats;
    if (this.t < this.dashReadyAt || this.t < this.dashUntil) return;
    const fwd = new Vector3(Math.sin(this.aimAngle), 0, Math.cos(this.aimAngle));
    this.dashDir = move.lengthSquared() > 0.01 ? move.normalizeToNew() : fwd;
    this.dashUntil = this.t + s.dashDurationMs / 1000;
    this.invulnUntil = Math.max(this.invulnUntil, this.dashUntil);
    this.dashReadyAt = this.t + s.dashCooldownMs / 1000;
    this.fx.burst(this.warden.root.position.add(new Vector3(0, 0.8, 0)), hexAccent(), 30, [2, 5]);
    this.fx.sound.play('dash', 0.5);
  }

  private tryBomb(): void {
    const s = this.run.playerStats;
    if (this.t < this.bombReadyAt) return;
    this.bombReadyAt = this.t + s.bombCooldownMs / 1000;
    const p = this.warden.root.position;
    const R = s.bombRadius * PX;
    this.fx.bombBlast(p, R);
    for (const e of [...this.enemies]) {
      const dx = e.mesh.position.x - p.x;
      const dz = e.mesh.position.z - p.z;
      if (Math.hypot(dx, dz) <= R + e.r) this.damageEnemy(e, s.bombDamage);
    }
  }

  // ---------------------------------------------------------------- frame

  private frame(dt: number): void {
    const w = this.warden;
    if (this.mode === 'menu') {
      // Slow attract-mode drift over the empty arena.
      const a = performance.now() / 9000;
      this.stage.follow(new Vector3(Math.sin(a) * 18, 0, Math.cos(a * 0.7) * 10), dt);
      return;
    }
    if (this.mode === 'dying' || this.mode === 'won') {
      this.dieT += dt;
      if (this.mode === 'dying') {
        w.body.rotation.x = Math.min(Math.PI / 2, this.dieT * 3);
        w.body.position.y = -Math.min(0.6, this.dieT * 1.2);
      }
      if (this.dieT > 1.1) this.finish(this.mode === 'won');
      this.animateEnemies(dt, false);
      this.stage.follow(w.root.position, dt);
      return;
    }
    if (this.mode !== 'play') {
      this.stage.follow(w.root.position, 0);
      return;
    }

    this.t += dt;
    const s = this.run.playerStats;

    for (let i = this.timers.length - 1; i >= 0; i--) {
      if (this.timers[i].at <= this.t) {
        const fn = this.timers[i].fn;
        this.timers.splice(i, 1);
        fn();
      }
    }

    // --- movement
    const move = this.input.move();
    if (this.input.consume('Space')) this.tryDash(move);
    if (this.input.consume('KeyE') || this.input.consume('Bomb')) this.tryBomb();
    const p = w.root.position;
    const dashing = this.t < this.dashUntil;
    const v = dashing ? this.dashDir.scale(s.dashSpeed * PX) : move.scale(s.speed * PX);
    const np = { x: p.x + v.x * dt, z: p.z + v.z * dt };
    resolveCircle(np, PLAYER_R);
    p.x = np.x;
    p.z = np.z;

    // --- aim
    let target: Vector3 | null = null;
    if (!IS_TOUCH) {
      this.input.updateAim();
      target = this.input.aim;
    } else {
      let best: Enemy | null = null;
      let bd = AUTO_AIM_RANGE * AUTO_AIM_RANGE;
      for (const e of this.enemies) {
        const d = Vector3.DistanceSquared(e.mesh.position, p);
        if (d < bd) { bd = d; best = e; }
      }
      if (best) target = best.mesh.position;
      else if (move.lengthSquared() > 0.01) target = p.add(move);
    }
    if (target) this.aimAngle = Math.atan2(target.x - p.x, target.z - p.z);
    let dr = this.aimAngle - w.root.rotation.y;
    dr = Math.atan2(Math.sin(dr), Math.cos(dr));
    w.root.rotation.y += dr * Math.min(1, dt * 20);

    // --- Warden animation
    const moving = move.lengthSquared() > 0.01 || dashing;
    const ts = this.t * (dashing ? 22 : 12);
    w.legL.rotation.x = moving ? Math.sin(ts) * 0.6 : 0;
    w.legR.rotation.x = moving ? -Math.sin(ts) * 0.6 : 0;
    w.body.position.y = moving ? Math.abs(Math.sin(ts)) * 0.06 : Math.sin(this.t * 2) * 0.012;
    w.body.rotation.x = dashing ? 0.35 : 0;
    this.recoil = Math.max(0, this.recoil - dt * 10);
    w.gun.position.z = 0.32 - this.recoil * 0.12;
    const blink = this.t < this.invulnUntil && !dashing && Math.floor(this.t * 20) % 2 === 0;
    for (const m of w.meshes) m.visibility = blink ? 0.35 : 1;

    // --- fire
    const wantsFire = IS_TOUCH ? this.enemies.length > 0 : this.input.fireHeld;
    if (wantsFire && this.t - this.lastFired >= s.fireRateMs / 1000) {
      this.lastFired = this.t;
      this.fire();
    }

    // --- regen
    this.regenT += dt;
    if (this.regenT >= 1) {
      this.regenT -= 1;
      if (s.regen > 0 && s.health < s.maxHealth) this.heal(s.regen);
    }

    // --- spawning
    if (this.queue.length) {
      this.spawnT -= dt;
      if (this.spawnT <= 0) {
        this.spawnT = SPAWN_INTERVAL;
        for (let i = 0; i < SPAWN_BATCH && this.queue.length; i++) {
          if (!this.spawn(this.queue[0])) break;
          this.queue.shift();
        }
      }
    }
    if (this.boss) {
      this.bossSummonT -= dt;
      if (this.bossSummonT <= 0) {
        this.bossSummonT = BOSS_SUMMON_EVERY;
        for (let i = 0; i < 4; i++) this.spawn('swarmer');
      }
    }

    this.animateEnemies(dt, true);
    this.updateBullets(dt);
    this.updateGems(dt);
    this.updatePickups(dt);
    if (this.mode === 'play') this.checkWaveCleared();

    this.hud.setCooldowns(
      this.cooldown(this.dashReadyAt, s.dashCooldownMs),
      this.cooldown(this.bombReadyAt, s.bombCooldownMs),
    );
    this.stage.playerLight.intensity += (0.7 - this.stage.playerLight.intensity) * Math.min(1, dt * 12);
    this.stage.follow(p, dt);
  }

  private cooldown(readyAt: number, ms: number): number {
    if (this.t >= readyAt) return 1;
    return 1 - (readyAt - this.t) / (ms / 1000);
  }

  private animateEnemies(dt: number, live: boolean): void {
    const p = this.warden.root.position;
    const time = performance.now() / 1000;
    const list = this.enemies;
    for (let i = 0; i < list.length; i++) {
      const e = list[i];
      const m = e.mesh;
      e.grow = Math.min(1, e.grow + dt * 5.5);
      // Back-ease pop-in like v1.
      const g = e.grow;
      const pop = g < 1 ? 1 + 2.2 * Math.pow(g - 1, 3) + 1.2 * Math.pow(g - 1, 2) : 1;
      const pulse = e.hit > 0 ? 1.12 : 1;
      e.hit = Math.max(0, e.hit - dt);
      m.scaling.setAll(e.scale * Math.max(0.01, pop) * pulse);
      if (!live) continue;

      // Steer toward the Warden with soft separation from neighbours.
      let dx = p.x - m.position.x;
      let dz = p.z - m.position.z;
      const d = Math.hypot(dx, dz) || 1;
      dx /= d;
      dz /= d;
      let sx = 0;
      let sz = 0;
      for (let j = 0; j < list.length; j++) {
        if (j === i) continue;
        const o = list[j];
        const ox = m.position.x - o.mesh.position.x;
        const oz = m.position.z - o.mesh.position.z;
        const min = e.r + o.r;
        const od2 = ox * ox + oz * oz;
        if (od2 < min * min && od2 > 1e-6) {
          const od = Math.sqrt(od2);
          const k = (min - od) / min;
          sx += (ox / od) * k * 2.5;
          sz += (oz / od) * k * 2.5;
        }
      }
      const pos = { x: m.position.x + (dx + sx) * e.speed * dt, z: m.position.z + (dz + sz) * e.speed * dt };
      resolveCircle(pos, e.r);
      m.position.x = pos.x;
      m.position.z = pos.z;
      m.rotation.y = Math.atan2(dx, dz);
      const gait = time * (6 + e.speed) + e.phase;
      m.rotation.z = Math.sin(gait) * (e.type === 'spider' ? 0.04 : 0.09);
      m.position.y = e.type === 'spider' ? 0 : Math.abs(Math.sin(gait)) * 0.08 * e.scale;

      // Contact damage.
      if (d < e.r + PLAYER_R && this.t >= this.invulnUntil && this.t - e.lastContact >= CONTACT_COOLDOWN) {
        e.lastContact = this.t;
        this.hurt(e.cfg.contactDamage);
        if (this.mode !== 'play') return;
      }
    }
  }

  private updateBullets(dt: number): void {
    for (let i = this.bullets.length - 1; i >= 0; i--) {
      const b = this.bullets[i];
      b.life -= dt;
      b.mesh.position.addInPlace(b.vel.scale(dt));
      const bp = b.mesh.position;
      let dead = b.life <= 0;
      if (!dead && pointInWall(bp.x, bp.z)) {
        this.fx.bulletImpact(bp);
        dead = true;
      }
      if (!dead) {
        for (const e of [...this.enemies]) {
          if (b.hits.has(e)) continue;
          const rr = e.r + b.radius;
          const dx = e.mesh.position.x - bp.x;
          const dz = e.mesh.position.z - bp.z;
          if (dx * dx + dz * dz > rr * rr) continue;
          b.hits.add(e);
          this.fx.bulletImpact(bp);
          this.damageEnemy(e, b.damage);
          if (!b.piercing) { dead = true; break; }
        }
      }
      if (dead) {
        b.mesh.dispose();
        this.bullets.splice(i, 1);
      }
    }
  }

  private updateGems(dt: number): void {
    const p = this.warden.root.position;
    const s = this.run.playerStats;
    const vacuum = this.queue.length === 0 && this.enemies.length === 0;
    for (let i = this.gems.length - 1; i >= 0; i--) {
      const g = this.gems[i];
      const m = g.mesh;
      g.t += dt;
      m.rotation.y += dt * 3;
      m.rotation.x += dt * 1.4;
      if (g.vel) {
        m.position.addInPlace(g.vel.scale(dt));
        g.vel.y -= 18 * dt;
        if (m.position.y <= 0.55) { m.position.y = 0.55; g.vel = null; }
        continue;
      }
      m.position.y = 0.55 + Math.sin(g.t * 4.8) * 0.12;
      const dx = p.x - m.position.x;
      const dz = p.z - m.position.z;
      const d = Math.hypot(dx, dz);
      if (vacuum || d < s.magnetRange * PX) {
        const step = Math.min(d, (vacuum ? 700 : 420) * PX * dt);
        m.position.x += (dx / d) * step;
        m.position.z += (dz / d) * step;
      }
      if (d < PLAYER_R + 0.35) {
        this.fx.burst(m.position, hexAccent(), 6, [1, 3], [0.05, 0.15]);
        m.dispose();
        this.gems.splice(i, 1);
        this.run.score += GEM_SCORE;
        this.run.xp += 1;
        this.hud.setScore(this.run.score, this.run.coins);
        this.fx.sound.play('pickup', 0.3, (Math.random() - 0.5) * 300);
        if (this.run.xp >= this.run.xpToNext) {
          this.levelUp();
          if (this.mode !== 'play') return;
        } else {
          this.hud.setXp(this.run.xp, this.run.xpToNext, this.run.level);
        }
      }
    }
  }

  private updatePickups(dt: number): void {
    const p = this.warden.root.position;
    for (let i = this.pickups.length - 1; i >= 0; i--) {
      const k = this.pickups[i];
      k.t += dt;
      k.mesh.rotation.y += dt * 2.5;
      k.mesh.position.y = 0.7 + Math.sin(k.t * 4.5) * 0.12;
      // Blink for the last second before expiring (instances can't fade, so toggle).
      k.mesh.isVisible = !(k.t > PICKUP_LIFE - 1 && Math.floor(k.t * 7) % 2 === 0);
      const d = Math.hypot(p.x - k.mesh.position.x, p.z - k.mesh.position.z);
      if (d < PLAYER_R + 0.4) {
        this.fx.sound.play('pickup', 0.5);
        if (k.kind === 'heart') this.heal(HEART_HEAL);
        else {
          this.run.coins += 1;
          this.run.score += COIN_VALUE;
          this.hud.setScore(this.run.score, this.run.coins);
        }
        this.fx.burst(k.mesh.position, k.kind === 'heart' ? hexHealth() : hexGold(), 16, [2, 5]);
      } else if (k.t < PICKUP_LIFE) continue;
      k.mesh.dispose();
      this.pickups.splice(i, 1);
    }
  }
}

const hexAccent = () => hex(COLORS.accent);
const hexHealth = () => hex(COLORS.health);
const hexGold = () => hex(COLORS.gold);
