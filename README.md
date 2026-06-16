# Warden Zero

A modern, top-down arena shooter that runs in the browser. Survive five waves
of corrupted creatures, pick an upgrade between each, and clear the arena.

Built with Vite, TypeScript, and Phaser 3.

## Controls

| Input        | Action |
| ------------ | ------ |
| WASD         | Move   |
| Mouse        | Aim    |
| Left Click   | Shoot  |
| Q            | Dash (brief i-frames, on cooldown) |
| E            | Bomb (radial blast, on cooldown)   |
| Esc          | Pause  |
| 1 / 2 / 3    | Pick upgrade (between waves) |

## Gameplay

- Five waves of escalating difficulty (grunts, runners, tanks).
- Between waves, choose one of three randomised upgrades. Most stack.
- Enemies drop hearts (heal) and coins (score) on death.
- Clear wave 5 to win; reach zero health and it's game over.

## Local development

```bash
npm install
npm run dev      # http://localhost:5173
```

## Quality / build

```bash
npm run check    # type-check only (tsc --noEmit)
npm run build    # type-check + production build to dist/
npm run preview  # serve the production build locally
```

## Deployment

The build is a fully static site in `dist/` — deploy anywhere that serves
static files.

**Vercel** (recommended)
- Framework preset: Vite
- Build command: `npm run build`
- Output directory: `dist`
- Install command: `npm install`

**Netlify**
- Build command: `npm run build`
- Publish directory: `dist`

## Project structure

```
src/
  main.ts                 # Phaser game bootstrap + scene list
  config/                 # constants, palette, player stats, enemies, waves, upgrades
  scenes/                 # Boot, Preload, MainMenu, Game, Upgrade, Pause, GameOver, Victory
  entities/               # Player, Bullet, Enemy, Pickup
  systems/                # Weapon, EnemySpawn, Wave, Upgrade, Effects, Sound
  ui/                     # Hud, Button, UpgradeCard
  types/                  # shared game types
```

## Assets

The MVP generates all textures procedurally (no external art). SFX play through
`SoundSystem`, which is silent until audio files are loaded in `PreloadScene` —
drop `.wav`/`.mp3` files keyed by name (`shoot`, `enemy_die`, `bomb`, ...) to
enable sound with no code changes.
