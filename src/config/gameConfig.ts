import Phaser from 'phaser';
import { GAME_WIDTH, GAME_HEIGHT, COLORS } from './constants';

// Modern (non-pixel-art) rendering: antialiased, smooth scaling. WebGL when
// available so we can layer glow/blend effects later.
export function createGameConfig(
  scenes: Phaser.Types.Scenes.SceneType[],
): Phaser.Types.Core.GameConfig {
  return {
    type: Phaser.AUTO,
    parent: 'game',
    width: GAME_WIDTH,
    height: GAME_HEIGHT,
    backgroundColor: COLORS.bgDeep,
    antialias: true,
    roundPixels: false,
    // Global smooth rendering for the toon enemies; the pixel-art player
    // texture is set to NEAREST individually in PreloadScene to stay crisp.
    pixelArt: false,
    scale: {
      mode: Phaser.Scale.FIT,
      autoCenter: Phaser.Scale.CENTER_BOTH,
    },
    physics: {
      default: 'arcade',
      arcade: {
        gravity: { x: 0, y: 0 },
        debug: false,
      },
    },
    render: {
      powerPreference: 'high-performance',
    },
    scene: scenes,
  };
}
