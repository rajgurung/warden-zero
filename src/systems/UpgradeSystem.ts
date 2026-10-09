import { UPGRADES, UPGRADES_BY_ID, type Upgrade, type UpgradeId } from '../config/upgrades';
import type { RunState } from '../types/game';

function shuffle<T>(a: T[]): T[] {
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

// Pure logic for offering and applying upgrades (unchanged from v1).
export const UpgradeSystem = {
  pickThree(run: RunState): Upgrade[] {
    const counts = new Map<UpgradeId, number>();
    for (const id of run.selectedUpgrades as UpgradeId[]) counts.set(id, (counts.get(id) ?? 0) + 1);
    const eligible = UPGRADES.filter((u) => (counts.get(u.id) ?? 0) < u.maxStacks);
    return shuffle(eligible.slice()).slice(0, 3);
  },

  apply(run: RunState, id: UpgradeId): void {
    const upgrade = UPGRADES_BY_ID[id];
    if (!upgrade) return;
    upgrade.apply(run.playerStats);
    run.selectedUpgrades.push(id);
  },

  stacks(run: RunState, id: UpgradeId): number {
    return run.selectedUpgrades.filter((s) => s === id).length;
  },
};
