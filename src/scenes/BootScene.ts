import Phaser from 'phaser';
import { SCENES } from '../config/constants';

// Lightweight scene: sets global defaults, then hands off to preload.
export class BootScene extends Phaser.Scene {
  constructor() {
    super(SCENES.BOOT);
  }

  create(): void {
    // Crisp input, and make the canvas the focus target for keyboard.
    this.input.mouse?.disableContextMenu();
    this.scene.start(SCENES.PRELOAD);
  }
}
