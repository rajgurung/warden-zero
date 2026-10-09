import { Stage, IS_TOUCH } from './render/Stage';
import { Hud } from './ui/Hud';
import { Input } from './systems/Input';
import { Sound } from './systems/Sound';
import { Effects } from './systems/Effects';
import { Game } from './core/Game';

const canvas = document.getElementById('game') as HTMLCanvasElement;
const hud = new Hud();
const stage = new Stage(canvas);
const sound = new Sound();
const fx = new Effects(stage, sound);
const input = new Input(stage, canvas, {
  joy: hud.el.joy,
  knob: hud.el.knob,
  reticle: hud.el.reticle,
  dash: hud.el.btnDash,
  bomb: hud.el.btnBomb,
});
const game = new Game(stage, hud, input, fx);

if (IS_TOUCH) {
  document.body.classList.add('touch');
  document.getElementById('controls')!.innerHTML =
    '<dt>Left thumb</dt><dd>Drag anywhere to move</dd>' +
    '<dt>Auto</dt><dd>Aims and fires at the nearest enemy</dd>' +
    '<dt>Dash · Bomb</dt><dd>Buttons on the right</dd>';
}

const show = (el: HTMLElement, on: boolean) => (el.hidden = !on);
const play = () => {
  sound.unlock();
  show(hud.el.menu, false);
  show(hud.el.result, false);
  show(hud.el.pause, false);
  show(hud.el.hud, true);
  show(hud.el.btnDash, IS_TOUCH);
  show(hud.el.btnBomb, IS_TOUCH);
  game.start();
  canvas.focus();
};
const toMenu = () => {
  game.quitToMenu();
  show(hud.el.result, false);
  show(hud.el.pause, false);
  show(hud.el.hud, false);
  show(hud.el.btnDash, false);
  show(hud.el.btnBomb, false);
  show(hud.el.menu, true);
};

document.getElementById('btn-play')!.addEventListener('click', play);
document.getElementById('btn-retry')!.addEventListener('click', play);
document.getElementById('btn-menu')!.addEventListener('click', toMenu);
document.getElementById('btn-quit')!.addEventListener('click', toMenu);
document.getElementById('btn-resume')!.addEventListener('click', () => game.pause(false));
document.addEventListener('visibilitychange', () => {
  if (document.hidden) game.pause(true);
});

stage.scene.executeWhenReady(() => {
  hud.el.loading.hidden = true;
  show(hud.el.menu, true);
});
stage.engine.runRenderLoop(() => stage.scene.render());
