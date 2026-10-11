# Milestone 2: Traversal

Spec: `SPEC.md` (Milestones 2, Risks). Base: `main` after the M1 polish (behind view, V / VIEW toggle, camera collision and foliage fade are already in). Branch: `m2-traversal`. Each iteration ends with zero warnings, green EditMode and PlayMode suites (currently ~43 / ~96), a WebGL build, inspected screenshots (`Snapshots.Capture`) and a commit. The PR gets a Vercel preview and the user play-tests it before merging.

**What M2 adds.**
- The camera switches by context, and a manual override still wins.
- A platforming moveset: jump, vault, mantle, ledge grab and hang, shimmy, wall climb and drop.
- A short traversal course on the trail from the LZ to checkpoint A.
- Touch controls for all of it in both views.

---

## 0. Decisions

### 0.1 Movement: a custom height and ledge system, not CharacterController

The plan extends the existing collision maths with authored solid blocks (`World.Blocks`) and adds a pure-C# `Traversal` state machine. Reasons:

1. **Wasm size and first load.** The project strips engine code (`stripEngineCode: 1`), and the scene builders delete every collider, including the `TerrainCollider`. A CharacterController pulls PhysX back into the wasm, which costs first-load MB on a 20.1 MB / ~25 MB budget, plus heap on iOS.
2. **One world model.** Camera collision (`CameraCollision`), crosshair aim, bolts (`WorldCast.InScenery`), trunks and rocks all read `World`. Colliders would make a second world that has to agree with the first. Blocks added to `World` and `WorldCast` give the camera, aim and bolts the obstacles for free.
3. **Testable.** Blocks and the state machine run in EditMode with no scene, like `WorldTests` and `WorldCastTests` today.
4. **The content suits it.** Every obstacle is authored: a log, a crate, a ruin wall, a rock ledge. Each is one yawed box (or a few) with known top edges. Ledges come straight from box edges, so no geometry probing is needed.

Cost: no slopes on blocks, and no dynamic objects. Neither is in scope. If M3 vehicles need real physics, that decision is revisited there, for vehicles only.

### 0.2 Keys

- **Arena (Stage 1 unchanged):** Space dashes, as before. Shift also dashes.
- **Jungle:** Space does the context move (jump, vault, climb, or up while hanging). Shift dashes. C or Ctrl drops or lets go. V toggles the camera.

`World.TraversalEnabled` is set by `JungleStage.Awake` and cleared by `World.UseArena`.

### 0.3 Heights

The Warden is ~2.6 m tall in game units: 1.9 m model at 1.4x scale. Heights are measured from his feet, in `GameConfig`, and tuned in iteration 4.

| Move | Rule |
| --- | --- |
| Step up (automatic) | Up to 0.5 m |
| Vault | Top 0.5-1.4 m, block depth no more than 2.5 m along the move, clear landing beyond |
| Mantle (climb up) | Top 1.4-2.6 m, headroom on top |
| Jump-grab ledge | Top 2.6-3.7 m (jump apex ~1.1 m plus reach) |
| Wall climb | Only faces flagged `Climbable` (vines), any height, ending in a mantle |
| Drops | No damage up to 5 m. Above that, a hard landing (the existing `land` clip, deeper), scaled damage, never lethal (same rule as the parachute) |

Assists: jump with coyote time 0.12 s and an input buffer of 0.15 s; 40% air control. He can fire while airborne. He cannot fire while vaulting, hanging or climbing; the rifle is slung.

---

## 1. Iterations

### 1. World blocks and casts
Gives the world solid boxes with heights and yaw.

- **Files:**
  - `Scripts/World.cs`: `Block` struct with centre, half extents, yaw, bottom, top, flags `Vault | Ledge | Climbable`, and per-edge ledge masks. Adds `World.Blocks`, `SurfaceAt(p, feetY)` (terrain or a block top within step height) and `PushOutOfBlocks(p, r, feetY)` (circle against a yawed box, only for blocks above step height).
  - `Scripts/WorldCast.cs`: yawed `Box` cast and `InScenery`, used by the camera, aim and bolts.
  - `Scripts/GameConfig.cs`: the height constants.
- **Tests (EditMode):**
  - `WorldTests`: circle against a yawed box; standing on a top; step-up limit; no push from a block below his feet.
  - `WorldCastTests`: a ray hits a yawed box; a point inside a box is scenery; the camera sphere cast stops at a box.
- **Acceptance:** the suites pass. The arena behaves the same (no blocks there).

### 2. Traversal core: ground, jump and fall
- **Files:**
  - New `Scripts/Traversal.cs`: plain C# with no MonoBehaviour. `Tick(input, dt) -> state`; states `Grounded`, `Air`; vertical speed, coyote time, buffer, landing impact.
  - `PlayerController.cs`: replace `next.y = World.HeightAt(next)` with `Traversal`; key routing from 0.2; adds `Airborne`, `VerticalSpeed` and `State` to `WardenPose`.
  - `WardenView.cs`: the `WardenPose` fields.
- **Tests:**
  - EditMode `TraversalTests`: apex height, coyote, buffer, landing on a block top, walking off an edge falls, step-up, drop damage curve and never lethal.
  - PlayMode: arena Space still dashes; jungle Space jumps and Shift dashes.
- **Acceptance:** in the jungle he jumps on flat ground and lands without jitter on slopes. Arena tests are unchanged.

### 3. Traversal moves
- **Files:**
  - `Traversal.cs`: `Vault`, `Mantle`, `Hang` (shimmy along the edge, clamped to its ends), `ClimbUp`, `WallClimb`, `Drop`. Each move is a timed curve from a start pose to an end pose, so it is deterministic and testable.
  - New `Scripts/TraversalProbe.cs`: finds the best edge or face in front of him (within 1.2 m and ±40° of the move or aim direction) and returns the context label `JUMP`, `VAULT`, `CLIMB`, `UP` or `DROP`.
- **Tests (EditMode):**
  - Vault only in its height band, and never into a block.
  - Mantle needs headroom.
  - Grab only while falling near the edge.
  - Shimmy stops at edge ends and at corners.
  - Climb-up lands on top.
  - Drop from a hang falls.
  - A wall climb ends in a mantle.
  - The probe picks the nearest valid edge; labels are right.
- **Acceptance:** every move works on test blocks in both views. In the high view, direction comes from the stick or WASD in world space.

### 4. Greybox course (before any credits are spent)
- **Files:**
  - `Editor/SceneBuilder.Jungle.cs`: place greybox blocks (Lit grey boxes) along `Trail` and save them on `JungleStage.blocks`; clear foliage and trunks and keep rocks off their footprints.
  - `Scripts/JungleStage.cs`: register `World.Blocks`; add `DropStart.Course`; debug URL `?campaign=course`.
  - `Scripts/Campaign.cs`: map `course` in `DropFor`.
- **Course** (trail from the LZ at (-14,-30) to checkpoint A at (22,32)):

| # | Where (about) | Beat |
| --- | --- | --- |
| 1 | (-9,-18) | Fallen log across the trail: vault (top 1.0 m) |
| 2 | (-6,-8) | Crate steps: 1.1 m crate (vault or mantle) up to a 2.2 m stack (mantle), then a jump to a rock shelf |
| 3 | (-3,0) ford | Flat-topped stepping blocks over the stream: jumps (the existing stepping stones stay as decoration) |
| 4 | (3,10) | Rock ledge 2.4 m: mantle |
| 5 | (12,20) | Ruin wall 5 m with a vine face: wall climb, walk the top, shimmy along a 3.2 m ledge, drop down |
| 6 | (17,27) | Terrace ledge 3.2 m: jump-grab and climb up, short of checkpoint A |

- **Tests (PlayMode `CourseTests`):**
  - A scripted bot drives inputs along waypoints from the LZ to checkpoint A in both views and must finish in under 120 s.
  - No block overlaps the LZ, the `Landed`, `NearCheckpoint` and `CheckpointA` spawn points, or the capture ring.
  - A walk around each beat never traps him: there are no enclosed pits.
- **Acceptance:** the whole course plays in greybox and the heights feel right on desktop. The user gets a quick look, and tuning happens before any art spend.

### 5. Touch prototype (early, per the spec's risk)
- **Files:**
  - `Scripts/TouchControls.cs`: `jumpButton` with a context label and `ConsumeJump`/`JumpHeld`; `dropButton`; the right-half look also works while hanging.
  - `Editor/SceneBuilder.cs` (`TouchButton` layout).
  - `PlayerController.cs`: touch routing.
- **Tests (PlayMode, `ForceTouch`):**
  - JUMP shows VAULT at the log and the press vaults.
  - DROP appears only while hanging.
  - A look drag while hanging turns the camera.
  - The high-view touch JUMP works.
  - A touch starting on JUMP never starts the stick.
- **Acceptance:** the course can be finished with touch only, in Chrome iPhone emulation, in both views. Do a first real-iPhone check here.

### 6. Tripo animations (see section 2), clip import and animator
- **Files:**
  - `Editor/Warden3DImport.cs`: generalise `M1Clips` into one clip table with a per-clip root-Y mode (`Feet`, `Original`, or `NotBaked` so a climb's rise becomes discarded root motion and plays in place). Add the traversal rows.
  - `Editor/SceneBuilder.Warden3D.cs`: base-layer states `JumpUp`, `JumpAir`, `JumpLand`, `Vault`, `Hang`, `ClimbUp`, `WallClimb`, `Drop`, with a `TravSpeed` parameter.
  - `Assets/Art/Warden3D/TRIPO_LOG_M2.md`
  - `ArtSource/M2Traversal/` (README, requests, tasks, contact sheet, rejected clips).
- **Tests:**
  - `ContentTests` (or a new `ClipTests`): each new clip exists with its length, loop flag and `isHumanMotion`.
  - Feet within 0.12 m of the ground in ground clips, sampled through an Animator graph like the M1 check.
- **Acceptance:** contact sheet reviewed; each clip accepted or rejected with a reason, like the M1 polish README.

### 7. Warden presentation: poses, hands and rifle sling
- **Files:**
  - `Scripts/WardenModelView.cs`: plays the traversal states from `WardenPose.State`; adds `Sling(bool)` (rifle to an `UpperChest` holder on his back, `handIK.weight` to 0, then back with `Grip(true, 0.35f)` as after landing).
  - New `Scripts/TraversalPose.cs`: procedural layer after the Animator, like `SkydivePose`. Hands go to the ledge points while hanging and mantling, the support hand plants on the obstacle top while vaulting, and the legs tuck over a vault. It is also the fallback for any rejected clip.
  - `Scripts/WardenHandIK.cs`: ledge IK goals, or `TraversalPose` sets the goals itself.
- **Tests (PlayMode `TraversalPlayTests`):**
  - Hanging: both hands within 0.15 m of the edge and feet below it.
  - Vault: the support hand reaches the top.
  - The rifle is slung during hang and climb and both hands are back on it within 0.5 s after.
  - Animator states follow the traversal states.
  - The camera never ends inside a block.
- **Acceptance:** side-cam (`&sidecam`) stills of each move look right; no snapping between states.

### 8. Tripo props in the course
- **Files:**
  - `Editor/CampaignImport.cs`: new `Traversal` set under `Assets/Art/Campaign/Traversal/`, same import path as `Gear` (real size, textures extracted).
  - `Editor/ArtImportSettings.cs`: 512 px textures, 1024 for the ruin wall on High; Low tier gets 512.
  - `Editor/SceneBuilder.Jungle.cs`: swap the greybox for the props and fit each block to the prop's measured bounds plus a hand-set top.
  - `Editor/AddressablesSetup.cs`: props go in the jungle group, so first load is unchanged.
  - `Assets/Art/Campaign/TRIPO_LOG.md` (M2 section)
  - `Assets/ThirdParty/CREDITS.md`, if needed.
- **Tests:**
  - Props imported.
  - Each block's footprint within 0.25 m of its prop's bounds.
  - The course bot still finishes.
  - Bolts stop on the crate and the wall.
- **Acceptance:** stills next to the Warden pass the style check (user sign-off). Jungle bundle growth under ~4 MB.

### 9. Context camera
- **Files:**
  - `Scripts/CameraFollow.cs`: the high view becomes a script-driven `CinemachineCamera` (`highCam`). The brain stays on during gameplay and both views blend: EaseInOut 0.6 s, a cut on `Snap()`.
  - New `Scripts/CameraContext.cs`: plain C# resolver for context, hysteresis, dwell and override (section 3).
  - Platforming framing on the shoulder rig.
  - `Scripts/Menus.cs`: pause item `CAMERA · AUTO / MANUAL`, PlayerPrefs `wz.cam.auto`.
  - `Editor/SceneBuilder*.cs`: `highCam` in each scene.
  - Debug URL `?campaign=skirmish`: spawns a few arena grunts at the ford to drive the combat context. Real jungle enemies are M4.
- **Tests:**
  - EditMode `CameraContextTests`: enter and exit thresholds; dwell; no automatic switch mid-air or mid-move; the override sticks until the next context change; Manual mode never switches by itself.
  - PlayMode:
    - The skirmish switches to high and back after the area is cleared.
    - V overrides.
    - Set pieces untouched (freefall, canopy and landing still end behind him).
    - The arena and Greenfang behave as before.
    - The existing `CameraTests` stay green.
- **Acceptance:** blends read smoothly and there is no ping-pong at the zone edge.

### 10. Touch polish for both cameras
- **Files:**
  - `TouchControls.cs` and `SceneBuilder.cs`: final layout (section 4), portrait variant, safe-area insets.
  - `Menus.cs`: look sensitivity (0.5-2x on `GameConfig.TouchLook`) and an auto-vault assist toggle.
  - `PlayerController.cs`: light aim friction in the behind view on touch, slowing look near an enemy under the crosshair (in the arena now, jungle enemies in M4); auto-vault of logs and crates below 1.0 m when running into them, with assist on (default on touch).
- **Tests (PlayMode):**
  - Every button rect is inside the screen at 16:9, 19.5:9 and 9:19.5.
  - No button overlaps another.
  - Sensitivity scales look.
  - Auto-vault fires only when running into a low obstacle.
- **Acceptance:** the course and the arena behind view are comfortable on a real iPhone and an Android phone at Low.

### 11. Prompts and feel
- **Files:**
  - `Scripts/Hud.cs`: desktop chip near the crosshair, e.g. `SPACE · VAULT` or `C · DROP`.
  - `Scripts/SynthAudio.cs`: synthesised jump, land, grab and scramble sounds (no new audio files).
  - Small camera dip on hard landings via `AddShake`.
- **Tests:** the HUD chip label follows the probe.
- **Acceptance:** play-test notes addressed.

### 12. Ship
- Update `README.md` (controls, cameras, traversal, debug URLs) and this file. Commit `tools/tripo/` to main from `warden-3d`.
- Run `scripts/publish-web.sh` and commit `web/` (LFS).
- Measure:
  - first load: target ≤ 21.5 MB, hard cap 25;
  - jungle bundles;
  - wasm heap in the jungle at Low (currently 286 MB; must not grow by more than ~20 MB);
  - fps at High 1080p and with iPhone emulation at Low;
  - a real iPhone Safari run of the course.
- PR with the Vercel preview, reviewer agent, user play-test, merge.

---

## 2. Tripo assets

Workflow: `T="node --env-file=.env.local tools/tripo/tripo.mjs"`, then `$T balance` before anything. The key is never printed or logged. Restore the tool with `git show warden-3d:tools/tripo/tripo.mjs` (and its README and `.py` helpers), and commit it in iteration 12.

Prices below are the ones the M1 logs actually charged:

| Service | Credits |
| --- | --- |
| Retarget | 10 per clip, max 5 per task |
| Convert | 5 |
| image-to-image | 10 |
| image-to-model | 30 |

Last logged balance: **215**. Requests and task records go in `ArtSource/M2Traversal/`, the same way as M1.

### 2.1 Animations

All retargets run on the existing rig task `e73d30f3-ec0e-466e-b3bf-553da60a7ac9` (rig v1.0, mixamo names), with the M1 options (`glb`, `bake_animation`, `export_with_geometry`, `animate_in_place`). The FBX is converted from the task id.

**Step A: free re-trims of takes already in the repo.** Take names were checked with `strings` on the FBXs.

| Want | Source take (file) | Idea | Status |
| --- | --- | --- | --- |
| Jump takeoff / air | `jump` (`warden_anims_m1_fall_look.fbx`), rejected in M1 as "repeated hops on the spot" | Trim one hop: takeoff and in-air sections, played by `Traversal`'s timing | Unverified that a single hop reads well |
| Wall climb loop | `climb` (same file), rejected in M1 as "a ladder climb, body rises 2.4 m" | A ladder climb is what a vine wall needs. Import with root Y **not baked** so it plays in place, and move the root in code | Unverified |
| Step-off / drop | `jump_down` 0-2.2 s (`warden_anims_m1_idle_land.fbx`; M1 used 2.2 s to the end as `land`) | Step-off and fall section before touchdown | Unverified what 0-2.2 s contains |
| Landing | `land` (already imported) | Reuse for drops over 2 m | Verified (M1) |
| Hang idle | none | Procedural (`TraversalPose`): arms up to the ledge, legs hanging (the `canopy_hold` arms are close; try that clip on the base layer first) | Fallback by design |

**Step B: one retarget batch** of presets not yet tried, all taken from the M1 preset list (v1.0 rig):

| Preset | Hoped use | Status |
| --- | --- | --- |
| `run_upstairs` | Mantle and step-up legs | Unverified |
| `flip` | Speed-vault candidate | Unverified; likely rejected |
| `lift_heavy` | Mantle push-up arms | Unverified; speculative |

Cost: 3 × 10 + 5 convert = **35 credits**.

Before spending, re-check the preset list on the Tripo docs page. The list in `ArtSource/M1Polish/README.md` is a snapshot. The v2.5 rig's `climb` and `jump` would need a new rig (+25) and different bones: not planned.

**No Tripo preset is a vault, ledge hang or pull-up.** Those come from procedural poses over the nearest clip, the same pattern as `SkydivePose` replacing `fall_loop`.

**Fallback for any clip that looks bad:**
1. Procedural in `TraversalPose`: aim limb bones in the body frame, plus hand IK to the ledge or obstacle points.
2. If that is not good enough on a key move: an Asset Store or Mixamo rifle and parkour pack. It is Mixamo-named, so it retargets to the Humanoid avatar, but it needs a commercial-use licence and the user's budget approval.

### 2.2 Props

Pipeline: image-to-image (style reference `docs/warden-hero.png`, olive, gunmetal and cyan for man-made items), then image-to-model (`v3.1-20260211`, texture + pbr, `auto_size`), then convert to FBX. Prompts ask for realistic PBR to match the jungle's CC0 ground and rock textures, not the comic style used for the gear.

| # | Prop | Use | face_limit | Credits |
| --- | --- | --- | --- | --- |
| P1 | `fallen_log`: mossy jungle trunk, ~1 m thick, 6 m long | Vault | 5000 | 45 |
| P2 | `supply_crate`: olive military crate, ~1.1 m, stackable | Vault, mantle, steps (instanced and rotated) | 2500 | 45 |
| P3 | `ruin_wall`: stone wall segment with hanging vines and a flat top, ~5 m by 6 m | Wall climb, ledge, shimmy | 8000 | 45 |
| P4 | `rock_ledge`: flat-topped rock shelf, ~2.4 m | Mantle, jump-grab, ford steps (scaled) | 6000 | 45 |

### 2.3 Budget

| Spend | Credits |
| --- | --- |
| Animations (Step B) | 35 |
| Props P1-P4 | 180 |
| **Total** | **215** |
| Retry reserve (one art re-roll plus one model) | ~40 |
| **Cap** | **~255** |

This uses the whole logged balance and leaves no reserve, so **ask the user for a top-up of ~50-100 credits** before step 8.

If there is no top-up: do P1-P3 plus the animations (170) and build P4 from the jungle's own procedural rocks (`JungleFlora` boulders squashed onto a flat-topped block). That is a code fallback, flagged as an exception to the Tripo-first rule.

---

## 3. Camera context rules

Contexts, resolved every frame by `CameraContext`:

| Context | When | View |
| --- | --- | --- |
| Set piece | `cameraFollow` disabled (flight, jump, canopy, landing) | Set piece's own shots, as today |
| Combat | A live enemy within 18 m, or he is inside a `CombatZone` (circle on `JungleStage`; used by M4, and by `?campaign=skirmish` now) | High |
| Platforming | A traversal state other than `Grounded`, or the probe sees an edge within 3 m | Behind, with platforming framing |
| Explore | Otherwise | Behind |

Platforming framing:
- shoulder offset eased toward 0.4 m, so a climb is centred;
- distance +0.6 m and pitch +6°, to see the top;
- when hanging, yaw recentres gently to face the wall only if there has been no look input for 1.0 s.

Rules:
1. **Hysteresis.** Combat starts at 18 m or on entering a zone. It ends only after 4 s with no live enemy within 25 m and outside all zones.
2. **Dwell.** At least 2 s between automatic switches.
3. **No switch mid-move.** An automatic switch never happens while airborne, vaulting, hanging or climbing. It waits until he is grounded.
4. **Blends.** EaseInOut 0.6 s between views. A cut on `Snap()` (a new run, the end of a set piece).
5. **Manual override.**
   - V or VIEW picks the other view and pins it for the current context.
   - The pin clears when the context changes (Explore to Combat or back), so automatic switching resumes.
   - Platforming and Explore count as the same context for this rule, so pressing V while exploring is not undone at the next obstacle.
6. **Manual mode.** The pause menu has `CAMERA · AUTO / MANUAL` (`wz.cam.auto`, default Auto). Manual is exactly the M1 behaviour: V only, remembered in `wz.view.<stage>`.
7. **Scope.** The arena and Greenfang stay manual (no Stage 1 gameplay changes). The context camera runs in the jungle only.
8. **Traversal in either view.** Every move works in both views. In the high view the x-ray silhouette shows him behind the ruin wall.

---

## 4. Touch layout (landscape; canvas units match the existing builder)

| Zone | Control |
| --- | --- |
| Left half | Floating stick (as now). In the high view it may start anywhere outside the buttons (as now). |
| Right half, empty area | Drag to look (behind view, also while hanging). The high view keeps auto-aim and auto-fire. |
| Bottom-right corner | **JUMP**, big (1.35x). Context label: JUMP / VAULT / CLIMB / UP. Tap acts; a buffered press counts within 0.15 s. |
| Left of JUMP | **FIRE**, big (behind view only, as now); a drag that starts on FIRE also looks. |
| Above JUMP | **DROP**, small; shown only while hanging or climbing. Holding the stick down for 0.3 s also drops. |
| Arc above FIRE and JUMP | **DASH** and **BOMB**, small, existing styling (dimmed while cooling down). |
| Top-right | **VIEW** (existing) with an "AUTO" badge while the context camera is in charge; tapping pins (rule 5). SKIP is unchanged. |
| Set pieces | The existing ACTION button (BOARD, JUMP, DEPLOY, FLARE) is unchanged and never shown together with the traversal JUMP. |

- **Portrait:** buttons at 0.85x in a vertical column on the right; the stick's half becomes the lower-left 60%.
- **Safe area:** respected through `Screen.safeArea`.
- **Assists (pause menu, default on for touch):** auto-vault of obstacles below 1.0 m when running into them; look sensitivity slider; aim friction in the behind view.

---

## 5. Risks

| Risk | Mitigation |
| --- | --- |
| Tripo clips don't fit (no vault or hang presets; `jump` and `climb` already rejected once) | Procedural `TraversalPose` is planned as the primary path for vault and hang, not as an afterthought. Clips are judged on a contact sheet before wiring. |
| Feel in a custom controller (jitter at block edges, grabbing the wrong edge) | EditMode tests for each rule; greybox tuning (iteration 4) before art; probe uses angle and distance scoring. |
| Soft-locks (stuck in a gap, pinned against the wall) | The course bot runs in CI in both views; no enclosed pits; `PushOutOfBlocks` always resolves; fall damage is never lethal. |
| Touch: three thumbs' worth of actions | One context JUMP button, DROP only when needed, auto-vault assist; touch prototype in iteration 5 plus a real-phone check, not at the end. |
| Camera ping-pong or cuts mid-climb | Hysteresis, dwell, no switch mid-move, blends; tests for each. |
| High-view refactor (now a Cinemachine camera) breaks the arena or Greenfang | Existing `CameraTests`, `ArenaPlayTests` and `GreenfangTests` must stay green; arena framing compared in stills. |
| First load: new clips live in the Warden controller in the main build | Measure in iteration 6. If first load passes ~21.5 MB, move traversal clips into a jungle-only `AnimatorOverrideController` loaded with the jungle's Addressables. |
| iOS memory (286 MB heap already) | Props at 512 px on Low, 1024 only for the wall on High; measure the heap after iterations 8 and 12; real iPhone runs in iterations 5, 10 and 12. |
| Credits: the plan needs all 215 | Top-up asked before props; documented code fallback for P4. |
| Course is optional in open jungle (he can walk around beats) | Accepted for M2. Hard corridor bounds and mandatory gates come with the full corridor in M4. |

---

## 6. Out of scope

- **Vehicles** (drivable, on-rails turret) and their cameras and touch controls: **M3**.
- **Stage 2 content:** the full corridor, checkpoints B onward and their saves, 3D soldiers and creatures, enemy AI that uses or respects blocks, strikes and the Warlord in Stage 2, extraction: **M4**. M2's combat context is driven only by the debug skirmish (arena grunts) and the arena.
- **Realism and performance pass:** weather, final audio, quality tuning on many phones, final size pass: **M5**.
- Stage 1 gameplay changes (the arena keeps the manual camera, and Space keeps dashing).
- New save fields or checkpoints. Saves stay at checkpoint A.
- Swimming, sliding, wall-running, rope swings, ladders other than vine walls, sprint.
- A Mixamo re-rig of the Warden or finger bones.
- Hard corridor bounds.

## 7. Open questions for the user

1. Top-up of ~50-100 Tripo credits before iteration 8?
2. Should at least one beat (the ruin wall) be mandatory in M2, with side blocks, or does that wait for M4's corridor?
3. Auto-vault on desktop too, or touch only?

---

## 8. Review changes and user decisions (binding; they override sections 0-7)

User decisions (2026-10-11):
- **Tripo credits topped up to ~2,200.** The 215-credit budget and the P4 code fallback no longer apply. Every prop comes from Tripo, re-rolls are allowed, and quality wins over cost. Still log every spend.
- **Auto-vault: touch only** (default on for touch, toggle in the pause menu; desktop uses Space).
- **Course (PM call):** the ruin wall becomes the one **mandatory beat**. It is the milestone's showcase, and a play-tester who walks around it never sees the climb. Build it as a gate across the trail: the wall (about 4.5 m, see 11 below) flanked by Tripo-made impassable set dressing (collapsed ruin rubble and a steep rock outcrop or ravine edge), so the only way on is the vine climb, the top walk, the shimmy and the drop. All other beats stay optional but sit on the trail where the camera sees them. Full corridor bounds remain M4. The course bot must use the wall, and a test proves the way round is blocked.
- **Extra art the budget now allows:** a second crate variant, the gate's rubble and outcrop pieces, and up to two more animation retarget batches when a clip test needs them. No new features.

Reviewer changes (all required):
1. Reset `Blocks` and `TraversalEnabled` in `World.Use` (Greenfang calls `World.Use` directly); `JungleStage.Awake` sets them after its `World.Use`.
2. The drop set piece must respect blocks: one surface function on `JungleStage` (terrain or a block top) used by the Skydive ground, the landing slide, `BeginWalk`, `StandAt` and `Unclip`; `PushOutOfBlocks` during the landing slide; `Traversal.Reset(Grounded, position)` in `BeginWalk`, `StandAt` and `PlayerController.ResetForRun`. Tests: land on a block top, land against a block side.
3. Let go is **C** only (no Ctrl: Ctrl+W closes the tab).
4. Rules for non-grounded states: dash only while grounded; knock is ignored while attached; death while attached drops him first, then plays Death. One EditMode test each.
5. No tunnelling: sub-step movement at most ~0.3 m per step. Test: a dash at the log with dt = 0.1 does not pass through.
6. Jump distance and air speed use a fixed traversal speed in `GameConfig`, not `Stats.Speed`. The course bot passes at base speed and at max upgrades.
7. The course is data in code (`CourseLayout`, one static list). The builder places visuals from it and `JungleStage` registers it. The no-overlap test and the course bot run in EditMode by driving `Traversal` directly; keep one short PlayMode smoke run per view. "No enclosed pits" test: from sample points around each beat the bot can reach the trail.
8. Camera:
   - Platforming framing moves into iteration 3.
   - Iteration 9 must define: how `highCam` and the shoulder cam stay inactive in set pieces; who owns the blend (per-switch blends or a `CustomBlends` asset, not `JungleStage.Show` overwriting `DefaultBlend`); what replaces `brain.enabled = false` in `BeginWalk`/`StandAt`.
   - Automatic switches never write `wz.view.<stage>`.
   - Manual mode keeps the M1 cut on V. Automatic switches blend.
   - "Camera never ends inside a block" is an iteration 1 test.
9. Drop `?campaign=skirmish`. Test `CameraContext` in plain C# with a `CombatZone` and fake enemy positions. Record "enemies and high-view bolts ignore block heights" as an M4 known gap.
10. The bolts-on-blocks test covers aimed (behind view) bolts only.
11. The ruin wall is about 4.5 m, clear of the 5 m drop-damage threshold.
12. While hanging, climbing or vaulting, `Traversal` owns facing (wall normal or move direction) through a `WardenPose` field; mouse look turns only the camera.

Adopted suggestions: reuse the touch `actionButton` as the traversal JUMP (one consume path); no stick-hold drop (DROP button only); aim friction deferred to M4; reuse `AirSpeed` and `WardenModelView.Play`/`Action` where they fit; hiding the rifle is an acceptable first pass for the sling; measure clip memory before any override-controller work; `tools/tripo/` goes to main in its own small commit.
