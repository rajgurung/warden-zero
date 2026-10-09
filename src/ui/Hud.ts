import type { Upgrade } from '../config/upgrades';

const $ = <T extends HTMLElement = HTMLElement>(id: string): T => document.getElementById(id) as T;

// DOM HUD + overlays. The 3D canvas renders underneath; this stays crisp
// at any resolution and is trivial to restyle.
export class Hud {
  readonly el = {
    hud: $('hud'),
    hpBar: $('hp-bar'),
    hpText: $('hp-text'),
    wave: $('wave-num'),
    waveTotal: $('wave-total'),
    score: $('score'),
    coins: $('coins'),
    boss: $('boss'),
    bossBar: $('boss-bar'),
    xpBar: $('xp-bar'),
    lvl: $('lvl'),
    chipDash: $('chip-dash'),
    chipBomb: $('chip-bomb'),
    banner: $('banner'),
    reticle: $('reticle'),
    joy: $('joy'),
    knob: $('joy').querySelector('i') as HTMLElement,
    btnDash: $('btn-dash'),
    btnBomb: $('btn-bomb'),
    loading: $('loading'),
    menu: $('menu'),
    upgrade: $('upgrade'),
    upLevel: $('up-level'),
    cards: $('cards'),
    pause: $('pause'),
    result: $('result'),
  };
  private bannerTimer = 0;

  setHealth(hp: number, max: number): void {
    this.el.hpBar.style.width = `${Math.max(0, (hp / max) * 100)}%`;
    this.el.hpText.textContent = `${Math.max(0, Math.ceil(hp))} / ${max}`;
  }

  setWave(n: number, total: number): void {
    this.el.wave.textContent = String(n);
    this.el.waveTotal.textContent = String(total);
  }

  setScore(score: number, coins: number): void {
    this.el.score.textContent = score.toLocaleString();
    this.el.coins.textContent = String(coins);
  }

  setXp(xp: number, need: number, level: number): void {
    this.el.xpBar.style.width = `${Math.min(100, (xp / need) * 100)}%`;
    this.el.lvl.textContent = String(level);
  }

  setBoss(hp: number | null, max = 1): void {
    this.el.boss.hidden = hp === null;
    if (hp !== null) this.el.bossBar.style.width = `${Math.max(0, (hp / max) * 100)}%`;
  }

  // 0..1 readiness for each ability.
  setCooldowns(dash: number, bomb: number): void {
    const set = (chip: HTMLElement, btn: HTMLElement, k: number) => {
      (chip.querySelector('i') as HTMLElement).style.width = `${k * 100}%`;
      chip.classList.toggle('cooling', k < 1);
      btn.classList.toggle('cooling', k < 1);
    };
    set(this.el.chipDash, this.el.btnDash, dash);
    set(this.el.chipBomb, this.el.btnBomb, bomb);
  }

  banner(text: string, danger = false): void {
    const b = this.el.banner;
    b.textContent = text;
    b.classList.toggle('danger', danger);
    b.classList.add('on');
    clearTimeout(this.bannerTimer);
    this.bannerTimer = window.setTimeout(() => b.classList.remove('on'), 1700);
  }

  showUpgrades(level: number, choices: Upgrade[], stacks: (u: Upgrade) => number, onPick: (u: Upgrade) => void): void {
    this.el.upLevel.textContent = String(level);
    this.el.cards.replaceChildren(
      ...choices.map((u, i) => {
        const b = document.createElement('button');
        b.type = 'button';
        b.className = 'card';
        b.id = `upgrade-${u.id}`;
        const n = stacks(u);
        b.innerHTML = `<span class="k">${i + 1}</span><span class="n"></span><span class="d"></span><span class="s">${n}/${u.maxStacks}</span>`;
        (b.querySelector('.n') as HTMLElement).textContent = u.title;
        (b.querySelector('.d') as HTMLElement).textContent = u.description;
        b.addEventListener('click', () => onPick(u));
        return b;
      }),
    );
    this.el.upgrade.hidden = false;
    (this.el.cards.firstElementChild as HTMLElement | null)?.focus();
  }

  showResult(win: boolean, s: { wave: number; score: number; kills: number; level: number; ms: number }): void {
    $('res-eyebrow').textContent = win ? 'Victory' : 'Run over';
    $('res-title').textContent = win ? 'The Colossus falls' : 'The line broke';
    $('res-wave').textContent = String(s.wave);
    $('res-score').textContent = s.score.toLocaleString();
    $('res-kills').textContent = String(s.kills);
    $('res-level').textContent = String(s.level);
    const sec = Math.floor(s.ms / 1000);
    $('res-time').textContent = `${Math.floor(sec / 60)}:${String(sec % 60).padStart(2, '0')}`;
    this.el.result.hidden = false;
  }
}
