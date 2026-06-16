// Mutable per-run player stats. Upgrades mutate these between waves.
export type PlayerStats = {
  maxHealth: number;
  health: number;
  speed: number;
  fireRateMs: number;
  bulletDamage: number;
  bulletSpeed: number;
  bulletSize: number;
  bulletPiercing: boolean;
  bulletCount: number; // shots per trigger pull (double-shot upgrade)
  dashSpeed: number;
  dashDurationMs: number;
  dashCooldownMs: number;
  bombDamage: number;
  bombRadius: number;
  bombCooldownMs: number;
};

// Snapshot of a single run, carried across scenes via the Phaser registry.
export type RunState = {
  currentWave: number;
  score: number;
  coins: number;
  kills: number;
  selectedUpgrades: string[];
  playerStats: PlayerStats;
};

export type BulletData = {
  damage: number;
  speed: number;
  lifetimeMs: number;
  piercing: boolean;
};
