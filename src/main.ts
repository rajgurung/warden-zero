import Phaser from 'phaser';
import { createGameConfig } from './config/gameConfig';
import { BootScene } from './scenes/BootScene';
import { PreloadScene } from './scenes/PreloadScene';
import { MainMenuScene } from './scenes/MainMenuScene';
import { GameScene } from './scenes/GameScene';
import { UpgradeScene } from './scenes/UpgradeScene';
import { PauseScene } from './scenes/PauseScene';
import { GameOverScene } from './scenes/GameOverScene';
import { VictoryScene } from './scenes/VictoryScene';

const config = createGameConfig([
  BootScene,
  PreloadScene,
  MainMenuScene,
  GameScene,
  UpgradeScene,
  PauseScene,
  GameOverScene,
  VictoryScene,
]);

// eslint-disable-next-line no-new
new Phaser.Game(config);
