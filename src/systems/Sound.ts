export type SfxKey =
  | 'shoot'
  | 'enemy_hit'
  | 'enemy_die'
  | 'player_hurt'
  | 'pickup'
  | 'dash'
  | 'bomb'
  | 'upgrade_select'
  | 'wave_start'
  | 'game_over';

const KEYS: SfxKey[] = [
  'shoot', 'enemy_hit', 'enemy_die', 'player_hurt', 'pickup', 'dash',
  'bomb', 'upgrade_select', 'wave_start', 'game_over',
];

// Web Audio player for the v1 Kenney SFX in public/assets/audio. Unlocks on
// the first user gesture and stays silent if a file fails to load.
export class Sound {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private buffers = new Map<SfxKey, AudioBuffer>();
  private lastPlayed = new Map<SfxKey, number>();
  muted = false;

  unlock(): void {
    if (this.ctx) {
      void this.ctx.resume();
      return;
    }
    try {
      this.ctx = new AudioContext();
    } catch {
      return;
    }
    this.master = this.ctx.createGain();
    this.master.gain.value = 0.7;
    this.master.connect(this.ctx.destination);
    const base = import.meta.env.BASE_URL;
    for (const key of KEYS) {
      fetch(`${base}assets/audio/${key}.mp3`)
        .then((r) => r.arrayBuffer())
        .then((b) => this.ctx!.decodeAudioData(b))
        .then((buf) => this.buffers.set(key, buf))
        .catch(() => undefined);
    }
  }

  // detune in cents; throttled per key so 80-enemy hit storms don't clip.
  play(key: SfxKey, volume = 0.5, detune = 0): void {
    if (this.muted || !this.ctx || !this.master) return;
    const buf = this.buffers.get(key);
    if (!buf) return;
    const now = this.ctx.currentTime;
    if (now - (this.lastPlayed.get(key) ?? -1) < 0.035) return;
    this.lastPlayed.set(key, now);
    const src = this.ctx.createBufferSource();
    src.buffer = buf;
    src.detune.value = detune;
    const g = this.ctx.createGain();
    g.gain.value = volume;
    src.connect(g).connect(this.master);
    src.start();
  }
}
