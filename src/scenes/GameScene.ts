import Phaser from 'phaser';
import { SCENES, COLORS, WORLD_WIDTH, WORLD_HEIGHT } from '../config/constants';
import { Player } from '../entities/Player';
import { Bullet } from '../entities/Bullet';
import { Enemy } from '../entities/Enemy';
import { Pickup, HEART_HEAL, COIN_VALUE, type PickupType } from '../entities/Pickup';
import { Gem, GEM_SCORE } from '../entities/Gem';
import { WeaponSystem } from '../systems/WeaponSystem';
import { EnemySpawnSystem } from '../systems/EnemySpawnSystem';
import { EffectsSystem } from '../systems/EffectsSystem';
import { WaveSystem } from '../systems/WaveSystem';
import { UpgradeSystem } from '../systems/UpgradeSystem';
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
  private gems!: Phaser.Physics.Arcade.Group;
  private bombKey!: Phaser.Input.Keyboard.Key;
  private gameOver = false;
  private transitioning = false;
  private boss: Enemy | null = null;
  private bossSpawned = false;

  // Obstacle layout across the 3200x2000 world (world centre ~1600,1000 kept
  // clear for the player spawn).
  private static readonly WALL_DEFS = [
    { x: 1280, y: 760, w: 70, h: 260 },
    { x: 1920, y: 760, w: 70, h: 260 },
    { x: 1280, y: 1240, w: 70, h: 260 },
    { x: 1920, y: 1240, w: 70, h: 260 },
    { x: 1600, y: 460, w: 300, h: 70 },
    { x: 1600, y: 1540, w: 300, h: 70 },
    { x: 760, y: 1000, w: 70, h: 320 },
    { x: 2440, y: 1000, w: 70, h: 320 },
    { x: 980, y: 1400, w: 220, h: 70 },
    { x: 2220, y: 600, w: 220, h: 70 },
  ];

  constructor() {
    super(SCENES.GAME);
  }

  create(): void {
    this.gameOver = false;
    this.transitioning = false;
    this.boss = null;
    this.bossSpawned = false;
    this.run =
      (this.registry.get('runState') as RunState | undefined) ??
      createInitialRunState();

    this.cameras.main.fadeIn(250, 5, 7, 15);
    this.physics.world.setBounds(0, 0, WORLD_WIDTH, WORLD_HEIGHT);
    this.buildArenaFloor();
    this.buildObstacles();

    this.effects = new EffectsSystem(this);
    this.player = new Player(
      this,
      WORLD_WIDTH / 2,
      WORLD_HEIGHT / 2,
      this.run.playerStats,
    );

    // Camera follows the player across the larger world.
    this.cameras.main.setBounds(0, 0, WORLD_WIDTH, WORLD_HEIGHT);
    this.cameras.main.startFollow(this.player, true, 0.12, 0.12);
    this.weapon = new WeaponSystem(this, this.player, this.effects);
    this.spawner = new EnemySpawnSystem(this, this.effects);
    this.pickups = this.physics.add.group();
    this.gems = this.physics.add.group();
    this.bombKey = this.input.keyboard!.addKey('E');
    this.waves = new WaveSystem(this, this.spawner, () => ({
      x: this.player.x,
      y: this.player.y,
    }));

    this.hud = new Hud(this);
    this.hud.setWave(this.run.currentWave);
    this.hud.setScore(this.run.score);
    this.hud.setCoins(this.run.coins);
    this.hud.setXp(this.run.xp, this.run.xpToNext, this.run.level);
    this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);

    this.registerCollisions();

    // Resume fires when the upgrade overlay closes → advance to next wave.
    // Re-register cleanly so retries don't stack listeners.
    this.events.off(Phaser.Scenes.Events.RESUME, this.onResume, this);
    this.events.on(Phaser.Scenes.Events.RESUME, this.onResume, this);

    // Esc opens the pause overlay (re-registered cleanly across retries).
    this.input.keyboard?.off('keydown-ESC', this.onPauseKey, this);
    this.input.keyboard?.on('keydown-ESC', this.onPauseKey, this);

    // Health regeneration tick (from the Regeneration upgrade).
    this.time.addEvent({
      delay: 1000,
      loop: true,
      callback: () => {
        const regen = this.player.stats.regen;
        if (this.gameOver || regen <= 0) return;
        if (this.player.stats.health >= this.player.stats.maxHealth) return;
        this.player.heal(regen);
        this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);
      },
    });

    this.beginWave(this.run.currentWave);
  }

  update(time: number): void {
    if (this.gameOver) return;
    const pointer = this.input.activePointer;
    this.player.update(pointer);
    if (Phaser.Input.Keyboard.JustDown(this.bombKey)) this.tryBomb();
    this.spawner.chaseAll(this.player.x, this.player.y);
    this.weapon.update(time, pointer);
    this.updateGems();
    if (this.boss && this.boss.active) {
      this.hud.setBoss(this.boss.health, this.boss.config.maxHealth);
    }
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
    this.physics.add.overlap(
      this.player,
      this.gems,
      this.onCollectGem,
      undefined,
      this,
    );
  }

  private onCollectGem: Phaser.Types.Physics.Arcade.ArcadePhysicsCallback = (
    _playerObj,
    gemObj,
  ) => {
    const gem = gemObj as Gem;
    if (!gem.active) return;
    gem.destroy();
    this.run.score += GEM_SCORE;
    this.run.xp += 1;
    this.hud.setScore(this.run.score);
    this.effects.sound.play('pickup', 0.4);

    if (this.run.xp >= this.run.xpToNext) this.levelUp();
    else this.hud.setXp(this.run.xp, this.run.xpToNext, this.run.level);
  };

  // Gem XP filled the bar → level up and offer an upgrade (pauses the wave).
  private levelUp(): void {
    this.run.xp -= this.run.xpToNext;
    this.run.level += 1;
    this.run.xpToNext = 8 + (this.run.level - 1) * 4;
    this.hud.setXp(this.run.xp, this.run.xpToNext, this.run.level);
    this.effects.sound.play('upgrade_select', 0.4);

    const choices = UpgradeSystem.pickThree(this.run);
    if (choices.length === 0) {
      // Upgrade pool maxed out — reward a heal instead of an empty pick.
      this.player.heal(25);
      this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);
      return;
    }
    this.scene.launch(SCENES.UPGRADE, { runState: this.run, level: this.run.level });
    this.scene.pause();
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

  // Shared death bookkeeping: score, kills, gem drop, bonus drop, removal.
  private killEnemy(enemy: Enemy, scoreValue: number): void {
    if (enemy === this.boss) {
      this.defeatBoss();
      return;
    }
    this.run.score += scoreValue;
    this.run.kills += 1;
    this.hud.setScore(this.run.score);
    this.gems.add(new Gem(this, enemy.x, enemy.y)); // wave-objective gem + XP
    this.maybeDropPickup(enemy.x, enemy.y); // bonus heart/coin
    if (this.player.stats.lifesteal > 0) {
      this.player.heal(this.player.stats.lifesteal);
      this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);
    }
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
    this.effects.sound.play('wave_start', 0.4);
    this.waves.startWave(wave);
  }

  // Pull gems toward the player when near; once enemies are cleared, vacuum
  // every remaining gem in so the wave never ends in a tedious gem-hunt.
  private updateGems(): void {
    const vacuum = this.waves.doneSpawning && this.spawner.count === 0;
    for (const obj of this.gems.getChildren()) {
      const gem = obj as Gem;
      if (!gem.active) continue;
      const body = gem.body as Phaser.Physics.Arcade.Body;
      const d = Phaser.Math.Distance.Between(this.player.x, this.player.y, gem.x, gem.y);
      if (vacuum || d < this.player.stats.magnetRange) {
        const a = Phaser.Math.Angle.Between(gem.x, gem.y, this.player.x, this.player.y);
        this.physics.velocityFromRotation(a, vacuum ? 700 : 420, body.velocity);
      } else {
        body.velocity.set(0, 0);
      }
    }
  }

  // Wave clears once everything has spawned, no enemies remain, AND every
  // dropped gem has been collected (the vacuum auto-finishes that). Upgrades
  // now come from levelling up, so wave-clear just advances after a breather.
  private checkWaveCleared(): void {
    if (this.transitioning) return;
    if (!this.waves.doneSpawning) return;
    if (this.spawner.count > 0) return;
    if (this.gems.countActive(true) > 0) return;

    this.transitioning = true;
    if (this.run.currentWave >= FINAL_WAVE) {
      // Final wave cleared → the boss arrives (victory comes when it dies).
      if (!this.bossSpawned) this.startBossFight();
      this.transitioning = false;
      return;
    }
    // Short breather, then the next wave.
    this.time.delayedCall(1400, () => {
      if (this.gameOver) return;
      this.run.currentWave += 1;
      this.weapon.group.getChildren().forEach((b) => (b as Bullet).deactivate());
      this.player.setPosition(WORLD_WIDTH / 2, WORLD_HEIGHT / 2);
      this.player.setVelocity(0, 0);
      this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);
      this.beginWave(this.run.currentWave);
    });
  }

  // Resume fires after a level-up overlay (or the pause menu) closes; refresh
  // the health bar in case an upgrade raised max health.
  private onResume(): void {
    this.hud.setHealth(this.player.stats.health, this.player.stats.maxHealth);
  }

  // Spawn the finale boss and start its minion summons + health bar.
  private startBossFight(): void {
    this.bossSpawned = true;
    this.effects.sound.play('wave_start', 0.6);
    this.effects.screenShake(400, 0.015);
    this.boss = this.spawner.spawn('boss', this.player.x, this.player.y);
    if (this.boss) this.hud.setBoss(this.boss.health, this.boss.config.maxHealth);
    this.time.addEvent({
      delay: 4000,
      loop: true,
      callback: () => {
        if (this.gameOver || !this.boss || !this.boss.active) return;
        for (let i = 0; i < 4; i++) {
          this.spawner.spawn('swarmer', this.player.x, this.player.y);
        }
      },
    });
  }

  private defeatBoss(): void {
    const b = this.boss;
    if (!b) return;
    this.run.score += b.config.scoreValue;
    this.run.kills += 1;
    this.hud.setScore(this.run.score);
    this.hud.clearBoss();
    this.effects.enemyDeath(b.x, b.y, 0xff3344);
    this.effects.screenShake(600, 0.03);
    this.effects.sound.play('enemy_die', 0.9);
    b.die();
    this.boss = null;
    this.transitioning = true;
    this.registry.set('runState', this.run);
    this.time.delayedCall(1100, () => {
      this.scene.start(SCENES.VICTORY, { runState: this.run });
    });
  }

  private triggerGameOver(): void {
    this.gameOver = true;
    this.effects.sound.play('game_over', 0.6);
    this.waves.stop();
    this.player.die();
    this.registry.set('runState', this.run);
    // Give the death animation time to play before the game-over screen.
    this.time.delayedCall(1100, () => {
      this.scene.start(SCENES.GAME_OVER, { runState: this.run });
    });
  }

  private buildArenaFloor(): void {
    this.add
      .rectangle(0, 0, WORLD_WIDTH, WORLD_HEIGHT, COLORS.bgMid)
      .setOrigin(0, 0)
      .setDepth(-10);

    const grid = this.add.graphics().setDepth(-9);
    grid.lineStyle(1, COLORS.grid, 0.5);
    const step = 64;
    for (let x = step; x < WORLD_WIDTH; x += step) {
      grid.lineBetween(x, 0, x, WORLD_HEIGHT);
    }
    for (let y = step; y < WORLD_HEIGHT; y += step) {
      grid.lineBetween(0, y, WORLD_WIDTH, y);
    }

    // Vertical depth gradient: darker toward the top.
    const bands = 16;
    for (let i = 0; i < bands; i++) {
      const alpha = 0.5 * (1 - i / (bands - 1));
      this.add
        .rectangle(
          0,
          (WORLD_HEIGHT / bands) * i,
          WORLD_WIDTH,
          WORLD_HEIGHT / bands + 1,
          COLORS.bgDeep,
          alpha,
        )
        .setOrigin(0, 0)
        .setDepth(-8.5);
    }

    this.add
      .image(WORLD_WIDTH / 2, WORLD_HEIGHT / 2, 'glow')
      .setScale(28, 18)
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
