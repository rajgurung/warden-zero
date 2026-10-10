import {
  Color3,
  Color4,
  DynamicTexture,
  Material,
  Mesh,
  MeshBuilder,
  StandardMaterial,
  Texture,
  TransformNode,
  Vector3,
  type Scene,
} from './babylon';
import { COLORS } from '../config/constants';
import { ENEMY_CONFIGS, type EnemyType } from '../config/enemies';
import { PX } from '../config/world';
import { hex, type Stage } from './Stage';

// Characters are the v1 hand-drawn sprites on upright cards (classic 2.5D);
// props are procedural low-poly meshes. Enemy cards are drawn as instances,
// so 80 on screen is cheap. Swap any builder without touching gameplay code.

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

// ------------------------------------------------------------ sprite cards

// The camera looks down at ~50°, so an upright card shows only ~0.64 of its
// height. Cards are narrowed by the same factor to keep the art's proportions.
const SQUASH = 0.64;
const SPRITES = `${import.meta.env.BASE_URL}assets/sprites/`;

function spriteTex(scene: Scene, file: string, pixelArt = false): Texture {
  const t = new Texture(SPRITES + file, scene, pixelArt, true, pixelArt ? Texture.NEAREST_SAMPLINGMODE : Texture.TRILINEAR_SAMPLINGMODE);
  t.hasAlpha = true;
  t.wrapU = Texture.CLAMP_ADDRESSMODE;
  t.wrapV = Texture.CLAMP_ADDRESSMODE;
  return t;
}

// Unlit and alpha-tested: colour = tint × texture, so the scene lights don't
// wash the art out and overlapping cards need no sorting.
function spriteMat(scene: Scene, name: string, tex: Texture, tint = Color3.White()): StandardMaterial {
  const m = new StandardMaterial(name, scene);
  m.diffuseTexture = tex;
  m.useAlphaFromDiffuseTexture = true;
  m.emissiveColor = tint;
  m.disableLighting = true;
  m.backFaceCulling = false; // mirrored cards show their back face
  m.transparencyMode = Material.MATERIAL_ALPHATEST;
  m.alphaCutOff = 0.5;
  return m;
}

// Upright plane with its origin on the bottom edge, so position = feet.
// `sink` pushes it below the floor to hide empty space under the feet.
function card(scene: Scene, name: string, width: number, height: number, sink = 0): Mesh {
  const m = MeshBuilder.CreatePlane(name, { width, height }, scene);
  m.position.y = height / 2 - sink;
  m.bakeCurrentTransformIntoVertices();
  m.billboardMode = Mesh.BILLBOARDMODE_Y;
  m.metadata = { card: true }; // see the glow layer setup in Stage
  return m;
}

// Soft round drop shadow (the v1 shadow blob), 1 m across; scale per unit.
export function buildShadow(stage: Stage): Mesh {
  const scene = stage.scene;
  const tex = new DynamicTexture('shadowTex', { width: 64, height: 64 }, scene, false);
  tex.hasAlpha = true;
  const g = tex.getContext() as CanvasRenderingContext2D;
  const grd = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  grd.addColorStop(0, 'rgba(0,0,0,0.7)');
  grd.addColorStop(0.55, 'rgba(0,0,0,0.45)');
  grd.addColorStop(1, 'rgba(0,0,0,0)');
  g.fillStyle = grd;
  g.fillRect(0, 0, 64, 64);
  tex.update();
  const m = MeshBuilder.CreateGround('shadow', { width: 1, height: 1 }, scene);
  const sm = new StandardMaterial('shadowMat', scene);
  sm.diffuseTexture = tex;
  sm.useAlphaFromDiffuseTexture = true;
  sm.diffuseColor = Color3.Black();
  sm.specularColor = Color3.Black();
  sm.disableLighting = true;
  m.material = sm;
  m.isVisible = false;
  stage.glow.addExcludedMesh(m);
  return m;
}

// One card per walk frame per enemy type (instances can't swap textures, so
// each enemy owns one instance per frame and shows one at a time). Art,
// tint and size follow v1: card height = radius × 5 (v1 px, now metres).
export function buildEnemyCards(stage: Stage): Record<EnemyType, Mesh[]> {
  const scene = stage.scene;
  const textures = new Map<string, Texture[]>();
  const framesOf = (key: string): Texture[] => {
    if (!textures.has(key)) {
      // grunt (zombie) and runner (robot) have a 4-frame walk; the Tiny
      // Dungeon monsters are single 16 px tiles kept crisp.
      const walk = key === 'grunt' || key === 'runner';
      textures.set(key, walk ? [0, 1, 2, 3].map((i) => spriteTex(scene, `${key}_walk${i}.png`)) : [spriteTex(scene, `${key}_idle.png`, true)]);
    }
    return textures.get(key)!;
  };
  const out = {} as Record<EnemyType, Mesh[]>;
  for (const cfg of Object.values(ENEMY_CONFIGS)) {
    const frames = framesOf(cfg.spriteKey);
    const pixelArt = frames.length === 1;
    const h = cfg.radius * 5 * PX;
    const w = h * (pixelArt ? 1 : 0.75) * SQUASH;
    const tint = cfg.tint !== undefined ? hex(cfg.tint) : Color3.White();
    out[cfg.type] = frames.map((tex, i) => {
      const m = card(scene, cfg.type + i, w, h, pixelArt ? h / 16 : 0);
      m.material = spriteMat(scene, cfg.type + 'Mat' + i, tex, tint);
      // Per-instance colour multiplier, used for the white hit flash.
      m.registerInstancedBuffer('color', 4);
      m.instancedBuffers.color = new Color4(1, 1, 1, 1);
      m.isVisible = false;
      return m;
    });
  }
  return out;
}

// The Warden: the v1 hero frames (public/assets/sprites/hero) on one card,
// swapping the texture per pose. Side poses face right; mirror for left.
export type WardenPose = 'idle' | 'run_down' | 'run_up' | 'run_side' | 'shoot' | 'shoot_up' | 'shoot_down' | 'dash' | 'death';

export type WardenModel = {
  root: TransformNode; // position; yaw follows aim and only drives the muzzle
  card: Mesh;
  muzzleLocal: Vector3;
  show(pose: WardenPose, frame: number, flip: boolean): void;
};

// Height of the rifle on the card; bullets fly at this height.
export const MUZZLE_Y = 1.45;
const RUN_FRAMES = 6;

export function buildWarden(stage: Stage, shadow: Mesh): WardenModel {
  const scene = stage.scene;
  const root = new TransformNode('warden', scene);
  // Matches the on-screen height of the old 3D model (which also showed its top).
  const h = 2.8;
  const c = card(scene, 'wardenCard', h * 0.75 * SQUASH, h);
  c.parent = root;
  const textures = new Map<string, Texture>();
  for (const pose of ['idle', 'shoot', 'shoot_up', 'shoot_down', 'dash', 'death']) {
    textures.set(pose, spriteTex(scene, `hero/${pose}.png`));
  }
  for (const dir of ['down', 'up', 'side']) {
    for (let i = 0; i < RUN_FRAMES; i++) textures.set(`run_${dir}_${i}`, spriteTex(scene, `hero/run_${dir}_${i}.png`));
  }
  const m = spriteMat(scene, 'wardenMat', textures.get('idle')!);
  // Blended as well as alpha-tested so the invulnerability blink can fade it.
  m.transparencyMode = Material.MATERIAL_ALPHATESTANDBLEND;
  c.material = m;
  const s = shadow.createInstance('wardenShadow');
  s.parent = root;
  s.position.y = 0.03;
  s.scaling.setAll(1.4);
  return {
    root,
    card: c,
    muzzleLocal: new Vector3(0, MUZZLE_Y, 0.7),
    show(pose, frame, flip) {
      m.diffuseTexture = textures.get(pose.startsWith('run') ? `${pose}_${frame % RUN_FRAMES}` : pose)!;
      c.scaling.x = flip ? -1 : 1;
      // The death pose lies in the upper part of its frame; drop it to the floor.
      c.position.y = pose === 'death' ? -0.33 * h : 0;
    },
  };
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
