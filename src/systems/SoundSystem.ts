import Phaser from 'phaser';

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
  | 'game_over'
  | 'shell_whistle'
  | 'jet_pass'
  | 'strike_ready';

// Thin facade over Phaser audio. Plays a key only if it has been loaded, so
// the game runs silently until real SFX assets are dropped into PreloadScene.
export class SoundSystem {
  private scene: Phaser.Scene;

  constructor(scene: Phaser.Scene) {
    this.scene = scene;
  }

  // detune is in cents (±). Small per-shot variation stops rapid fire from
  // sounding like a robotic loop.
  play(key: SfxKey, volume = 0.5, detune = 0): void {
    if (this.scene.cache.audio.exists(key)) {
      this.scene.sound.play(key, { volume, detune });
    }
  }
}
