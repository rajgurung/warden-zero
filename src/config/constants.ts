// Core dimensions. Internal render size is 16:9; the scale manager fits it
// to the viewport so it embeds cleanly in the portfolio iframe.
export const GAME_WIDTH = 1280;
export const GAME_HEIGHT = 720;

// Render depth bands. Characters/walls/shadows sort by their y (0..720) for
// the 3/4 look; bullets and FX sit above them, HUD on top.
export const DEPTH = {
  bullet: 900,
  fx: 920,
  hud: 1000,
} as const;

// Scene keys, centralised to avoid stringly-typed typos across the codebase.
export const SCENES = {
  BOOT: 'BootScene',
  PRELOAD: 'PreloadScene',
  MAIN_MENU: 'MainMenuScene',
  GAME: 'GameScene',
  UPGRADE: 'UpgradeScene',
  GAME_OVER: 'GameOverScene',
  VICTORY: 'VictoryScene',
  PAUSE: 'PauseScene',
} as const;

// Modern cinematic palette: deep space-navy base with neon cyan/magenta
// accents and a warm gold highlight. Numbers (0x...) for Phaser tint/fill,
// CSS strings for any DOM/text styling.
export const COLORS = {
  bgDeep: 0x05070f,
  bgMid: 0x0b1020,
  panel: 0x141b30,
  panelEdge: 0x243154,
  grid: 0x1a2240,
  accent: 0x4fd1ff, // neon cyan
  accentSoft: 0x2a8fc0,
  magenta: 0xff5ca8,
  gold: 0xffd75a,
  health: 0xff4d5e,
  healthBack: 0x3a1620,
  pickup: 0x6cff9b,
  enemy: 0xff7a59,
  enemyAccent: 0xb15cff,
  textBright: 0xe6ecff,
  textDim: 0x8a96b8,
} as const;

export const CSS = {
  accent: '#4fd1ff',
  magenta: '#ff5ca8',
  gold: '#ffd75a',
  textBright: '#e6ecff',
  textDim: '#8a96b8',
} as const;

export const FONT = {
  display: '"Inter", system-ui, sans-serif',
  body: '"Inter", system-ui, sans-serif',
} as const;
