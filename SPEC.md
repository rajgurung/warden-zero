# Project: Warden Zero Campaign

## Goal

Turn Warden Zero from a single arena into a staged campaign of 45+ minutes. Stage 1 is the current arena. Beating it calls in a chopper that extracts the Warden, flies him out and drops him over the jungle: freefall, parachute, landing. Stage 2 is a realistic, open jungle he crosses from checkpoint to checkpoint, on foot with real platforming and by vehicle, fighting soldiers and creatures. The visual bar is the Tripo 3D Warden: every asset should look that good, as realistic as a web game can be. It must play well on desktop and on phones.

## Scope

### In Scope

- Campaign flow: Stage 1 (arena) → extraction and drop → Stage 2 (jungle), with level and upgrades carried over
- Checkpoint saves (in the browser), so a run can be continued on another visit
- Chopper set piece: arrives after the Colossus, hold the landing zone, board, flight
- Jump set piece: exit the chopper, steer the freefall, deploy the parachute, steer to a landing zone
- Stage 2 jungle: a large wide-corridor map (not free roam) with checkpoints A → B → C → ... → extraction
- Real platforming: jump, vault, ledge grab and climb, height levels
- Vehicles: a drivable vehicle (jeep or quad) and on-rails turret rides on some stretches
- Enemies: 3D human soldiers and 3D creatures (no 2D sprites in Stage 2)
- Greenfang's strikes and Warlord folded into Stage 2
- Cameras: context camera (over-the-shoulder for traversal, driving and platforming; high view for combat; cinematic for set pieces) plus a player toggle
- Touch controls for everything above
- Quality levels (High for desktop, Low for phones, picked automatically) and streaming Stage 2 in while Stage 1 plays

### Out of Scope

- Free-roam sandbox open world
- Multiplayer, accounts or cloud saves
- Native desktop or console builds (possible later, not now)
- Changing Stage 1 gameplay beyond what the campaign flow needs
- Mixamo re-rig of the Warden for its own sake (done only when soldiers need Mixamo anyway)

## Technical Approach

- Unity 6.3 LTS, URP, WebGL 2, existing project in `unity/`. Deploy with `scripts/publish-web.sh` and a PR (Vercel serves `web/`).
- Content streaming: Addressables for Stage 2 so the first load stays small; Stage 2 downloads in the background during Stage 1.
- Size budget: total up to ~150 MB on desktop High; first load target under ~25 MB; Low tier for phones with smaller textures and lighter foliage. Watch iOS Safari memory limits.
- Performance target: 60 fps on a mid-range laptop at High, 30+ fps on a recent phone at Low.
- Cameras: Cinemachine with a context switcher and a toggle key or button.
- Movement: a character controller with jump, vault, ledge grab and climb; the existing twin-stick aiming in the high view, centre-crosshair aiming over the shoulder.
- Vehicles: arcade handling (not a physics sim); turret rides on splines.
- Environment: Unity Terrain plus a realistic nature pack for trees and foliage (free or bought, CC0 or Asset Store licence), baked or mixed lighting, fog and ambience.
- Assets: Tripo for hero props, vehicles, the chopper (rotors as separate parts), structures and characters; Mixamo for humanoid rigs and animations (soldiers, and the Warden's finger bones and rifle anims). Tripo credits are topped up as needed.
- Saves: PlayerPrefs (IndexedDB on WebGL) at each checkpoint: stage, checkpoint, health, level, upgrades, stats.
- Same working pattern as before: one engineer agent per milestone, tests for each system, a reviewer agent, a PR with a Vercel preview, and the user play-tests before merging.

## Milestones

1. **The drop slice.** Stage 1 → chopper extraction → flight → jump, freefall and parachute → land in a small, realistic jungle patch → reach checkpoint A (saved). Basic quality levels and touch controls for the set pieces. Proves the wow moment and the realism level.
2. **Traversal.** Over-the-shoulder camera, context switching and toggle; platforming (jump, vault, ledge grab, climb); touch controls for both cameras.
3. **Vehicles.** Drivable vehicle and on-rails turret ride, with touch controls.
4. **Stage 2 content.** The full jungle corridor and its checkpoints, 3D soldiers and creatures, strikes and the Warlord, checkpoint saves throughout, extraction at the end.
5. **Realism and performance.** Streaming, quality tiers tuned on real phones, weather, ambience and audio, final size and frame-rate pass.

## Key Flows

1. Start a campaign → play the arena (8 waves, Colossus) → "Extraction inbound" → hold the landing zone → board the chopper.
2. Flight cinematic → jump → steer the freefall → deploy the chute (too late hurts) → steer to the landing zone → land.
3. Cross the jungle: run, jump and climb over obstacles; drive between distant checkpoints; ride a turret on set stretches; clear each checkpoint's fight; each checkpoint saves.
4. Leave and come back → "Continue" resumes at the last checkpoint with the same health, level and upgrades.
5. On a phone, the same flow works with touch controls at the Low quality level.

## Success Criteria

- [ ] A full run (arena → drop → jungle → extraction) takes 45+ minutes and has no soft-locks
- [ ] The drop sequence plays on desktop and phone, and is the highlight of a play-test
- [ ] Stage 2 assets match the 3D Warden's quality bar (user sign-off)
- [ ] Desktop High: 60 fps on a mid-range laptop; phone Low: 30+ fps on a recent iPhone and Android
- [ ] First load under ~25 MB; total download under ~150 MB; no memory crash on iOS Safari
- [ ] Checkpoint saves survive closing the tab
- [ ] Both cameras and all traversal, vehicle and combat actions work with touch
- [ ] Every milestone ships to the live site through a reviewed, tested PR

## Risks and Unknowns

- iOS Safari memory crashes with a large, realistic world: quality tiers, Addressables streaming, texture budgets, test on a real iPhone from milestone 1.
- Realism vs. web size and speed: set budgets per milestone and measure every build.
- Two cameras and platforming on touch screens are hard to get right: prototype touch controls in milestone 2, not at the end.
- Asset consistency (Tripo props, a nature pack and the Warden in one style): build a style guide and a reference scene in milestone 1.
- Tripo limits: vehicles and the chopper need separate moving parts; characters need weapons kept separate; animations need drift checks.
- Mixamo is old and sometimes unreliable: have a fallback (Asset Store animation packs).
- Scope: this is a large game. Ship and play-test each milestone before starting the next, and cut what doesn't earn its cost.
- Asset licences: nature packs and animations must allow commercial use.

## Open Questions

- Setting and story: names for the stages, why the Warden drops into the jungle, what the extraction at the end leads to (Stage 3?).
- Which vehicles exactly (jeep, quad, boat on a river?).
- Soldier types and creature types for Stage 2, and how they behave (cover, flanking, packs).
- Does the standalone arena and the old Greenfang mode stay in the menu alongside the campaign?
- Budget for paid assets (nature pack, animation packs) and further Tripo top-ups.
