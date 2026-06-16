import Phaser from 'phaser';
import {
  UPGRADES,
  UPGRADES_BY_ID,
  type Upgrade,
  type UpgradeId,
} from '../config/upgrades';
import type { RunState } from '../types/game';

// Pure logic for offering and applying upgrades. No scene/UI concerns.
export const UpgradeSystem = {
  // Three distinct upgrades the player hasn't maxed out yet.
  pickThree(run: RunState): Upgrade[] {
    const counts = new Map<UpgradeId, number>();
    for (const id of run.selectedUpgrades as UpgradeId[]) {
      counts.set(id, (counts.get(id) ?? 0) + 1);
    }
    const eligible = UPGRADES.filter(
      (u) => (counts.get(u.id) ?? 0) < u.maxStacks,
    );
    return Phaser.Utils.Array.Shuffle(eligible.slice()).slice(0, 3);
  },

  apply(run: RunState, id: UpgradeId): void {
    const upgrade = UPGRADES_BY_ID[id];
    if (!upgrade) return;
    upgrade.apply(run.playerStats);
    run.selectedUpgrades.push(id);
  },
};
