import type { PlayerStats, RunState } from '../types/game';

export const DEFAULT_PLAYER_STATS: PlayerStats = {
  maxHealth: 100,
  health: 100,
  speed: 245,
  fireRateMs: 150, // ~6.7 shots/s — crisper cadence
  bulletDamage: 25, // grunt = 2 hits
  bulletSpeed: 720, // snappier travel
  bulletSize: 1,
  bulletPiercing: false,
  bulletCount: 1,
  critChance: 0,
  critMult: 2,
  lifesteal: 0,
  regen: 0,
  magnetRange: 180,
  dashSpeed: 700,
  dashDurationMs: 150, // ~105px burst
  dashCooldownMs: 1500, // more usable defensively
  bombDamage: 80, // one-shots grunts + runners, chunks tanks
  bombRadius: 150,
  bombCooldownMs: 7000,
};

// Fresh run state with a deep copy of default stats so runs never share refs.
export function createInitialRunState(): RunState {
  return {
    currentWave: 1,
    score: 0,
    coins: 0,
    kills: 0,
    level: 1,
    xp: 0,
    xpToNext: 8,
    lifetimeMs: 0,
    selectedUpgrades: [],
    playerStats: { ...DEFAULT_PLAYER_STATS },
  };
}
