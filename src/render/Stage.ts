import {
  Engine,
  Scene,
  FreeCamera,
  Vector3,
  Color3,
  Color4,
  HemisphericLight,
  DirectionalLight,
  PointLight,
  ShadowGenerator,
  GlowLayer,
  DefaultRenderingPipeline,
  ImageProcessingConfiguration,
  MeshBuilder,
  StandardMaterial,
  DynamicTexture,
  Texture,
  type AbstractMesh,
} from './babylon';
import { COLORS } from '../config/constants';
import { ARENA_W, ARENA_D, HALF_W, HALF_D, WALL_RECTS, WALL_HEIGHT } from '../config/world';

export const hex = (n: number): Color3 => Color3.FromHexString('#' + n.toString(16).padStart(6, '0'));

export const IS_TOUCH = typeof matchMedia !== 'undefined' && matchMedia('(pointer: coarse)').matches;

// Camera sits behind and above the Warden, angled down — the 3D take on the
// v1 3/4 view. Offset chosen so roughly the same slice of arena is on screen.
export const CAM_OFFSET = new Vector3(0, 20, -14);

// Owns the Babylon engine, scene, camera, lights, post-processing and the
// static arena. Gameplay systems only add/remove dynamic meshes.
export class Stage {
  readonly engine: Engine;
  readonly scene: Scene;
  readonly camera: FreeCamera;
  readonly shadows: ShadowGenerator;
  readonly playerLight: PointLight;
  private shake = 0;
  private camTarget = Vector3.Zero();

  constructor(canvas: HTMLCanvasElement) {
    this.engine = new Engine(canvas, true, { stencil: true, antialias: true, powerPreference: 'high-performance' }, true);
    // Render at device resolution (capped at 2x) — no blurry upscaling.
    this.engine.setHardwareScalingLevel(1 / Math.min(window.devicePixelRatio || 1, 2));

    const scene = (this.scene = new Scene(this.engine));
    const bg = hex(COLORS.bgDeep);
    scene.clearColor = new Color4(bg.r, bg.g, bg.b, 1);
    scene.ambientColor = new Color3(0.14, 0.16, 0.24);
    scene.fogMode = Scene.FOGMODE_EXP2;
    scene.fogDensity = 0.011;
    scene.fogColor = bg;
    scene.skipPointerMovePicking = true;

    const cam = (this.camera = new FreeCamera('cam', CAM_OFFSET.clone(), scene));
    cam.fov = 0.8;
    cam.minZ = 0.5;
    cam.maxZ = 260;
    cam.inputs.clear();
    cam.setTarget(Vector3.Zero());

    const hemi = new HemisphericLight('hemi', new Vector3(0, 1, 0), scene);
    hemi.intensity = 0.6;
    hemi.diffuse = new Color3(0.6, 0.72, 1);
    hemi.groundColor = new Color3(0.04, 0.05, 0.1);

    const sun = new DirectionalLight('sun', new Vector3(-0.45, -1, -0.3), scene);
    sun.position = new Vector3(30, 50, 25);
    sun.intensity = 1.6;
    sun.diffuse = new Color3(0.86, 0.9, 1);
    sun.autoUpdateExtends = false;
    sun.shadowFrustumSize = 64;

    const sg = (this.shadows = new ShadowGenerator(IS_TOUCH ? 1024 : 2048, sun));
    sg.usePercentageCloserFiltering = true;
    sg.filteringQuality = ShadowGenerator.QUALITY_MEDIUM;
    sg.bias = 0.0015;
    sg.normalBias = 0.02;
    // Shadow frustum follows the camera target so detail stays where you look.
    scene.onBeforeRenderObservable.add(() => {
      sun.position.set(this.camTarget.x + 30, 50, this.camTarget.z + 25);
    });

    const pl = (this.playerLight = new PointLight('playerLight', new Vector3(0, 2, 0), scene));
    pl.diffuse = hex(COLORS.accent);
    pl.intensity = 0.7;
    pl.range = 12;

    const glow = new GlowLayer('glow', scene, { mainTextureSamples: IS_TOUCH ? 1 : 4, blurKernelSize: 48 });
    glow.intensity = 0.85;

    const pipe = new DefaultRenderingPipeline('pp', true, scene, [cam]);
    pipe.samples = IS_TOUCH ? 2 : 4;
    pipe.fxaaEnabled = true;
    pipe.sharpenEnabled = true;
    pipe.sharpen.edgeAmount = 0.2;
    pipe.imageProcessingEnabled = true;
    const ip = pipe.imageProcessing;
    ip.toneMappingEnabled = true;
    ip.toneMappingType = ImageProcessingConfiguration.TONEMAPPING_ACES;
    ip.exposure = 1.2;
    ip.contrast = 1.15;
    ip.vignetteEnabled = true;
    ip.vignetteWeight = 2.4;
    ip.vignetteColor = new Color4(0, 0, 0, 0);

    this.buildArena();
    window.addEventListener('resize', () => this.engine.resize());
  }

  addCaster(m: AbstractMesh): void {
    this.shadows.addShadowCaster(m, false);
  }

  addShake(amount: number): void {
    this.shake = Math.max(this.shake, amount);
  }

  // Ease the camera toward the player each frame, plus decaying shake.
  follow(target: Vector3, dt: number, snap = false): void {
    const want = target.add(CAM_OFFSET);
    const k = snap ? 1 : Math.min(1, dt * 6);
    this.camera.position = Vector3.Lerp(this.camera.position, want, k);
    this.camTarget = Vector3.Lerp(this.camTarget, target.add(new Vector3(0, 0, 2.5)), snap ? 1 : Math.min(1, dt * 8));
    if (this.shake > 0) {
      this.shake = Math.max(0, this.shake - dt * 1.8);
      const s = this.shake * 0.7;
      this.camera.position.addInPlace(new Vector3((Math.random() - 0.5) * s, (Math.random() - 0.5) * s, (Math.random() - 0.5) * s));
    }
    this.camera.setTarget(this.camTarget);
    this.playerLight.position.set(target.x, 2.4, target.z);
  }

  private buildArena(): void {
    const scene = this.scene;

    // Floor: tiled deck plates with seams and rivets, drawn once to a texture.
    const ground = MeshBuilder.CreateGround('floor', { width: ARENA_W + 40, height: ARENA_D + 40 }, scene);
    const gm = new StandardMaterial('floorMat', scene);
    const tex = this.deckTexture();
    tex.uScale = (ARENA_W + 40) / 8;
    tex.vScale = (ARENA_D + 40) / 8;
    gm.diffuseTexture = tex;
    gm.specularColor = new Color3(0.22, 0.28, 0.4);
    gm.specularPower = 56;
    ground.material = gm;
    ground.receiveShadows = true;

    // Centre spawn marking.
    const ring = MeshBuilder.CreateTorus('spawnRing', { diameter: 7, thickness: 0.08, tessellation: 64 }, scene);
    ring.position.y = 0.02;
    ring.scaling.y = 0.05;
    ring.material = this.emissive('ringMat', hex(COLORS.accent).scale(0.55));

    // Obstacles: dark blocks with a glowing accent cap, like the v1 stroke.
    const wallMat = new StandardMaterial('wallMat', scene);
    wallMat.diffuseColor = hex(COLORS.panelEdge);
    wallMat.specularColor = new Color3(0.4, 0.45, 0.6);
    wallMat.specularPower = 48;
    const capMat = this.emissive('capMat', hex(COLORS.accent).scale(0.75));
    const topMat = new StandardMaterial('wallTop', scene);
    topMat.diffuseColor = hex(COLORS.bgMid);
    topMat.specularColor = new Color3(0.08, 0.1, 0.14);
    for (const [i, w] of WALL_RECTS.entries()) {
      const sx = w.maxX - w.minX;
      const sz = w.maxZ - w.minZ;
      const box = MeshBuilder.CreateBox('wall' + i, { width: sx, depth: sz, height: WALL_HEIGHT }, scene);
      box.position.set((w.minX + w.maxX) / 2, WALL_HEIGHT / 2, (w.minZ + w.maxZ) / 2);
      box.material = wallMat;
      box.receiveShadows = true;
      this.addCaster(box);
      // Thin glowing rim around the top edge (the v1 accent stroke).
      const top = MeshBuilder.CreateBox('wallTop' + i, { width: sx - 0.1, depth: sz - 0.1, height: 0.04 }, scene);
      top.position.set(box.position.x, WALL_HEIGHT + 0.02, box.position.z);
      top.material = topMat;
      const rim = 0.07;
      for (const [w2, d2, ox, oz] of [
        [sx + rim, rim, 0, sz / 2], [sx + rim, rim, 0, -sz / 2],
        [rim, sz + rim, sx / 2, 0], [rim, sz + rim, -sx / 2, 0],
      ]) {
        const r = MeshBuilder.CreateBox('wallRim' + i, { width: w2, depth: d2, height: 0.06 }, scene);
        r.position.set(box.position.x + ox, WALL_HEIGHT + 0.02, box.position.z + oz);
        r.material = capMat;
      }
    }

    // Perimeter barrier with a magenta warning strip.
    const barrierMat = new StandardMaterial('barrierMat', scene);
    barrierMat.diffuseColor = hex(COLORS.panel);
    barrierMat.specularColor = new Color3(0.3, 0.3, 0.4);
    const stripMat = this.emissive('stripMat', hex(COLORS.magenta).scale(0.8));
    const edges = [
      { x: 0, z: HALF_D + 0.5, w: ARENA_W + 2, d: 1 },
      { x: 0, z: -HALF_D - 0.5, w: ARENA_W + 2, d: 1 },
      { x: HALF_W + 0.5, z: 0, w: 1, d: ARENA_D },
      { x: -HALF_W - 0.5, z: 0, w: 1, d: ARENA_D },
    ];
    edges.forEach((e, i) => {
      const b = MeshBuilder.CreateBox('edge' + i, { width: e.w, depth: e.d, height: 1.4 }, scene);
      b.position.set(e.x, 0.7, e.z);
      b.material = barrierMat;
      b.receiveShadows = true;
      const s = MeshBuilder.CreateBox('edgeStrip' + i, { width: e.w + 0.02, depth: e.d + 0.02, height: 0.12 }, scene);
      s.position.set(e.x, 1.2, e.z);
      s.material = stripMat;
    });

    // Pylons along the long edges for silhouette and parallax.
    const pylon = MeshBuilder.CreateCylinder('pylon', { height: 5, diameterTop: 0.8, diameterBottom: 1.6, tessellation: 8 }, scene);
    pylon.material = barrierMat;
    pylon.isVisible = false;
    const lamp = MeshBuilder.CreateSphere('lamp', { diameter: 0.5, segments: 8 }, scene);
    lamp.material = this.emissive('lampMat', hex(COLORS.gold));
    lamp.isVisible = false;
    for (let x = -HALF_W; x <= HALF_W; x += 12) {
      for (const z of [HALF_D + 3, -HALF_D - 3]) {
        const p = pylon.createInstance('py');
        p.position.set(x, 2.5, z);
        const l = lamp.createInstance('lp');
        l.position.set(x, 5.1, z);
      }
    }
  }

  emissive(name: string, c: Color3): StandardMaterial {
    const m = new StandardMaterial(name, this.scene);
    m.emissiveColor = c;
    m.diffuseColor = Color3.Black();
    m.specularColor = Color3.Black();
    m.disableLighting = true;
    return m;
  }

  private deckTexture(): DynamicTexture {
    const S = 512;
    const dt = new DynamicTexture('deck', { width: S, height: S }, this.scene, true);
    const g = dt.getContext() as CanvasRenderingContext2D;
    g.fillStyle = '#0b1020';
    g.fillRect(0, 0, S, S);
    const P = S / 4;
    for (let y = 0; y < S; y += P) {
      for (let x = 0; x < S; x += P) {
        const v = Math.floor(Math.random() * 6);
        g.fillStyle = `rgb(${14 + v},${19 + v},${36 + v})`;
        g.fillRect(x + 3, y + 3, P - 6, P - 6);
        g.fillStyle = '#2a3654';
        for (const [rx, ry] of [[8, 8], [P - 12, 8], [8, P - 12], [P - 12, P - 12]]) g.fillRect(x + rx, y + ry, 4, 4);
      }
    }
    g.strokeStyle = '#1a2240';
    g.lineWidth = 3;
    for (let i = 0; i <= S; i += P) {
      g.beginPath(); g.moveTo(i, 0); g.lineTo(i, S); g.stroke();
      g.beginPath(); g.moveTo(0, i); g.lineTo(S, i); g.stroke();
    }
    // Faint cyan conduit on one plate per tile for detail.
    g.strokeStyle = 'rgba(79,209,255,0.18)';
    g.lineWidth = 2;
    g.beginPath(); g.moveTo(P * 1.5, P + 14); g.lineTo(P * 1.5, P * 2 - 14); g.stroke();
    dt.update();
    dt.anisotropicFilteringLevel = 8;
    dt.wrapU = Texture.WRAP_ADDRESSMODE;
    dt.wrapV = Texture.WRAP_ADDRESSMODE;
    return dt;
  }
}

