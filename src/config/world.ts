import { WORLD_WIDTH, WORLD_HEIGHT } from './constants';

// v2 renders in metres. All gameplay tuning still lives in the v1 pixel units
// (enemies.ts, playerStats.ts, upgrades.ts), so balance carries over 1:1 —
// this factor converts px → m at the boundary.
export const PX = 1 / 30;

export const ARENA_W = WORLD_WIDTH * PX; // 120 m
export const ARENA_D = WORLD_HEIGHT * PX; // ~73 m
export const HALF_W = ARENA_W / 2;
export const HALF_D = ARENA_D / 2;

// v1 world (x right, y down, origin top-left) → v2 ground plane (x right,
// z up-screen, origin centre).
export function toWorld(x: number, y: number): { x: number; z: number } {
  return { x: (x - WORLD_WIDTH / 2) * PX, z: -(y - WORLD_HEIGHT / 2) * PX };
}

// Obstacle layout carried over from the v1 GameScene (pixel space).
export const WALL_DEFS = [
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

export type Rect = { minX: number; maxX: number; minZ: number; maxZ: number };

export const WALL_RECTS: Rect[] = WALL_DEFS.map((d) => {
  const c = toWorld(d.x, d.y);
  const hw = (d.w * PX) / 2;
  const hd = (d.h * PX) / 2;
  return { minX: c.x - hw, maxX: c.x + hw, minZ: c.z - hd, maxZ: c.z + hd };
});

export const WALL_HEIGHT = 2.4;

// Push a circle out of every wall and keep it inside the arena. Mutates pos.
export function resolveCircle(pos: { x: number; z: number }, r: number): boolean {
  let hit = false;
  for (const w of WALL_RECTS) {
    const cx = Math.max(w.minX, Math.min(pos.x, w.maxX));
    const cz = Math.max(w.minZ, Math.min(pos.z, w.maxZ));
    const dx = pos.x - cx;
    const dz = pos.z - cz;
    const d2 = dx * dx + dz * dz;
    if (d2 >= r * r) continue;
    hit = true;
    if (d2 > 1e-6) {
      const d = Math.sqrt(d2);
      pos.x = cx + (dx / d) * r;
      pos.z = cz + (dz / d) * r;
    } else {
      // Centre is inside the box: shove out along the shallowest axis.
      const pushes = [
        { a: 'x', v: w.minX - r, d: pos.x - (w.minX - r) },
        { a: 'x', v: w.maxX + r, d: w.maxX + r - pos.x },
        { a: 'z', v: w.minZ - r, d: pos.z - (w.minZ - r) },
        { a: 'z', v: w.maxZ + r, d: w.maxZ + r - pos.z },
      ].sort((p, q) => Math.abs(p.d) - Math.abs(q.d));
      const p = pushes[0];
      if (p.a === 'x') pos.x = p.v;
      else pos.z = p.v;
    }
  }
  const m = 0.6;
  pos.x = Math.max(-HALF_W + r + m, Math.min(HALF_W - r - m, pos.x));
  pos.z = Math.max(-HALF_D + r + m, Math.min(HALF_D - r - m, pos.z));
  return hit;
}

export function pointInWall(x: number, z: number): boolean {
  for (const w of WALL_RECTS) {
    if (x > w.minX && x < w.maxX && z > w.minZ && z < w.maxZ) return true;
  }
  return Math.abs(x) > HALF_W || Math.abs(z) > HALF_D;
}
