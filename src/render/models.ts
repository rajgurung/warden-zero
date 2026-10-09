import {
  Color3,
  Mesh,
  MeshBuilder,
  StandardMaterial,
  TransformNode,
  Vector3,
  type Scene,
} from './babylon';
import { COLORS } from '../config/constants';
import { ENEMY_CONFIGS, type EnemyType } from '../config/enemies';
import { PX } from '../config/world';
import { hex, type Stage } from './Stage';

// Procedural low-poly models built from primitives. Enemies are merged into
// one mesh per type and drawn as instances, so 80 on screen is cheap.
// Swap any builder for a glTF load later without touching gameplay code.

function mat(scene: Scene, name: string, diffuse: Color3, emissive = Color3.Black(), spec = 0.35): StandardMaterial {
  const m = new StandardMaterial(name, scene);
  m.diffuseColor = diffuse;
  m.emissiveColor = emissive;
  m.specularColor = new Color3(spec, spec, spec * 1.1);
  m.specularPower = 40;
  return m;
}

function glowMat(scene: Scene, name: string, c: Color3): StandardMaterial {
  const m = new StandardMaterial(name, scene);
  m.emissiveColor = c;
  m.diffuseColor = Color3.Black();
  m.disableLighting = true;
  return m;
}

function merge(parts: Mesh[], name: string): Mesh {
  const m = Mesh.MergeMeshes(parts, true, true, undefined, false, true)!;
  m.name = name;
  m.isVisible = false;
  return m;
}

type Look = { skin: Color3; armour: Color3; eye: Color3 };

// Humanoid zombie-ish silhouette: legs, hunched torso, reaching arms, head.
function humanoid(scene: Scene, key: string, look: Look, opts: { bulk?: number; horns?: boolean; thin?: boolean } = {}): Mesh[] {
  const bulk = opts.bulk ?? 1;
  const thin = opts.thin ? 0.6 : 1;
  const skin = mat(scene, key + 'Skin', look.skin, look.skin.scale(0.06));
  const armour = mat(scene, key + 'Arm', look.armour, Color3.Black(), 0.5);
  const eye = glowMat(scene, key + 'Eye', look.eye);
  const parts: Mesh[] = [];
  const add = (m: Mesh, material: StandardMaterial, p: [number, number, number], r: [number, number, number] = [0, 0, 0]) => {
    m.material = material;
    m.position.set(...p);
    m.rotation.set(...r);
    parts.push(m);
  };
  const legW = 0.22 * bulk * thin;
  add(MeshBuilder.CreateBox(key + 'l1', { width: legW, height: 0.7, depth: legW }, scene), armour, [-0.18 * bulk, 0.35, 0]);
  add(MeshBuilder.CreateBox(key + 'l2', { width: legW, height: 0.7, depth: legW }, scene), armour, [0.18 * bulk, 0.35, 0]);
  add(MeshBuilder.CreateCapsule(key + 't', { height: 0.95, radius: 0.32 * bulk * thin, tessellation: 12 }, scene), skin, [0, 1.12, 0.06], [0.28, 0, 0]);
  add(MeshBuilder.CreateBox(key + 'a1', { width: 0.16 * bulk, height: 0.16 * bulk, depth: 0.75 }, scene), skin, [-0.42 * bulk, 1.3, 0.38], [0.15, 0, 0]);
  add(MeshBuilder.CreateBox(key + 'a2', { width: 0.16 * bulk, height: 0.16 * bulk, depth: 0.75 }, scene), skin, [0.42 * bulk, 1.25, 0.38], [0.25, 0, 0]);
  add(MeshBuilder.CreateSphere(key + 'h', { diameter: 0.46 * Math.sqrt(bulk), segments: 12 }, scene), skin, [0, 1.78, 0.2]);
  add(MeshBuilder.CreateSphere(key + 'e1', { diameter: 0.09, segments: 6 }, scene), eye, [-0.09, 1.82, 0.42]);
  add(MeshBuilder.CreateSphere(key + 'e2', { diameter: 0.09, segments: 6 }, scene), eye, [0.09, 1.82, 0.42]);
  if (bulk > 1.2) {
    add(MeshBuilder.CreateBox(key + 'sh', { width: 1.0 * bulk, height: 0.22, depth: 0.5 }, scene), armour, [0, 1.5, 0]);
  }
  if (opts.horns) {
    add(MeshBuilder.CreateCylinder(key + 'hn1', { height: 0.4, diameterTop: 0, diameterBottom: 0.12, tessellation: 6 }, scene), armour, [-0.15, 2.05, 0.15], [0, 0, 0.5]);
    add(MeshBuilder.CreateCylinder(key + 'hn2', { height: 0.4, diameterTop: 0, diameterBottom: 0.12, tessellation: 6 }, scene), armour, [0.15, 2.05, 0.15], [0, 0, -0.5]);
  }
  return parts;
}

function robot(scene: Scene, key: string, look: Look): Mesh[] {
  const body = mat(scene, key + 'Body', look.skin, Color3.Black(), 0.7);
  const dark = mat(scene, key + 'Dark', look.armour, Color3.Black(), 0.5);
  const eye = glowMat(scene, key + 'Eye', look.eye);
  const parts: Mesh[] = [];
  const add = (m: Mesh, material: StandardMaterial, p: [number, number, number], r: [number, number, number] = [0, 0, 0]) => {
    m.material = material; m.position.set(...p); m.rotation.set(...r); parts.push(m);
  };
  add(MeshBuilder.CreateBox(key + 'l1', { width: 0.16, height: 0.75, depth: 0.16 }, scene), dark, [-0.16, 0.38, 0], [0.35, 0, 0]);
  add(MeshBuilder.CreateBox(key + 'l2', { width: 0.16, height: 0.75, depth: 0.16 }, scene), dark, [0.16, 0.38, 0], [-0.35, 0, 0]);
  add(MeshBuilder.CreateBox(key + 't', { width: 0.55, height: 0.6, depth: 0.4 }, scene), body, [0, 1.05, 0.05], [0.4, 0, 0]);
  add(MeshBuilder.CreateBox(key + 'h', { width: 0.42, height: 0.32, depth: 0.42 }, scene), body, [0, 1.5, 0.28]);
  add(MeshBuilder.CreateBox(key + 'v', { width: 0.34, height: 0.07, depth: 0.05 }, scene), eye, [0, 1.52, 0.5]);
  add(MeshBuilder.CreateCylinder(key + 'an', { height: 0.35, diameter: 0.04, tessellation: 5 }, scene), dark, [0.14, 1.8, 0.2]);
  return parts;
}

function spider(scene: Scene, key: string, look: Look): Mesh[] {
  const body = mat(scene, key + 'Body', look.skin, look.skin.scale(0.05));
  const leg = mat(scene, key + 'Leg', look.armour);
  const eye = glowMat(scene, key + 'Eye', look.eye);
  const parts: Mesh[] = [];
  const abdomen = MeshBuilder.CreateSphere(key + 'ab', { diameter: 0.9, segments: 12 }, scene);
  abdomen.scaling.set(1, 0.75, 1.2); abdomen.position.set(0, 0.55, -0.35); abdomen.material = body; parts.push(abdomen);
  const head = MeshBuilder.CreateSphere(key + 'hd', { diameter: 0.5, segments: 10 }, scene);
  head.position.set(0, 0.45, 0.25); head.material = body; parts.push(head);
  for (const x of [-0.08, 0.08, -0.16, 0.16]) {
    const e = MeshBuilder.CreateSphere(key + 'e', { diameter: 0.07, segments: 6 }, scene);
    e.position.set(x, 0.55, 0.47); e.material = eye; parts.push(e);
  }
  for (let i = 0; i < 4; i++) {
    for (const side of [-1, 1]) {
      const l = MeshBuilder.CreateBox(key + 'lg', { width: 0.9, height: 0.06, depth: 0.06 }, scene);
      l.position.set(side * 0.45, 0.35, 0.3 - i * 0.22);
      l.rotation.set(0, side * (0.35 - i * 0.25), side * -0.45);
      l.material = leg; parts.push(l);
    }
  }
  return parts;
}

// One merged template per enemy type, scaled so its footprint matches the
// v1 collision radius.
export function buildEnemyTemplates(scene: Scene): Record<EnemyType, Mesh> {
  const c = (n: number) => hex(n);
  const red = c(0xff3a52);
  const looks: Record<EnemyType, () => Mesh[]> = {
    grunt: () => humanoid(scene, 'grunt', { skin: new Color3(0.36, 0.52, 0.3), armour: new Color3(0.2, 0.18, 0.22), eye: c(COLORS.enemy) }),
    swarmer: () => humanoid(scene, 'swarmer', { skin: c(0xbfff5a).scale(0.55), armour: new Color3(0.14, 0.16, 0.1), eye: c(0xbfff5a) }, { thin: true }),
    runner: () => robot(scene, 'runner', { skin: new Color3(0.62, 0.64, 0.7), armour: new Color3(0.15, 0.16, 0.2), eye: c(COLORS.gold) }),
    brute: () => humanoid(scene, 'brute', { skin: c(0x6f9a4f).scale(0.7), armour: new Color3(0.22, 0.2, 0.18), eye: c(COLORS.enemy) }, { bulk: 1.5 }),
    tank: () => humanoid(scene, 'tank', { skin: c(0xb15cff).scale(0.55), armour: new Color3(0.18, 0.14, 0.26), eye: c(COLORS.magenta) }, { bulk: 1.35 }),
    skeleton: () => humanoid(scene, 'skeleton', { skin: c(0xe6dcc0).scale(0.85), armour: new Color3(0.4, 0.37, 0.32), eye: c(0x4fd1ff) }, { thin: true }),
    spider: () => spider(scene, 'spider', { skin: c(0x9a6a44).scale(0.6), armour: new Color3(0.2, 0.13, 0.08), eye: red }),
    demon: () => humanoid(scene, 'demon', { skin: c(0xff5a5a).scale(0.6), armour: new Color3(0.15, 0.06, 0.06), eye: c(COLORS.gold) }, { horns: true }),
    boss: () => humanoid(scene, 'boss', { skin: c(0x9b2230).scale(0.9), armour: new Color3(0.12, 0.1, 0.12), eye: red }, { bulk: 1.6, horns: true }),
    spitter: () => robot(scene, 'spitter', { skin: c(0x7fd23a).scale(0.6), armour: new Color3(0.12, 0.16, 0.1), eye: c(0x9bff67) }),
    warlord: () => humanoid(scene, 'warlord', { skin: c(0x356a2a), armour: new Color3(0.15, 0.12, 0.1), eye: red }, { bulk: 1.6, horns: true }),
  };
  const out = {} as Record<EnemyType, Mesh>;
  for (const type of Object.keys(looks) as EnemyType[]) {
    const m = merge(looks[type](), type);
    // Base models are ~0.6 m in radius; scale to the configured hitbox.
    const r = ENEMY_CONFIGS[type].radius * PX;
    const baseR = type === 'spider' ? 0.7 : type === 'brute' || type === 'tank' || type === 'boss' || type === 'warlord' ? 0.7 : 0.45;
    m.metadata = { baseScale: r / baseR };
    out[type] = m;
  }
  return out;
}

// The Warden: armoured trooper with a cyan visor and rifle. Kept as separate
// child meshes so we can animate legs and recoil.
export type WardenModel = {
  root: TransformNode;
  body: TransformNode;
  legL: Mesh;
  legR: Mesh;
  gun: TransformNode;
  meshes: Mesh[];
  muzzleLocal: Vector3;
};

export function buildWarden(stage: Stage): WardenModel {
  const scene = stage.scene;
  const root = new TransformNode('warden', scene);
  const body = new TransformNode('wardenBody', scene);
  body.parent = root;
  const plate = mat(scene, 'wPlate', new Color3(0.82, 0.85, 0.9), Color3.Black(), 0.9);
  plate.specularPower = 96;
  const suit = mat(scene, 'wSuit', hex(COLORS.panel).scale(1.4), Color3.Black(), 0.4);
  const visor = glowMat(scene, 'wVisor', hex(COLORS.accent));
  const gold = glowMat(scene, 'wGold', hex(COLORS.gold).scale(0.9));
  const meshes: Mesh[] = [];
  const part = (m: Mesh, material: StandardMaterial, parent: TransformNode, p: [number, number, number]) => {
    m.material = material; m.parent = parent; m.position.set(...p); meshes.push(m); return m;
  };
  const legL = part(MeshBuilder.CreateBox('wLegL', { width: 0.22, height: 0.75, depth: 0.26 }, scene), suit, root, [-0.17, 0.38, 0]);
  const legR = part(MeshBuilder.CreateBox('wLegR', { width: 0.22, height: 0.75, depth: 0.26 }, scene), suit, root, [0.17, 0.38, 0]);
  legL.setPivotPoint(new Vector3(0, 0.37, 0));
  legR.setPivotPoint(new Vector3(0, 0.37, 0));
  part(MeshBuilder.CreateCylinder('wHip', { height: 0.3, diameter: 0.6, tessellation: 12 }, scene), suit, body, [0, 0.85, 0]);
  part(MeshBuilder.CreateCylinder('wChest', { height: 0.7, diameterTop: 0.86, diameterBottom: 0.6, tessellation: 14 }, scene), plate, body, [0, 1.3, 0]);
  part(MeshBuilder.CreateBox('wPauldL', { width: 0.32, height: 0.18, depth: 0.42 }, scene), plate, body, [-0.5, 1.6, 0]);
  part(MeshBuilder.CreateBox('wPauldR', { width: 0.32, height: 0.18, depth: 0.42 }, scene), plate, body, [0.5, 1.6, 0]);
  part(MeshBuilder.CreateSphere('wHead', { diameter: 0.5, segments: 16 }, scene), plate, body, [0, 1.92, 0]);
  part(MeshBuilder.CreateBox('wVisor', { width: 0.4, height: 0.09, depth: 0.12 }, scene), visor, body, [0, 1.95, 0.2]);
  part(MeshBuilder.CreateBox('wPack', { width: 0.55, height: 0.6, depth: 0.25 }, scene), suit, body, [0, 1.3, -0.42]);
  part(MeshBuilder.CreateBox('wPackLight', { width: 0.36, height: 0.05, depth: 0.04 }, scene), gold, body, [0, 1.48, -0.56]);
  const gun = new TransformNode('wGun', scene);
  gun.parent = body;
  gun.position.set(0.32, 1.22, 0.32);
  part(MeshBuilder.CreateBox('wGunBody', { width: 0.14, height: 0.2, depth: 0.95 }, scene), suit, gun, [0, 0, 0.25]);
  part(MeshBuilder.CreateBox('wGunRail', { width: 0.04, height: 0.04, depth: 0.7 }, scene), visor, gun, [0, 0.12, 0.28]);
  part(MeshBuilder.CreateBox('wArm', { width: 0.18, height: 0.18, depth: 0.5 }, scene), plate, gun, [-0.12, -0.05, -0.05]);
  meshes.forEach((m) => {
    if (m.material !== visor && m.material !== gold) stage.addCaster(m);
  });
  return { root, body, legL, legR, gun, meshes, muzzleLocal: new Vector3(0.32, 1.22, 1.12) };
}

export function buildBolt(stage: Stage): Mesh {
  const m = MeshBuilder.CreateSphere('bolt', { diameter: 0.22, segments: 8 }, stage.scene);
  m.scaling.z = 3.4;
  m.material = glowMat(stage.scene, 'boltMat', hex(COLORS.accent).add(new Color3(0.25, 0.2, 0.1)));
  m.isVisible = false;
  m.bakeCurrentTransformIntoVertices();
  return m;
}

export function buildCritBolt(stage: Stage): Mesh {
  const m = MeshBuilder.CreateSphere('critBolt', { diameter: 0.28, segments: 8 }, stage.scene);
  m.scaling.z = 3.6;
  m.material = glowMat(stage.scene, 'critMat', hex(COLORS.gold).scale(1.2));
  m.isVisible = false;
  m.bakeCurrentTransformIntoVertices();
  return m;
}

export function buildGem(stage: Stage): Mesh {
  const m = MeshBuilder.CreatePolyhedron('gem', { type: 1, size: 0.24 }, stage.scene);
  const gm = mat(stage.scene, 'gemMat', hex(COLORS.accentSoft), hex(COLORS.accent).scale(0.85), 1);
  m.material = gm;
  m.isVisible = false;
  return m;
}

export function buildHeart(stage: Stage): Mesh {
  const scene = stage.scene;
  const red = glowMat(scene, 'heartMat', hex(COLORS.health));
  const a = MeshBuilder.CreateSphere('h1', { diameter: 0.34, segments: 10 }, scene);
  a.position.set(-0.11, 0.08, 0);
  const b = MeshBuilder.CreateSphere('h2', { diameter: 0.34, segments: 10 }, scene);
  b.position.set(0.11, 0.08, 0);
  const t = MeshBuilder.CreateCylinder('h3', { height: 0.36, diameterTop: 0.5, diameterBottom: 0, tessellation: 4 }, scene);
  t.position.set(0, -0.12, 0);
  t.rotation.y = Math.PI / 4;
  t.scaling.z = 0.55;
  [a, b, t].forEach((m) => (m.material = red));
  return merge([a, b, t], 'heart');
}

export function buildCoin(stage: Stage): Mesh {
  const m = MeshBuilder.CreateCylinder('coin', { height: 0.06, diameter: 0.42, tessellation: 20 }, stage.scene);
  m.rotation.x = Math.PI / 2;
  m.bakeCurrentTransformIntoVertices();
  m.material = mat(stage.scene, 'coinMat', hex(COLORS.gold).scale(0.6), hex(COLORS.gold).scale(0.7), 1);
  m.isVisible = false;
  return m;
}
