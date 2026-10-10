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
- [ ] `publish-web.sh`, commit `web/`
- [ ] Measure: first load, total size, fps High (real GPU) and Low (mobile emulation)
