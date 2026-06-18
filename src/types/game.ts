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
  bulletCount: number; // shots per trigger pull (multishot upgrade)
  critChance: number; // 0..1 chance a shot crits
  critMult: number; // damage multiplier on a crit
  lifesteal: number; // HP healed per kill
  regen: number; // HP regenerated per second
  magnetRange: number; // gem pickup magnet radius (world px)
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
  level: number;
  xp: number;
  xpToNext: number;
  selectedUpgrades: string[];
  playerStats: PlayerStats;
};

export type BulletData = {
  damage: number;
  speed: number;
  lifetimeMs: number;
  piercing: boolean;
};
