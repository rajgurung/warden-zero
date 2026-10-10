# Warden Zero Unity: feature parity plan

Goal: bring the Unity build to full parity with the Babylon game on `main`, then port
Operation Greenfang from the Phaser v1 history. The Warden stays a sprite, drawn by one
component (`WardenSpriteView`) so a 3D model can replace it later.

Every phase must end with: zero compile errors and warnings, EditMode and PlayMode
tests passing (with new tests per system), a WebGL build in `Build/WebGL`, browser
screenshots inspected, and a commit. Scene, prefab and material generation stays in
`Assets/Editor/SceneBuilder.cs`.

Sources: Babylon = `main` (`src/`). v1 = Phaser code at commit `e3bf116`
(`git show e3bf116:<path>`).

## Phase 0: plan
- [x] This file

## Phase 1: arena core loop
- [x] Run state and mutable player stats (Babylon `src/types/game.ts`, `src/config/playerStats.ts`)
- [x] Warden rendering isolated in `WardenSpriteView` (facing, poses, muzzle point)
- [x] Main menu, pause (Esc / P, and on losing focus), game over and victory result screens (`index.html`, `src/style.css`, `src/main.ts`, `Game.start/pause/finish`)
- [x] XP gems: drop on kill, pop, bob, magnet range, end-of-wave vacuum, +50 score (`Game.dropGem/updateGems`)
- [x] Levelling (`xpToNext = 8 + (level-1)*4`) and the 3-card upgrade picker, keys 1/2/3, paused while picking, chained level-ups, heal 25 when all upgrades are maxed (`Game.levelUp/pickUpgrade`)
- [x] All 16 upgrades with stack caps (`src/config/upgrades.ts`, `src/systems/UpgradeSystem.ts`)
- [x] Multishot spread (8 deg), crit (gold bolt, x2), piercing, bigger and faster bullets, lifesteal, regen (1 s tick), max health, speed, magnet, dash cooldown (`Game.fire`, `Game.frame`)
- [x] Hearts (+18 HP) and coins (+1 coin, +25 score), 25% drop chance (35% heart), 8 s life with blink (`Game.dropPickup/updatePickups`)
- [x] Bomb (E / right mouse): cooldown, radius, damage, gold shockwave ring, burst, shake, `bomb` sound (`Game.tryBomb`, `Effects.bombBlast`)
- [x] HUD: health, wave, score and coins, dash and bomb chips with cooldown, XP bar with level, banner (`src/ui/Hud.ts`)
- [x] Wave clear needs gems collected too; Warden recentred between waves (`Game.checkWaveCleared`)

## Phase 2: full arena content
- [x] All 11 enemy types (`src/config/enemies.ts`) using the v1 art: grunt and runner walk cycles; skeleton, spider and demon pixel tiles with point filtering; tints for swarmer, brute, tank, boss, spitter and warlord; v1 sizing (visual height = radius x 5 px, `e3bf116:src/entities/Enemy.ts`)
- [x] Spitter ranged projectiles (`e3bf116:src/scenes/JungleScene.ts`; arena waves never spawn spitters, so they appear in Greenfang)
- [x] All 8 waves (`src/config/waves.ts`), wave banners, wave-clear flow
- [x] Colossus boss: banner, summons 4 swarmers every 4 s, boss health bar, death and victory (`Game.startBossFight`)
- [x] Debug shortcuts: `?boss` and `?wave=N` URL parameters

## Phase 3: ship-ready
- [x] Touch controls: drag-anywhere stick, auto-aim at the nearest enemy within 22 m, auto-fire, dash and bomb buttons (`src/systems/Input.ts`, `Game.frame`)
- [x] All SFX wired where Babylon plays them, with per-clip throttling (`src/systems/Sound.ts`)
- [x] Responsive canvas that fills the window, tidy WebGL template (title, loading bar)
- [x] Performance check with a late wave on screen (real GPU fps)

## Phase 4: Operation Greenfang (WIP, paused for the 3D Warden)
Playable end to end in WebGL and covered by tests (`GreenfangTests`, `WorldTests`).
Debug URL: `?phase=push|advance|warlord|extraction`.
- [x] Mission mode as a second scene (`Greenfang.unity`), entered from the main menu (`e3bf116:src/scenes/JungleScene.ts`, `MainMenuScene.ts`)
- [x] Jungle arena in the 3D look: ground, trees (trunks block the Warden only), bushes, decor, tree-line border, fireflies, green fog
- [x] Phases: insertion, push (beacon Alpha), advance (beacon Bravo), warlord, extraction (survive 40 s), result screen with objectives
- [x] Capture beacons with presence-based hold (soft-lock fix in `e3bf116`)
- [x] Warlord mini-boss (telegraphed pound with knockback, summons), Spitter
- [x] Artillery and air strikes with friendly-fire self-damage, air charges rearmed at beacons, kill tally (`e3bf116:src/systems/StrikeSystem.ts`)
- [x] Waypoint arrow, objective line, capture bar, strike boxes (`e3bf116:src/ui/JungleHud.ts`)
- [ ] Polish: air-strike telegraph strip is far too bright; jungle floor reads almost black; check tree canopies hiding the Warden
- [ ] Switch to the 3D Warden once it lands
- [ ] Touch: strikes fire at the nearest enemy (no cycling or aiming on touch yet)

## 3D Warden and aim (priority, in progress)
- [ ] Study Babylon `4e34f7e` hero (eased yaw, muzzle from the gun) and Phaser `e3bf116` targeting
- [ ] Import Tripo Warden (`warden-3d` branch) and rifle into `Assets/Art/Warden3D`
- [ ] `WardenModelView`: smooth 360 degree yaw, locomotion, upper-body fire layer, hit and death clips, rifle on the right hand (sprite view kept behind a flag)
- [ ] Aim accuracy: bolts from the muzzle to the point under the cursor at muzzle height; reticle under the cursor
- [ ] Crowd readability and emissive visor with bloom

## Build size log
| After | Download |
| --- | --- |
| Visual parity (2bd5def) | 14.67 MB |
| Phase 1: core loop | 14.98 MB |
| Phase 2: full arena content | 14.98 MB |
| Phase 3: ship-ready | 14.98 MB |
| Phase 4 WIP: Greenfang | 15.18 MB |
