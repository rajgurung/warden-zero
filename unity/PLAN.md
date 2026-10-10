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

## Phase 4: Operation Greenfang (playable; open items below)
Playable end to end in WebGL and covered by tests (`GreenfangTests`, `WorldTests`).
Debug URL: `?phase=push|advance|warlord|extraction`.
- [x] Mission mode as a second scene (`Greenfang.unity`), entered from the main menu (`e3bf116:src/scenes/JungleScene.ts`, `MainMenuScene.ts`)
- [x] Jungle arena in the 3D look: ground, trees (trunks block the Warden only), bushes, decor, tree-line border, fireflies, green fog
- [x] Phases: insertion, push (beacon Alpha), advance (beacon Bravo), warlord, extraction (survive 40 s), result screen with objectives
- [x] Capture beacons with presence-based hold (soft-lock fix in `e3bf116`)
- [x] Warlord mini-boss (telegraphed pound with knockback, summons), Spitter
- [x] Artillery and air strikes with friendly-fire self-damage, air charges rearmed at beacons, kill tally (`e3bf116:src/systems/StrikeSystem.ts`)
- [x] Waypoint arrow, objective line, capture bar, strike boxes (`e3bf116:src/ui/JungleHud.ts`)
- [x] Polish: air-strike strip dimmed, jungle floor and ambient lifted
- [x] Behind tree canopies the Warden shows as an x-ray silhouette
- [x] Uses the 3D Warden (shared CreateCore)
- [ ] Touch: strikes fire at the nearest enemy (no cycling or aiming on touch yet)

## 3D Warden and aim (done)
- [x] Study Babylon `4e34f7e` hero (eased yaw, muzzle from the gun, aim plane at gun height) and Phaser `e3bf116` targeting (shots straight from the player to the cursor)
- [x] Import Tripo Warden (`warden-3d` branch) and rifle into `Assets/Art/Warden3D` (`Warden3DImport` sets Humanoid, avatar copy, Bake Into Pose, loops)
- [x] Root drift checked in PlayMode: lowest foot stays 0.06-0.26 m (model metres) through two run loops, hips stay within 0.12 m of the Warden, so the FBX clips are used (no glTFast fallback)
- [x] `WardenModelView`: eased 360 degree yaw, idle/walk/run blend (run plays backwards when backpedalling), upper-body `fire` layer (Avatar Mask), hit flinch, defeat_03 death, rifle in the right hand along the facing; `WardenSpriteView` kept behind `SceneBuilder.UseModelWarden`
- [x] Aim: cursor projected on the muzzle-height plane (1.4 m); bolts fly from the muzzle at that point using the real aim, not the eased body yaw; screen-space reticle at the pointer
- [x] Readability: Warden draws after enemy sprites, ground ring kept, red flash on hurt instead of blinking out; cyan emission mask from the texture glows with bloom
- [x] Two-handed hold: the rifle hangs off the body frame at chest height; Humanoid IK puts the right hand on the pistol grip and the left on the handguard (elbow hints below and outside), checked to stay within 5 cm through running and a 360 degree sweep
- [x] Crowd and scenery readability: draws above enemy sprites, cyan fresnel rim, flat x-ray silhouette where walls or trees hide him
- [x] Carbine stance (procedural, `WardenHandIK`): low ready when not firing, shouldered within ~0.15 s when firing (stock in the right shoulder pocket, cheek weld, rifle on the aim line), back ~0.3 s after the last shot; per-shot kick; vertical foregrip for the support hand; wrists rotated onto both grips; elbow hints (firing elbow out, support elbow down); slight lean via LookAt
- [x] Colours: the Warden draws after the sprites with his true queue (URP was resetting it to opaque, letting the x-ray paint over him), hurt tint only on his own material (it was turning the x-ray and rim white); x-ray is #4fd1ff at 45%, rim thin and below the bloom threshold
- [ ] Not done: a rifle-carry run/idle clip (Tripo has none; Mixamo rifle packs would fit the rig); fingers can't curl (no finger bones)

## Build size log
| After | Download |
| --- | --- |
| Visual parity (2bd5def) | 14.67 MB |
| Phase 1: core loop | 14.98 MB |
| Phase 2: full arena content | 14.98 MB |
| Phase 3: ship-ready | 14.98 MB |
| Phase 4 WIP: Greenfang | 15.18 MB |
| 3D Warden (512 px textures) | 16.55 MB |
| Review fixes, hand IK, x-ray | 16.56 MB |
| Stance and colour polish | 16.31 MB |
| M1 drop slice (first load / with jungle bundles) | 20.08 MB / 56.40 MB |
