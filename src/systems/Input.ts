import { Matrix, Vector3 } from '../render/babylon';
import type { Stage } from '../render/Stage';

// Keyboard + mouse on desktop; virtual stick + buttons on touch.
// Gameplay reads a snapshot each frame and consumes one-shot presses.
export class Input {
  private keys = new Set<string>();
  private pressed = new Set<string>();
  fireHeld = false;
  mouseActive = false;
  aim = new Vector3(0, 0, 5);
  private stick = { x: 0, y: 0 };
  private stickId: number | null = null;
  private origin = { x: 0, y: 0 };
  private pointer = { x: 0, y: 0 };

  constructor(private stage: Stage, canvas: HTMLCanvasElement, ui: { joy: HTMLElement; knob: HTMLElement; reticle: HTMLElement; dash: HTMLElement; bomb: HTMLElement }) {
    addEventListener('keydown', (e) => {
      if (!this.keys.has(e.code)) this.pressed.add(e.code);
      this.keys.add(e.code);
      if (e.code === 'Space') e.preventDefault();
    });
    addEventListener('keyup', (e) => this.keys.delete(e.code));
    addEventListener('blur', () => { this.keys.clear(); this.fireHeld = false; });
    canvas.addEventListener('contextmenu', (e) => e.preventDefault());

    canvas.addEventListener('pointerdown', (e) => {
      if (e.pointerType === 'mouse') {
        this.mouseActive = true;
        if (e.button === 0) this.fireHeld = true;
        if (e.button === 2) this.pressed.add('Bomb');
        return;
      }
      if (this.stickId !== null) return;
      this.stickId = e.pointerId;
      this.origin = { x: e.clientX, y: e.clientY };
      this.stick = { x: 0, y: 0 };
      ui.joy.style.left = e.clientX + 'px';
      ui.joy.style.top = e.clientY + 'px';
      ui.joy.hidden = false;
      ui.knob.style.transform = 'translate(0px, 0px)';
    });
    addEventListener('pointermove', (e) => {
      if (e.pointerType === 'mouse') {
        this.mouseActive = true;
        this.pointer = { x: e.clientX, y: e.clientY };
        ui.reticle.style.transform = `translate(${e.clientX}px, ${e.clientY}px)`;
        return;
      }
      if (e.pointerId !== this.stickId) return;
      let dx = e.clientX - this.origin.x;
      let dy = e.clientY - this.origin.y;
      const d = Math.hypot(dx, dy);
      const max = 52;
      if (d > max) { dx = (dx / d) * max; dy = (dy / d) * max; }
      this.stick = { x: dx / max, y: dy / max };
      ui.knob.style.transform = `translate(${dx}px, ${dy}px)`;
    });
    const end = (e: PointerEvent) => {
      if (e.pointerType === 'mouse' && e.button === 0) this.fireHeld = false;
      if (e.pointerId === this.stickId) {
        this.stickId = null;
        this.stick = { x: 0, y: 0 };
        ui.joy.hidden = true;
      }
    };
    addEventListener('pointerup', end);
    addEventListener('pointercancel', end);
    ui.dash.addEventListener('pointerdown', (e) => { e.stopPropagation(); this.pressed.add('Space'); });
    ui.bomb.addEventListener('pointerdown', (e) => { e.stopPropagation(); this.pressed.add('Bomb'); });
  }

  // Unit-ish move vector on the ground plane (x right, z up-screen).
  move(): Vector3 {
    let x = 0;
    let z = 0;
    if (this.keys.has('KeyW') || this.keys.has('ArrowUp')) z += 1;
    if (this.keys.has('KeyS') || this.keys.has('ArrowDown')) z -= 1;
    if (this.keys.has('KeyD') || this.keys.has('ArrowRight')) x += 1;
    if (this.keys.has('KeyA') || this.keys.has('ArrowLeft')) x -= 1;
    x += this.stick.x;
    z -= this.stick.y;
    const v = new Vector3(x, 0, z);
    const l = v.length();
    if (l > 1) v.scaleInPlace(1 / l);
    return v;
  }

  // Project the mouse onto the shooting plane (y = 1.2 m).
  updateAim(): void {
    if (!this.mouseActive) return;
    const scene = this.stage.scene;
    const rect = this.stage.engine.getRenderingCanvasClientRect();
    if (!rect) return;
    // CSS pixels relative to the canvas; Babylon applies hardware scaling itself.
    const ray = scene.createPickingRay(this.pointer.x - rect.left, this.pointer.y - rect.top, Matrix.Identity(), this.stage.camera);
    if (ray.direction.y > -0.01) return;
    const t = (1.2 - ray.origin.y) / ray.direction.y;
    this.aim = ray.origin.add(ray.direction.scale(t));
  }

  consume(code: string): boolean {
    const had = this.pressed.has(code);
    this.pressed.delete(code);
    return had;
  }

  clearPresses(): void {
    this.pressed.clear();
    this.fireHeld = false;
  }
}
