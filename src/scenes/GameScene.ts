import Phaser from 'phaser';
import { SCENES, COLORS, GAME_WIDTH, GAME_HEIGHT } from '../config/constants';
import { Player } from '../entities/Player';
import { Bullet } from '../entities/Bullet';
import { Enemy } from '../entities/Enemy';
import { Pickup, HEART_HEAL, COIN_VALUE, type PickupType } from '../entities/Pickup';
import { WeaponSystem } from '../systems/WeaponSystem';
import { EnemySpawnSystem } from '../systems/EnemySpawnSystem';
import { EffectsSystem } from '../systems/EffectsSystem';
import { WaveSystem } from '../systems/WaveSystem';
import { Hud } from '../ui/Hud';
import { createInitialRunState } from '../config/playerStats';
import { FINAL_WAVE } from '../config/waves';
import type { RunState } from '../types/game';

// The arena. Wires player, weapon, enemies, collisions and HUD into the core
// combat loop. (Continuous spawning here is replaced by the wave system in M6.)
export class GameScene extends Phaser.Scene {
  private run!: RunState;
  private player!: Player;
  private weapon!: WeaponSystem;
  private spawner!: EnemySpawnSystem;
  private effects!: EffectsSystem;
  private waves!: WaveSystem;
  private hud!: Hud;
  private walls!: Phaser.Physics.Arcade.StaticGroup;
  private pickups!: Phaser.Physics.Arcade.Group;
  private bombKey!: Phaser.Input.Keyboard.Key;
  private gameOver = false;
  private transitioning = false;

  // Obstacle layout (centre is kept clear for the player spawn).
  private static readonly WALL_DEFS = [
    { x: 500, y: 360, w: 44, h: 170 },
    { x: 780, y: 360, w: 44, h: 170 },
    { x: 360, y: 200, w: 130, h: 44 },
    { x: 920, y: 200, w: 130, h: 44 },
    { x: 360, y: 520, w: 130, h: 44 },
    { x: 920, y: 520, w: 130, h: 44 },
  ];

  constructor() {
    super(SCENES.GAME);
  }

  create(): void {
    this.gameOver = false;
    this.transitioning = false;
    this.run =
      (this.registry.get('runState') as RunState | undefined) ??
      createInitialRunState();

    this.cameras.main.fadeIn(250, 5, 7, 15);
    this.physics.world.setBounds(0, 0, GAME_WIDTH, GAME_HEIGHT);
    this.buildArenaFloor();
    this.buildObstacles();

    this.effects = new EffectsSystem(this);
    this.player = new Player(
      this,
      GAME_WIDTH / 2,
      GAME_HEIGHT / 2,
      this.run.playerStats,
    );
    this.weapon = new WeaponSystem(this, this.player, this.effects);
    this.spawner = new EnemySpawnSystem(this, this.effects);
    this.pickups = this.physics.add.group();
    this.bombKey = this.input.keyboard!.addKey('E');
    this.waves = new WaveSystem(this, this.spawner, () => ({
      x: this.player.x,
      y: this.player.y,
    }));

    this.hud = new Hud(this);
    this.hud.setWave(this.run.currentWave);
    this.hud.setScore(this.run.score);
    this.hud.setCoins(this.run.coins);
    this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);

    this.registerCollisions();

    // Resume fires when the upgrade overlay closes → advance to next wave.
    // Re-register cleanly so retries don't stack listeners.
    this.events.off(Phaser.Scenes.Events.RESUME, this.onResume, this);
    this.events.on(Phaser.Scenes.Events.RESUME, this.onResume, this);

    // Esc opens the pause overlay (re-registered cleanly across retries).
    this.input.keyboard?.off('keydown-ESC', this.onPauseKey, this);
    this.input.keyboard?.on('keydown-ESC', this.onPauseKey, this);

    this.effects.sound.play('wave_start', 0.4);
    this.beginWave(this.run.currentWave);
  }

  update(time: number): void {
    if (this.gameOver) return;
    const pointer = this.input.activePointer;
    this.player.update(pointer);
    if (Phaser.Input.Keyboard.JustDown(this.bombKey)) this.tryBomb();
    this.spawner.chaseAll(this.player.x, this.player.y);
    this.weapon.update(time, pointer);
    this.hud.setAbilityCooldowns(this.player.dashReady(), this.player.bombReady());
    this.checkWaveCleared();
  }

  private onPauseKey(): void {
    if (this.gameOver || this.transitioning) return;
    this.scene.launch(SCENES.PAUSE);
    this.scene.pause();
  }

  private registerCollisions(): void {
    this.physics.add.overlap(
      this.weapon.group,
      this.spawner.group,
      this.onBulletHitEnemy,
      undefined,
      this,
    );
    this.physics.add.overlap(
      this.player,
      this.spawner.group,
      this.onPlayerHitEnemy,
      undefined,
      this,
    );
    // Enemies push off each other instead of stacking.
    this.physics.add.collider(this.spawner.group, this.spawner.group);

    // Obstacles block the player and enemies; bullets break on them.
    this.physics.add.collider(this.player, this.walls);
    this.physics.add.collider(this.spawner.group, this.walls);
    this.physics.add.collider(
      this.weapon.group,
      this.walls,
      this.onBulletHitWall,
      undefined,
      this,
    );

    this.physics.add.overlap(
      this.player,
      this.pickups,
      this.onCollectPickup,
      undefined,
      this,
    );
  }

  private onBulletHitWall: Phaser.Types.Physics.Arcade.ArcadePhysicsCallback = (
    bulletObj,
  ) => {
    const bullet = bulletObj as Bullet;
    if (!bullet.active) return;
    this.effects.bulletImpact(bullet.x, bullet.y);
    bullet.deactivate();
  };

  private onCollectPickup: Phaser.Types.Physics.Arcade.ArcadePhysicsCallback = (
    _playerObj,
    pickupObj,
  ) => {
    const pickup = pickupObj as Pickup;
    if (!pickup.active) return;
    this.effects.sound.play('pickup', 0.5);

    if (pickup.pickupType === 'heart') {
      this.player.heal(HEART_HEAL);
      this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);
    } else {
      this.run.coins += 1;
      this.run.score += COIN_VALUE;
      this.hud.setScore(this.run.score);
      this.hud.setCoins(this.run.coins);
    }
    this.effects.enemyDeath(pickup.x, pickup.y, COLORS.pickup);
    pickup.destroy();
  };

  // ~25% of kills drop a pickup, skewed toward coins.
  private maybeDropPickup(x: number, y: number): void {
    if (Math.random() > 0.25) return;
    const type: PickupType = Math.random() < 0.35 ? 'heart' : 'coin';
    const pickup = new Pickup(this, x, y, type);
    this.pickups.add(pickup);
  }

  private onBulletHitEnemy: Phaser.Types.Physics.Arcade.ArcadePhysicsCallback = (
    bulletObj,
    enemyObj,
  ) => {
    const bullet = bulletObj as Bullet;
    const enemy = enemyObj as Enemy;
    if (!bullet.active || !enemy.active) return;

    this.effects.bulletImpact(bullet.x, bullet.y);
    const scoreValue = enemy.config.scoreValue;
    const lethal = enemy.takeDamage(bullet.damage);
    if (!bullet.piercing) bullet.deactivate();

    if (lethal) {
      this.killEnemy(enemy, scoreValue);
    }
  };

  // Shared death bookkeeping: score, kills, drop roll, removal.
  private killEnemy(enemy: Enemy, scoreValue: number): void {
    this.run.score += scoreValue;
    this.run.kills += 1;
    this.hud.setScore(this.run.score);
    this.maybeDropPickup(enemy.x, enemy.y);
    enemy.die();
  }

  // Player-centred bomb: damages everything in radius, on cooldown.
  private tryBomb(): void {
    if (!this.player.canBomb()) return;
    this.player.markBombUsed();
    const stats = this.player.stats;
    this.effects.bombBlast(this.player.x, this.player.y, stats.bombRadius);

    for (const obj of this.spawner.group.getChildren()) {
      const enemy = obj as Enemy;
      if (!enemy.active) continue;
      const dist = Phaser.Math.Distance.Between(
        this.player.x,
        this.player.y,
        enemy.x,
        enemy.y,
      );
      if (dist > stats.bombRadius) continue;
      const scoreValue = enemy.config.scoreValue;
      if (enemy.takeDamage(stats.bombDamage)) {
        this.killEnemy(enemy, scoreValue);
      }
    }
  }

  private onPlayerHitEnemy: Phaser.Types.Physics.Arcade.ArcadePhysicsCallback = (
    _playerObj,
    enemyObj,
  ) => {
    const enemy = enemyObj as Enemy;
    if (!enemy.active || this.player.isInvulnerable) return;
    if (!enemy.canDealContactDamage(this.time.now)) return;

    const applied = this.player.takeDamage(enemy.config.contactDamage);
    if (!applied) return;

    this.effects.playerHurt();
    this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);

    if (this.player.isDead) this.triggerGameOver();
  };

  private beginWave(wave: number): void {
    this.transitioning = false;
    this.hud.setWave(wave);
    this.waves.startWave(wave);
  }

  // Wave clears once everything has spawned and no enemies remain.
  private checkWaveCleared(): void {
    if (this.transitioning) return;
    if (!this.waves.doneSpawning) return;
    if (this.spawner.count > 0) return;

    this.transitioning = true;
    if (this.run.currentWave >= FINAL_WAVE) {
      this.registry.set('runState', this.run);
      this.scene.start(SCENES.VICTORY, { runState: this.run });
      return;
    }
    // Open the upgrade overlay; onResume advances to the next wave.
    this.scene.launch(SCENES.UPGRADE, {
      runState: this.run,
      nextWave: this.run.currentWave + 1,
    });
    this.scene.pause();
  }

  private onResume(): void {
    // Only the upgrade overlay advances the wave; the pause menu must not.
    if (!this.transitioning) return;
    this.run.currentWave += 1;
    // Clear any stray bullets so the new wave starts clean.
    this.weapon.group.getChildren().forEach((b) => (b as Bullet).deactivate());
    // Recentre the player for the next wave.
    this.player.setPosition(GAME_WIDTH / 2, GAME_HEIGHT / 2);
    this.player.setVelocity(0, 0);
    this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);
    this.beginWave(this.run.currentWave);
  }

  private triggerGameOver(): void {
    this.gameOver = true;
    this.effects.sound.play('game_over', 0.6);
    this.waves.stop();
    this.player.setVelocity(0, 0);
    this.registry.set('runState', this.run);
    this.time.delayedCall(700, () => {
      this.scene.start(SCENES.GAME_OVER, { runState: this.run });
    });
  }

  private buildArenaFloor(): void {
    this.add
      .rectangle(0, 0, GAME_WIDTH, GAME_HEIGHT, COLORS.bgMid)
      .setOrigin(0, 0)
      .setDepth(-10);

    const grid = this.add.graphics().setDepth(-9);
    grid.lineStyle(1, COLORS.grid, 0.5);
    const step = 64;
    for (let x = step; x < GAME_WIDTH; x += step) {
      grid.lineBetween(x, 0, x, GAME_HEIGHT);
    }
    for (let y = step; y < GAME_HEIGHT; y += step) {
      grid.lineBetween(0, y, GAME_WIDTH, y);
    }

    // Vertical depth gradient: darker toward the top so the floor reads as
    // receding away from the camera (the 3/4 "looking from the front" feel).
    const bands = 16;
    for (let i = 0; i < bands; i++) {
      const alpha = 0.5 * (1 - i / (bands - 1));
      this.add
        .rectangle(
          0,
          (GAME_HEIGHT / bands) * i,
          GAME_WIDTH,
          GAME_HEIGHT / bands + 1,
          COLORS.bgDeep,
          alpha,
        )
        .setOrigin(0, 0)
        .setDepth(-8.5);
    }

    this.add
      .image(GAME_WIDTH / 2, GAME_HEIGHT / 2, 'glow')
      .setScale(16, 10)
      .setAlpha(0.1)
      .setTint(COLORS.accent)
      .setBlendMode(Phaser.BlendModes.ADD)
      .setDepth(-8);
  }

  // Static stone obstacles with a faint accent edge.
  private buildObstacles(): void {
    this.walls = this.physics.add.staticGroup();
    for (const def of GameScene.WALL_DEFS) {
      const rect = this.add
        .rectangle(def.x, def.y, def.w, def.h, COLORS.panelEdge, 1)
        .setStrokeStyle(2, COLORS.accent, 0.35)
        .setDepth(def.y);
      this.physics.add.existing(rect, true);
      this.walls.add(rect);
    }
  }
}
