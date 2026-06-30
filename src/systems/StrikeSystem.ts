import Phaser from 'phaser';
import { DEPTH } from '../config/constants';
import type { EffectsSystem } from './EffectsSystem';

export type StrikeType = 'artillery' | 'air';

// Applies area damage to enemies; returns how many were killed (for the
// "N ELIMINATED" popup). Implemented by the owning scene.
type DamageArea = (x: number, y: number, radius: number, damage: number) => number;
// Called per impact so the scene can apply friendly-fire self-damage.
type OnImpact = (x: number, y: number, radius: number) => void;

export const AIR_MAX_CHARGES = 4;

type StrikeDef = {
  label: string;
  cooldownMs: number;
  color: number;
};

export const STRIKES: Record<StrikeType, StrikeDef> = {
  artillery: { label: 'ARTILLERY', cooldownMs: 7000, color: 0xffd75a },
  air: { label: 'AIR STRIKE', cooldownMs: 11000, color: 0x4fd1ff },
};

const IMPACT_RADIUS = 170;
const IMPACT_DAMAGE = 420;

// The call-in strike system: arm a strike (Q), aim, fire (right-click). The
// payoff is a telegraph -> tension delay -> devastating, screen-shaking,
// crowd-launching barrage. Reuses EffectsSystem.bombBlast for each impact.
export class StrikeSystem {
  armed: StrikeType = 'artillery';
  private scene: Phaser.Scene;
  private effects: EffectsSystem;
  private damageArea: DamageArea;
  private onImpact?: OnImpact;
  private readyAt: Record<StrikeType, number> = { artillery: 0, air: 0 };
  private wasReady: Record<StrikeType, boolean> = { artillery: true, air: true };
  airCharges = AIR_MAX_CHARGES;

  constructor(
    scene: Phaser.Scene,
    effects: EffectsSystem,
    damageArea: DamageArea,
    onImpact?: OnImpact,
  ) {
    this.scene = scene;
    this.effects = effects;
    this.damageArea = damageArea;
    this.onImpact = onImpact;
  }

  // Rearm air-strike charges (e.g. on securing a beacon).
  addAirCharges(n: number): void {
    this.airCharges = Phaser.Math.Clamp(this.airCharges + n, 0, AIR_MAX_CHARGES);
  }

  // Can the armed strike actually be called right now (cooldown + charges)?
  canFire(type: StrikeType): boolean {
    if (!this.isReady(type)) return false;
    if (type === 'air' && this.airCharges <= 0) return false;
    return true;
  }

  // Call each frame: chime when a strike comes back off cooldown (the cadence
  // heartbeat that tells the player their power is restored).
  update(): void {
    (['artillery', 'air'] as StrikeType[]).forEach((type) => {
      const ready = this.isReady(type);
      if (ready && !this.wasReady[type]) this.play('strike_ready', 0.5);
      this.wasReady[type] = ready;
    });
  }

  cycle(): void {
    this.armed = this.armed === 'artillery' ? 'air' : 'artillery';
    this.play('strike_ready', 0.4);
  }

  isReady(type: StrikeType): boolean {
    return this.scene.time.now >= this.readyAt[type];
  }

  cooldownProgress(type: StrikeType): number {
    const remaining = this.readyAt[type] - this.scene.time.now;
    if (remaining <= 0) return 1;
    return Phaser.Math.Clamp(1 - remaining / STRIKES[type].cooldownMs, 0, 1);
  }

  // Fire the armed strike at (tx,ty). origin is the player (defines air-strike
  // run direction). Returns false if the strike is still on cooldown.
  fire(tx: number, ty: number, originX: number, originY: number): boolean {
    const type = this.armed;
    if (!this.canFire(type)) return false;
    this.readyAt[type] = this.scene.time.now + STRIKES[type].cooldownMs;
    if (type === 'air') this.airCharges -= 1;

    if (type === 'artillery') this.callArtillery(tx, ty);
    else this.callAirStrike(tx, ty, originX, originY);
    return true;
  }

  // ---- artillery: a cluster barrage walking across the marked area ----------

  private callArtillery(cx: number, cy: number): void {
    const color = STRIKES.artillery.color;
    this.play('shell_whistle', 0.7);
    const decal = this.markZone(cx, cy, IMPACT_RADIUS * 1.4, color, 2000);

    // 6 impacts marching outward from the centre over ~900ms.
    const shots = 6;
    for (let i = 0; i < shots; i++) {
      const ang = (i / shots) * Math.PI * 2 + Phaser.Math.FloatBetween(0, 1);
      const rad = i === 0 ? 0 : Phaser.Math.Between(40, IMPACT_RADIUS);
      const ix = cx + Math.cos(ang) * rad;
      const iy = cy + Math.sin(ang) * rad;
      this.scene.time.delayedCall(2000 + i * 150, () => {
        if (i === 0) decal.destroy();
        this.impact(ix, iy, i === 0);
      });
    }
  }

  // ---- air strike: a jet strafes a line through the target ------------------

  private callAirStrike(tx: number, ty: number, ox: number, oy: number): void {
    const color = STRIKES.air.color;
    this.play('shell_whistle', 0.35);
    const ang = Phaser.Math.Angle.Between(ox, oy, tx, ty);
    const half = 700;
    const sx = tx - Math.cos(ang) * half;
    const sy = ty - Math.sin(ang) * half;
    const ex = tx + Math.cos(ang) * half;
    const ey = ty + Math.sin(ang) * half;

    const line = this.markLine(sx, sy, ex, ey, color, 1500);

    this.scene.time.delayedCall(1500, () => {
      line.destroy();
      this.play('jet_pass', 0.6);
      // Jet sprite flies the line.
      const jet = this.scene.add
        .image(sx, sy, 'jet')
        .setRotation(ang)
        .setDepth(DEPTH.fx + 200)
        .setScale(1.4);
      this.scene.tweens.add({
        targets: jet,
        x: ex,
        y: ey,
        duration: 650,
        ease: 'Sine.easeIn',
        onComplete: () => jet.destroy(),
      });
      // Strafe impacts along the line.
      const shots = 7;
      for (let i = 0; i < shots; i++) {
        const t = i / (shots - 1);
        const ix = Phaser.Math.Linear(sx, ex, t);
        const iy = Phaser.Math.Linear(sy, ey, t);
        this.scene.time.delayedCall(120 + i * 80, () => this.impact(ix, iy, i === 3));
      }
    });
  }

  // ---- shared impact: explosion FX + area damage + crowd launch -------------

  private impact(x: number, y: number, big: boolean): void {
    this.effects.bombBlast(x, y, big ? IMPACT_RADIUS * 1.2 : IMPACT_RADIUS);
    // Each shell punches the camera so the barrage itself feels devastating
    // (not just the call-in).
    this.scene.cameras.main.shake(big ? 220 : 90, big ? 0.012 : 0.006);
    this.damageArea(x, y, IMPACT_RADIUS, IMPACT_DAMAGE);
    this.onImpact?.(x, y, IMPACT_RADIUS); // friendly-fire check

    // Lingering scorch that fades over a few seconds.
    const scorch = this.scene.add
      .image(x, y, 'scorch')
      .setDepth(-700)
      .setScale((IMPACT_RADIUS / 64) * 1.1)
      .setAlpha(0.85);
    this.scene.tweens.add({
      targets: scorch,
      alpha: 0,
      duration: 5000,
      onComplete: () => scorch.destroy(),
    });

    if (big) this.hitStop(70);
  }

  // Brief physics freeze so the big blast feels like it has mass. Guarded so a
  // scene shutdown mid-freeze can never leave the world paused.
  private hitStop(ms: number): void {
    this.scene.physics.world.pause();
    const ev = this.scene.time.delayedCall(ms, () =>
      this.scene.physics.world.resume(),
    );
    this.scene.events.once('shutdown', () => {
      ev.remove(false);
      this.scene.physics.world.resume();
    });
  }

  // Pulsing ring decal that marks an incoming artillery zone.
  private markZone(
    x: number,
    y: number,
    radius: number,
    color: number,
    life: number,
  ): Phaser.GameObjects.Image {
    const ring = this.scene.add
      .image(x, y, 'ring')
      .setTint(color)
      .setAlpha(0.9)
      .setDepth(-650)
      .setScale((radius / 64) * 0.9);
    this.scene.tweens.add({
      targets: ring,
      alpha: 0.35,
      scale: (radius / 64) * 1.0,
      duration: 240,
      yoyo: true,
      repeat: Math.floor(life / 480),
    });
    return ring;
  }

  // Telegraph strip for the air-strike run.
  private markLine(
    sx: number,
    sy: number,
    ex: number,
    ey: number,
    color: number,
    life: number,
  ): Phaser.GameObjects.Rectangle {
    const ang = Phaser.Math.Angle.Between(sx, sy, ex, ey);
    const len = Phaser.Math.Distance.Between(sx, sy, ex, ey);
    const strip = this.scene.add
      .rectangle((sx + ex) / 2, (sy + ey) / 2, len, IMPACT_RADIUS * 1.3, color, 0.18)
      .setRotation(ang)
      .setDepth(-650);
    this.scene.tweens.add({
      targets: strip,
      alpha: 0.4,
      duration: 220,
      yoyo: true,
      repeat: Math.floor(life / 440),
    });
    return strip;
  }

  private play(key: string, volume: number): void {
    if (this.scene.cache.audio.exists(key)) {
      this.scene.sound.play(key, { volume });
    }
  }
}
