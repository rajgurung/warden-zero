# Milestone 1: the drop slice

Spec: `SPEC.md` on branch `campaign-spec`. Each step ends with zero warnings, green
EditMode and PlayMode tests, a WebGL build, inspected screenshots and a commit.

Debug URLs: `?campaign=extraction|flight|jump|jungle|walk|checkpoint` jump to each part.

## 1. Foundations
- [x] Packages: Addressables, Cinemachine
- [x] Quality levels High (desktop) and Low (phones), picked from the pointer type
- [x] Save data (PlayerPrefs): stage, checkpoint, health, level, upgrades, stats; round-trip tests

## 2. Campaign in the arena
- [x] Menu: Campaign, Continue (when a save exists), Arena, Operation Greenfang
- [x] Colossus down -> "Extraction inbound" -> chopper flies in and lands at the LZ
- [x] Hold the LZ ~20 s while enemies keep coming, then walk into the chopper to board
- [x] Save after boarding (stage 1 done)

## 3. Tripo assets
- [x] Chopper body (rotor-less), rotor (main, reused for tail), parachute canopy, checkpoint beacon
- [x] Log in `Assets/Art/Campaign/TRIPO_LOG.md`; budget 250 credits

## 4. Jungle patch (Addressables)
- [x] Jungle scene generated from code: terrain (~150 x 150 m) with CC0 ground textures, trees,
      ferns, grass, bushes, rocks, stream; sky, fog, light, post
- [x] Wider low-detail landscape for the view from the air
- [x] Scene and heavy assets in an Addressables group, loaded over HTTP from `web/`
- [x] Download starts in the background during Stage 1; loading screen fallback
- [x] `scripts/publish-web.sh` builds Addressables content
- [x] Credits in `Assets/ThirdParty/CREDITS.md`

## 5. Flight and jump
- [x] Flight cinematic (Cinemachine), ~15-20 s, skippable
- [x] Exit, freefall (steer, altitude HUD), deploy (Space / button), auto-deploy at minimum altitude
- [x] Canopy: steer, slower descent, flare; landing damage by touchdown speed
- [x] Land, chute released, camera settles into gameplay

## 6. Checkpoint A and saves
- [x] Beacon a short walk from the LZ; hold to capture; "Checkpoint A secured"
- [x] Save; "Milestone 1 complete" banner; back to the menu
- [x] Continue resumes at the last checkpoint

## 7. Touch and quality
- [x] Touch buttons: board, steer, deploy, skip
- [x] Quality override in the pause menu

## 8. Ship
- [x] Tests: flow, chute timing and hard landing, save round-trip, quality selection
- [x] `publish-web.sh`, commit `web/`
- [x] Measure: first load 20.1 MB, total 56.4 MB; uncapped Chrome on an M1 Pro: ~145 fps
      at High 1920x1080 in the jungle, ~320 fps with iPhone 13 emulation at Low

## 9. Jump realism and sound (play-test feedback)
- [x] No x-ray silhouette in set pieces
- [x] Door crouch, lean out, short tumble into a stable arch; steering leans, dives, brakes
- [x] Pilot chute, unfurl and inflate, opening jolt, pendulum swing upright; toggles, flare
- [x] Run-out or crouched hard landing; chute collapses and sinks
- [x] Chopper attitude: cruise nose-down, coordinated banks, hover drift, landing flare
- [x] Synthesised sound: rotor/turbine (3D, Doppler, cabin mix), wind, canopy, landing, jungle
- [x] Debug side camera and auto-flare for checking the poses

## 10. Review fixes
- [x] Loader: one load at a time, overlay blocks clicks, no pause while loading, menu
      cancels, failed load offers RETRY / MAIN MENU, no forced timeScale
- [x] Bundles released after the background download and on Campaign.End
- [x] DEPLOY button for the whole freefall; Continue at checkpoint A ends on the result
- [x] Saves with an unknown place are ignored
- [x] Low tier: impostors at half distance, trees to 120 m, a third of the ground cover
- [x] Generated Addressables files ignored; Tripo task records without storage keys
- [x] Tests: double load, menu mid-load, failed load, jungle to arena/Greenfang, death in the hold

## 11. Camera and stance (play-test feedback)
- [x] Behind view: Cinemachine over-the-shoulder camera, mouse look with pointer lock, centre
      crosshair, camera-relative movement, legs turn toward strafes
- [x] Camera collision against trunks, rocks, walls and the ground; foliage fades near the lens
      and around the Warden
- [x] V / VIEW toggle in the arena and the jungle, remembered per stage; touch look and FIRE
- [x] Freefall and canopy chase cameras; landing settles behind him
- [x] Upright relaxed rest stance (the fire clip's combat lean only while firing), rifle at a
      low ready across the body with both hands on it
- [x] Banner fits portrait phones; the BOARD button stays on screen

## 12. Drop polish (play-test feedback, Tripo clips and HALO gear)
- [x] Relaxed idle (idle_relaxed) with an occasional look-around; rifle held at a low ready
- [x] Freefall: calm procedural arch, no flutter (fall_loop compared, not used); clean door exit
- [x] Canopy: canopy_hold arms, lines to the hands, toggles pull a hand down
- [x] Landing: land clip into the idle; hard landing deeper; smooth re-grip of the rifle
- [x] HALO pack, helmet and altimeter from boarding to landing; pack left on the ground
- [x] Foliage fade under the canopy near the ground; arena behind-view fill; no x-ray tint behind him
