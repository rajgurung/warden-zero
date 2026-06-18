import Phaser from 'phaser';
import { GAME_WIDTH, GAME_HEIGHT, COLORS } from './constants';

// Smooth (non-pixel-art) antialiased rendering. The world stays at logical
// 1280x720, but we render the canvas at the display's pixel density so it's
// crisp on high-DPI / Retina screens instead of being upscaled and blurry.
// Capped at 2x to keep the framebuffer (and GPU cost) reasonable.
export function createGameConfig(
  scenes: Phaser.Types.Scenes.SceneType[],
): Phaser.Types.Core.GameConfig {
  const dpr = Math.min(Math.max(window.devicePixelRatio || 1, 1), 2);
  return {
    type: Phaser.AUTO,
    parent: 'game',
    width: GAME_WIDTH,
    height: GAME_HEIGHT,
    backgroundColor: COLORS.bgDeep,
    antialias: true,
    roundPixels: false,
    pixelArt: false,
    scale: {
      mode: Phaser.Scale.FIT,
      autoCenter: Phaser.Scale.CENTER_BOTH,
      zoom: dpr,
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
