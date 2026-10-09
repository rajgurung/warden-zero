import {
  Color3,
  Color4,
  DynamicTexture,
  MeshBuilder,
  ParticleSystem,
  StandardMaterial,
  Vector3,
  type Mesh,
} from '../render/babylon';
import { COLORS } from '../config/constants';
import { hex, type Stage } from '../render/Stage';
import type { Sound } from './Sound';

// Particle bursts, bomb shockwaves and camera shake. Bursts round-robin over
// a small pool of particle systems so each can carry its own colour.
export class Effects {
  private pool: ParticleSystem[] = [];
  private next = 0;
  private waves: { mesh: Mesh; t: number; dur: number; r: number }[] = [];
  private waveMat: StandardMaterial;

  constructor(private stage: Stage, readonly sound: Sound) {
    const scene = stage.scene;
    const tex = new DynamicTexture('spark', { width: 64, height: 64 }, scene, false);
    tex.hasAlpha = true;
    const g = tex.getContext() as CanvasRenderingContext2D;
    const grd = g.createRadialGradient(32, 32, 0, 32, 32, 32);
    grd.addColorStop(0, 'rgba(255,255,255,1)');
    grd.addColorStop(0.3, 'rgba(255,255,255,0.65)');
    grd.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = grd;
    g.fillRect(0, 0, 64, 64);
    tex.update();

    for (let i = 0; i < 14; i++) {
      const ps = new ParticleSystem('fx' + i, 260, scene);
      ps.particleTexture = tex;
      ps.emitter = new Vector3(0, -100, 0);
      ps.minEmitBox = new Vector3(-0.15, 0, -0.15);
      ps.maxEmitBox = new Vector3(0.15, 0.3, 0.15);
      ps.colorDead = new Color4(0, 0, 0, 0);
      ps.minLifeTime = 0.2;
      ps.maxLifeTime = 0.6;
      ps.emitRate = 0;
      ps.blendMode = ParticleSystem.BLENDMODE_ADD;
      ps.direction1 = new Vector3(-1, 0.6, -1);
      ps.direction2 = new Vector3(1, 2.4, 1);
      ps.gravity = new Vector3(0, -16, 0);
      ps.start();
      this.pool.push(ps);
    }

    this.waveMat = new StandardMaterial('shockMat', scene);
    this.waveMat.emissiveColor = hex(COLORS.gold);
    this.waveMat.disableLighting = true;
    this.waveMat.alpha = 0.8;

    scene.onBeforeRenderObservable.add(() => {
      const dt = Math.min(stage.engine.getDeltaTime() / 1000, 0.05);
      for (let i = this.waves.length - 1; i >= 0; i--) {
        const w = this.waves[i];
        w.t += dt;
        const k = w.t / w.dur;
        const s = 0.2 + k * w.r * 2;
        w.mesh.scaling.set(s, 1, s);
        w.mesh.visibility = 1 - k;
        if (k >= 1) {
          w.mesh.dispose();
          this.waves.splice(i, 1);
        }
      }
    });
  }

  burst(pos: Vector3, color: Color3, count: number, power: [number, number] = [3, 9], size: [number, number] = [0.08, 0.3]): void {
    const ps = this.pool[this.next];
    this.next = (this.next + 1) % this.pool.length;
    ps.emitter = pos.clone();
    ps.color1 = new Color4(color.r, color.g, color.b, 1);
    ps.color2 = new Color4(Math.min(1, color.r + 0.35), Math.min(1, color.g + 0.35), Math.min(1, color.b + 0.35), 1);
    ps.minEmitPower = power[0];
    ps.maxEmitPower = power[1];
    ps.minSize = size[0];
    ps.maxSize = size[1];
    ps.manualEmitCount = count;
  }

  bulletImpact(pos: Vector3): void {
    this.burst(pos, hex(COLORS.accent), 5, [2, 6], [0.06, 0.18]);
  }

  enemyDeath(pos: Vector3, color: number, big = false): void {
    this.burst(pos, hex(color), big ? 120 : 22, big ? [6, 18] : [3, 10], big ? [0.2, 0.7] : [0.1, 0.35]);
    this.stage.addShake(big ? 1 : 0.06);
  }

  bombBlast(pos: Vector3, radius: number): void {
    const ring = MeshBuilder.CreateTorus('shock', { diameter: 1, thickness: 0.06, tessellation: 48 }, this.stage.scene);
    ring.position.set(pos.x, 0.25, pos.z);
    ring.material = this.waveMat;
    this.waves.push({ mesh: ring, t: 0, dur: 0.35, r: radius });
    this.burst(pos.add(new Vector3(0, 0.5, 0)), hex(COLORS.gold), 90, [8, 20], [0.15, 0.5]);
    this.stage.addShake(0.5);
    this.sound.play('bomb', 0.6);
  }

  playerHurt(pos: Vector3): void {
    this.burst(pos.add(new Vector3(0, 1.2, 0)), hex(COLORS.health), 18);
    this.stage.addShake(0.35);
    this.sound.play('player_hurt', 0.55);
  }
}
