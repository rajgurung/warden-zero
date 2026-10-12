# Milestone 3: Vehicles

Spec: `SPEC.md` (Milestone 3, Key Flow 3, Risks). Base: `main` after the M2 merge. Branch: `m3-vehicles`. Art: a separate art agent in its own worktree (`../combat_game-m3-art`, branch `m3-art`), the same way as M2. Each iteration ends with zero warnings, green EditMode and PlayMode suites (now 94 / 126 + 1 skip), a WebGL build, inspected stills (`Snapshots.Capture`) and a commit. The PR gets a Vercel preview, a reviewer agent and a user play-test before merging.

## Status (2026-10-12)

In progress on `m3-vehicles` (engineer: iterations 0-10, then 13-14 on greybox; art in `m3-art`).

| # | Done | Notes |
| --- | --- | --- |
| 0 | Heap probe | `?mem`, `?memprobe=N` (`MemoryProbe`, `WZ_HeapMB`). Measured below. |
| 1 | Road data | `RoadPath`, `RoadLayout`, `JungleLayout.Height` / `Landscape` / `NaturalRaw`. Route changes below. |
| 2 | Terrain tile, paint, trees, bounds | `SceneBuilder.Road.cs`; `World.Bounds` / `OutOfBounds`, the trunk grid, the terrain picked by rectangle (review 1). Heap held at 286 MB at Low by resolutions (below). |
| 3-15 | | |

Baseline (from M2): first load 22.39 MB; jungle bundle 40.54 MB; jungle wasm heap 286 MB at Low and 286 or 344 MB at High; 60 fps (vsync-capped).

Heap headroom (iteration 0, Chrome M1 Pro, `?campaign=jungle&memprobe=N`, N MB allocated 1 MB at a time 3 s after landing):

| Tier | Heap after the jungle load | Largest N with no growth | First N that grew |
| --- | --- | --- | --- |
| Low (iPhone 13 emulation) | 286 MB | 45 MB (44 never grew; 46 and 47 grew in some runs) | 46-48 MB (to 343 MB) |
| High (1280 x 720) | 286 MB (3 runs) or 343 MB (5 runs) | at 343 MB: 44 MB or more | not reached |

Route as built (iteration 1; review item 4 and the scaled jeep):
- The motor pool (D0) moved to (-8, 50), 35 m north-west of A, so the climb to the rim is long enough for a 9.5% grade: east along z = 50, a 28 m-radius bend north, and over the rim at x = 56 (D2). The road is at least 17.9 m from A, so its carve (10 m) stays clear of A's ring.
- D3 is (70, 128), 48 m from the tile's free edges. Between the rim and D3 the road passes within 30 m of the landscape corner at (80, 80); all three terrains are flat at the rim there.
- The centreline is a centripetal Catmull-Rom smoothed over 8 m (the spline bunches its curvature at its points). Minimum radius: drive 22 m, ride 28 m. Lengths: drive 569 m, ride 336 m (to the roadblock).
- The landscape is lowered into the road's valley around the tile (`JungleLayout.Landscape`: never above the valley walls, which rise at 24 degrees from the banks). The natural hills are 100-120 m high around B and the ride, and the road can't climb there at 10%, so without it the tile's edges would be 60-80 m cliffs. The tile blends into the landscape's own 16 m grid over its last 32 m.

So at Low about 45 MB is free before the step to 343 MB once the jungle is in. That is not the limit, though: the jungle's load peak is (iteration 2).

Iteration 2 (road tile), Chrome M1 Pro:
- The first tile build (heightmap 513, alphamap 512, ground cover 256, landscape alphamap 512) added only 1.11 MB to the jungle bundle, but **stepped the Low heap to 343 MB** (2 of 2 runs). The extra comes in during the load, not after it.
- Shipped: tile heightmap 257 (1.5 m), alphamap 256, ground cover 128 (3 m cells, same density), 2.4k fewer far cards, and the landscape's alphamap 512 to 256 (decision 0.5's first offset). The tile's far ground uses the landscape's canopy layer (no new textures; it reads as the same forest from the air), so the coarser paint costs little.
- Result: jungle bundle 40.50 MB (M2: 40.54); first load 22.40 MB; **wasm heap 286 MB at Low (3 of 3 runs) and 286 MB at High (3 of 3)**. After the load at Low 44 MB more still allocates without growth.
- Decision 0.5's fold-back: **not taken.** The post-load headroom is large, but the load peak sits just under the step, so `Vehicles` stays a separate group loaded after the jungle (iteration 12).
- The tile's tree distance follows the patch (400 m High, 120 m Low); at 600 m it slowed the PlayMode suite by half.

**What M3 adds.**
- After checkpoint A the campaign goes on. The Warden takes an open-top military jeep down a jungle dirt road to a new checkpoint B, which saves.
- At B a squadmate takes the wheel. The Warden mans the jeep's rear heavy machine gun for an on-rails ride past destructible targets to a roadblock: "MILESTONE 3 COMPLETE".
- A context prompt to get in and out (the M2 context button on touch), a chase camera and a gun camera, and touch layouts for driving and for the gun.
- Continue resumes at A or at B, on foot or in the jeep.

---

## 0. Decisions

### 0.1 Handling: a custom arcade model on `World`, no PhysX

`Vehicle` is a plain C# class, like `Traversal`. It samples the ground at four wheel points through `World.HeightAt` / `World.SurfaceAt`, and collides with the same trunks, rocks and blocks as the Warden and the camera.

Reasons:
1. **Size and heap.** PhysX is stripped today (`stripEngineCode: 1`; no script uses `Physics`; the builders delete every collider, including `TerrainCollider`).
   - Bringing it back for WheelColliders is estimated, not measured, at +1.5 to 2.5 MB on the first load. That would take 22.39 MB to about 24-25 MB, at the hard cap.
   - The heap cost is estimated at +10 to 25 MB: the PhysX scene, cooked heightfields for three terrains, and colliders for thousands of trees. That is almost certainly one growth step (286 to 344 MB at Low), which the budget forbids.
2. **One world model.** The camera, aim, bolts and the Warden already use `World` and `WorldCast`. Colliders would be a second world that has to agree with the first (M2 decision 0.1).
3. **Testable and deterministic.** The handling, the bot drive and the stuck/reset rules run in EditMode with no scene.
4. **The spec asks for arcade handling**, not a simulation.

Cost: no real rollovers or vehicle stacking. Neither is wanted. Tilt is clamped, and "flip recovery" becomes the reset rule (section 3.6).

### 0.2 Turret ride: the jeep's rear gun with a squadmate driving, not a river boat

| | Jeep rear gun (chosen) | River patrol boat |
| --- | --- | --- |
| Terrain | Reuses the road tile; the ride is the road past B (about 330 m) | A new river corridor: wide carved channel, banks, a second stretch of terrain |
| Rendering on phones | Opaque terrain and props | A large transparent water surface with reflections (the stream material) is costly overdraw at Low; plus wake and foam |
| Art | One vehicle serves both the drive and the ride; the gun parts are needed anyway | Hull, engine, gun mount and wake: 4 or more new models |
| Code reuse | Same ground-sampled pose (pitch, roll, wheels), seat code, cameras and road spline | New water-surface pose, buoyancy bob, and a separate spline system |
| Bundle and heap | About +0 MB of terrain beyond the road tile | About +1.5-2 MB more terrain, plus water textures |

The driver is a **squadmate**: a second instance of the Warden model, cloned at runtime from the live player model, so it adds no asset or bundle duplication. It is re-tinted (desaturated tan and olive, cyan off) and wears the M1 jump helmet so the face reads differently. It uses the same Animator, the `sit` clip and hand IK on the steering wheel. M4's soldiers can replace it later.

Fallback if the clone reads badly in the play-test: a "remote drive" fiction (the jeep drives itself, with the cyan autopilot light on). That is a code change only.

### 0.3 Targets: Tripo destructibles, not arena grunts

The arena grunts are 2D sprites, and the spec says no 2D sprites in Stage 2. M3 proves the gun on:
- steel pop-up target plates rising from behind cover;
- fuel drums that explode and chain;
- a fuel trailer for a big chain;
- crates (the M2 crates reused);
- targets on a sandbag nest and a watchtower.

A new `Destructible` registry is hit by bolts. Nothing shoots back in M3 (that is M4).

### 0.4 The road needs new terrain: one 384 m tile north-east of the patch

The walkable patch is 132 m square (`PlayHalfSize` 66) and checkpoint A is at (22, 32). A road inside it would be under 200 m (under 15 s of driving), would cross the stream and the M2 course, and would end at the valley rim. That is the "invisible wall" feel the brief rules out.

So a new terrain, `JungleRoad`:
- 384 x 384 m at x -48..336, z 80..464 (all multiples of the landscape's 16 m hole cells);
- heightmap 513 (0.75 m samples);
- the four existing terrain layers, so **no new ground textures**.

Costs and the layout are in section 4. The tile lives in the **jungle scene**, because it is visible from the air during the flight and jump and replaces landscape that is holed out.

### 0.5 Memory: measure first, and stream the vehicle art separately

M2 found that +2.19 MB of jungle bundle kept the Low heap at 286 MB, but +3.55 MB of textures stepped it to 344 MB. Growth is geometric: 0.2 per step, so 286 to about 344. The headroom left after M2 is unknown and probably small.

Rules for M3:
1. **Iteration 0 measures the headroom** with a debug allocation probe (`?memprobe=N`, section 1).
2. **Jungle bundle growth is capped at +1.5 MB.** That is the road tile and code-built meshes only, no new textures.
3. **Vehicle, target and outpost art goes in a new Addressables group, `Vehicles`.** It is loaded in the background once he lands (or at Continue), after the jungle scene load's peak and its temporary buffers are gone. Budget: 5 MB or less.
   - Shared shaders must not be duplicated into it. Check this with the Addressables "Check Duplicate Bundle Dependencies" analyse rule; if needed, move the URP Lit and `FoliageLit` shaders into a small `Shared` group.
   - If the probe shows 8 MB or more of headroom after the tile lands, fold `Vehicles` back into the jungle bundle (simpler) and record why.
4. **Offsets if the tile alone steps the heap:**
   - landscape alphamap 512 to 256 (about -0.75 MB);
   - landscape heightmap 513 to 257 (about -0.4 MB);
   - the tile's far zone with cards only.

### 0.6 Keys

**On foot (jungle):** unchanged from M2. Near the jeep's driver door, Space is the context move, labelled DRIVE. At B, near the jeep, it is GUN.

**Driving:**

| Input | Action |
| --- | --- |
| W / Up | Throttle |
| S / Down | Brake, then reverse once stopped |
| A / D | Steer |
| Space | Handbrake |
| C | Get out (only below 2.5 m/s, matching "C lets go") |
| R | Reset to the road (when offered, or any time below 1 m/s) |
| V | View |
| Mouse (pointer lock) | Orbits the chase camera, which recentres |

E (bomb) and Shift (dash) do nothing while seated.

**Gun:** the mouse aims (pointer lock), the left button fires, the right button zooms, V switches close and wide. No getting out during the ride.

### 0.7 One jeep, and parking spots

There is exactly one jeep.
- It is parked at A's motor pool from the start of the jungle.
- If he walks to B and leaves the jeep behind, securing B brings it to B's gate inside the fade that starts the ride.
- Each checkpoint has a `ParkingSpot` (position and heading), so M4 extends this.
- Saves gain `InVehicle`. Continue puts the jeep at that checkpoint's parking spot, with him in the driver's seat if `InVehicle`, else on foot beside it.

### 0.8 The milestone end moves

- Securing A no longer ends the slice. It saves and hands over to the new `RoadStage`.
- The ride's end calls `MilestoneComplete(3)`. The banner and result eyebrow text become parameters (they say "MILESTONE 1 COMPLETE" today).
- Continue after finishing resumes at B, which replays the ride.

---

## 1. Iterations

**Greybox first.** Iterations 0-10 need no art. Art lands in 12-13. The art agent starts at iteration 1, in parallel (section 2). The jeep body is the long pole (multiview plus possible re-rolls), so it goes first.

### 0. Heap probe and baseline
- **Files:**
  - `Plugins/WebGL/TouchDetect.jslib`: add `WZ_HeapBytes()` (returns `HEAPU8.length`).
  - New `Scripts/MemoryProbe.cs`:
    - debug URL `?mem` shows the heap size in the corner;
    - `?memprobe=N` allocates N MB of native memory (`NativeArray<byte>`, persistent) 3 s after the jungle is playing, and logs whether the heap grew.
- **Tests:** EditMode, URL parsing for `mem` and `memprobe`.
- **Acceptance:** search N in 1 MB steps at Low and High in Chrome. Record the headroom in this file's status. That number sets decision 0.5.

### 1. Road data: spline, route and ground
- **Files:**
  - New `Scripts/RoadPath.cs` (plain C#):
    - centripetal Catmull-Rom through control points;
    - an arc-length table (1 m samples);
    - `Sample(s)` returns position, tangent and curvature;
    - `Nearest(p)` returns s, signed lateral distance and the segment.
  - New `Scripts/RoadLayout.cs`: the tile rectangle; `Drive` and `Ride` paths; `CorridorHalf`; checkpoint B; parking spots at A and B; B's gate; the ride's speed profile and target slots; and `Carve(x, z, h)`, the road's height term (section 4).
  - `Scripts/JungleLayout.cs`:
    - `Height` applies `RoadLayout.Carve` inside the patch and the tile;
    - adds the tile's height function: valley and banks, blended to the landscape's own 16 m interpolation at the tile's edges so there is no seam.
- **Tests (EditMode `RoadTests`):**
  - arc length is monotonic, and `Nearest` agrees with brute force;
  - the minimum curve radius is at least 22 m on the drive and at least 28 m on the ride;
  - the grade is at most 10%, and the cross-slope on the road surface is at most 4%;
  - non-adjacent legs are at least 2 x `CorridorHalf` apart, so there are no shortcuts;
  - roads stay at least 40 m from the tile's free edges;
  - `JungleLayout.Height` is unchanged, bit for bit, at every `CourseLayout` block sample, the LZ ring, A's capture ring and the spawn offsets;
  - no course block is within 8 m of the road.
- **Acceptance:** the suites pass; the M2 `CourseTests` are untouched and green.

### 2. Terrain tile, road paint, trees and bounds
- **Files:**
  - New `Editor/SceneBuilder.Road.cs` (a partial, so `SceneBuilder.Jungle.cs` doesn't grow past 1,100 lines):
    - builds `JungleRoad`;
    - splat: Mud on the road (half-width 3 m, ruts as two darker bands), LeafyGrass shoulders, ForestFloor, MossyRock on banks steeper than 28 degrees;
    - corridor trees: mesh trees with trunks only within 25 m of a road, denser 12-18 m out, so trunks are the soft wall; far-tree cards beyond 25 m; ground cover kept off the road;
    - roadside rocks reuse `JungleFlora` boulders and the M2 `rock_outcrop` / `gate_rubble` at bends.
  - `Editor/SceneBuilder.Jungle.cs`:
    - the landscape hole covers the tile, and no far trees are planted on it;
    - patch trees, shrubs, details and rocks are cleared off the road from the motor pool to the patch edge. Decide this after the random draws, like `CourseLayout.Covers`, so the forest doesn't reshuffle.
  - `Scripts/World.cs`:
    - `World.Bounds`, a clamp function that replaces the centred `HalfW` / `HalfD` rectangle in the jungle. Rule: inside the patch square, or within `CorridorHalf` + 2 m of a road;
    - `World.OutOfBounds(p)`, used for bolts, with a looser rule (the patch plus the tile rectangle);
    - a uniform trunk grid with 16 m cells (`World.TrunksNear`), because trunks grow about 3-4x and `WorldCast`, `PushOutOfTrunks` and every bolt scan them all.
  - `Scripts/GameConfig.cs`: `ResolveCircle` and `PointInWall` use `World.Bounds` / `OutOfBounds`. The arena keeps its rectangle.
  - `Scripts/WorldCast.cs`: uses the grid.
  - `Scripts/JungleQuality.cs`: tile tree and detail distances, same as the patch; far cards like the landscape.
- **Tests:**
  - EditMode `WorldTests`: bounds clamp to the patch, the corridor and their union; trunk grid results equal brute force; the arena is unchanged.
  - EditMode `RoadTests`: an on-foot `Traversal` bot walks the whole drive road from A to B. Proves that abandoning the jeep never soft-locks him.
  - PlayMode: a bolt fired up the road is not killed at x or z = 66.
- **Acceptance:**
  - stills from the motor pool, a bend, B, and from the air during the jump: no seam at the tile edges or the patch edge;
  - jungle bundle growth at most 1.5 MB;
  - heap at Low measured with the probe, and the decision 0.5 gate written down.

### 3. Handling model (plain C#)
- **Files:**
  - New `Scripts/Vehicle.cs`: `VehicleInput` (Throttle -1..1, Steer -1..1, Handbrake), and `Vehicle.Tick(input, dt)`. The model is in section 3.
  - `Scripts/GameConfig.cs`: a `Jeep` constants block.
- **Tests (EditMode `VehicleTests`):**
  - 0-60 km/h in 3.6-4.4 s, and top speed 22 m/s on the road;
  - off-road speed caps;
  - braking from 20 m/s stops within 15-19 m; S reverses only after a stop;
  - turning radius about 4 m at walking pace, and the lateral-acceleration cap at speed;
  - handbrake slides the rear;
  - cannot climb 27 degrees or more; slides down side slopes over 22 degrees;
  - bounces off a trunk with speed loss and a yaw nudge;
  - pushed out of a block;
  - no tunnelling through a trunk or block at dt = 0.1;
  - leaves the ground over a crest at speed and lands;
  - tilt never exceeds the clamp;
  - corridor clamp holds;
  - stuck detection (each rule in 3.6) and reset placement;
  - same inputs give the same result.
- **Acceptance:** the suite passes. The numbers are in `GameConfig` for tuning.

### 4. The drive bot (EditMode)
- **Files:** `Tests/EditMode/DriveBotTests.cs`: a pure-pursuit driver (look-ahead 6 + 0.5 x v m; brakes for curvature ahead) on `RoadLayout.Drive`.
- **Tests:**
  - motor pool to B in under 55 s with no reset and never more than 6 m off the centreline;
  - three noisy drivers (seeded random steer added) for 120 s each never leave the clamp, and every stuck state resets within its timeout;
  - a reverse-only bot can't escape the corridor.
- **Acceptance:** green. The times feed the tuning notes.

### 5. Greybox jeep, get in and out, and the road stage
- **Files:**
  - New `Scripts/VehicleRig.cs` (MonoBehaviour, visuals only):
    - a pivot hierarchy with the final names (`Body`, `Wheel_FL/FR/RL/RR`, `Steering`, `GunYoke`, `Gun`, `Muzzle`, seats `Driver` / `Gunner`, door points `DoorL/R`);
    - wheels spin by distance over radius, front wheels steer, the steering wheel turns 270 degrees at full lock;
    - the body follows a spring on pitch and roll, so it bounces and squats.
    - Greybox: Lit boxes and cylinders, built by `SceneBuilder.Road.cs`.
  - New `Scripts/VehicleController.cs`:
    - reads input (keys and touch) and ticks `Vehicle` (sub-stepped);
    - places the rig;
    - keeps the jeep's block in `World.Blocks`: a reserved slot in the jungle's own array, never `CourseLayout.Blocks`, with a new `BlockFlags.Vehicle` that `WorldCast` can skip for the drive camera;
    - enter (0.6 s move to the seat, rifle slung via `WardenModelView.Sling`), exit (to the door side; if blocked, the other side, then the rear), and reset (0.3 s fade, then a cut).
  - `Scripts/PlayerController.cs`:
    - a `Seat` state: no movement, fire, dash or bomb while seated;
    - `WardenPose.Seated`;
    - the vehicle prompt outranks the traversal label within 2.5 m of a door.
  - New `Scripts/RoadStage.cs`: the phases after A. TakeJeep, Drive, HoldB, Swap, Ride, Done.
    - Objectives: TAKE THE JEEP, DRIVE TO CHECKPOINT B (`PointAt` B), HOLD CHECKPOINT B.
    - B's capture ring counts on foot or in a stopped jeep.
  - `Scripts/JungleStage.cs`: `SecureCheckpoint` and `StandAt(secured)` hand over to `RoadStage` instead of `MilestoneComplete`.
  - `Scripts/Hud.cs`: desktop chips `SPACE · DRIVE`, `C · EXIT`, `R · RESET`, `SPACE · GUN`.
- **Tests:**
  - PlayMode `VehiclePlayTests`:
    - Space at the door seats him (rifle slung, ground marks off);
    - C at speed does nothing, C stopped gets him out on the free side;
    - R resets onto the road;
    - the bot drives the last 120 m into B (via `?campaign=drive` placed near B) and B saves.
  - Updated `CampaignPlayTests`: securing A no longer completes the milestone.
- **Acceptance:** the whole greybox route drives well with a keyboard. **The user gets a quick look**, and handling is tuned now, before any art goes into the scene.

### 6. Drive camera and the context camera
- **Files:**
  - New `Scripts/DriveCamera.cs`: pure pose maths for the chase rig (section 5); EditMode-testable like `CameraFollow.BehindPose`.
  - `Scripts/CameraFollow.cs`:
    - the seat-aware target;
    - `driveCam` joins `shoulderCam` and `highCam` as "one gameplay vcam active at a time";
    - the high view follows the jeep with vehicle framing;
    - mouse and touch look orbit the chase camera, and it recentres.
  - `Scripts/CameraContext.cs`: a `Driving` context. `Inputs.Seat` takes None, Driver or Gunner.
  - `Editor/SceneBuilder.Road.cs`: `driveCam` and the `GameplayBlends` entries.
  - `Scripts/Menus.cs`: nothing new. CAMERA · AUTO / MANUAL applies.
- **Tests:**
  - EditMode `DriveCameraTests`: lag bounded at 25 degrees; distance and FOV grow with speed; recentres after 1.2 s; stays 0.8 m or more above the ground; the collision cast skips the jeep's own block.
  - EditMode `CameraContextTests`: entering clears a pin; no automatic switch while driving; exit returns to Explore.
  - PlayMode `ContextCameraTests`:
    - entering blends to `driveCam`;
    - V pins High until exit;
    - exit blends to behind;
    - Manual V cuts and remembers `wz.view.drive`;
    - set pieces untouched.
- **Acceptance:** a drive with smooth blends, no camera inside trunks at bends, and the jeep's side visible in turns.

### 7. Touch driving
- **Files:**
  - `Scripts/TouchLayout.cs`: `Compute(canvas, insets, Scheme)` with `Scheme { OnFoot, Drive, Gun }` (section 6).
  - `Scripts/TouchControls.cs`:
    - the scheme;
    - a per-button pressed and held table (GAS, BRAKE and handbrake are held), and relabelled buttons;
    - in Drive, the stick's x is steering.
  - `Scripts/VehicleController.cs`: touch input, plus DRIVE ASSIST (default on for touch; section 3.5).
  - `Scripts/Menus.cs`: `DRIVE ASSIST · ON/OFF` (`wz.assist.drive`).
- **Tests:**
  - EditMode `TouchLayoutTests`: each scheme at 16:9, 19.5:9 and 9:19.5 keeps every rect on screen with no overlaps.
  - PlayMode (`ForceTouch`):
    - GAS plus the stick drives and turns;
    - EXIT shows only below 2.5 m/s;
    - RESET shows only when offered;
    - a touch starting on GAS never moves the stick;
    - look-drag orbits.
- **Acceptance:** the drive from A to B with touch only in Chrome iPhone emulation; **first real-phone check** of the drive.

### 8. Saves, Continue and debug starts
- **Files:**
  - `Scripts/CampaignSave.cs`: place `CheckpointB = "checkpointB"`; `bool InVehicle`. Version stays 1: the field is additive and defaults to false.
  - `Scripts/Campaign.cs`:
    - `DropStart.Drive`, `CheckpointB` and `Ride`;
    - `Continue` maps the places;
    - `DropFor` gains `drive` (seated in the jeep at A, A secured), `checkpointb` and `ride`;
    - `checkpoint` now starts the road phase on foot.
  - `Scripts/JungleStage.cs` and `RoadStage.cs`: the starts, and parking-spot placement (decision 0.7).
- **Tests:**
  - EditMode `CampaignTests`: round-trip with B and `InVehicle`; a v1 JSON without the field loads; unknown places are still ignored; `DropFor` values.
  - PlayMode:
    - Continue at A: on foot, jeep parked, TAKE THE JEEP;
    - Continue at B in the vehicle: seated at B, GUN offered;
    - Continue at B on foot: beside the jeep;
    - health, level and upgrades restored.
- **Acceptance:** close the tab mid-drive and after B; Continue lands sensibly both times.

### 9. Gun, ride and targets (greybox)
- **Files:**
  - New `Scripts/Turret.cs` (plain C#):
    - yaw relative to the hull (full 360), pitch -8 to +35 degrees;
    - a no-fire cone of ±25 degrees ahead below +4 degrees pitch, so he never shoots the cab;
    - slew limits;
    - heat: +0.1 a round, cools 0.35 a second, overheats at 1 and locks until 0.35.
  - New `Scripts/TurretRide.cs` (plain C#): progress along `RoadLayout.Ride` with its speed profile and pauses. It reuses `Vehicle`'s four-point ground pose for pitch, roll and wheel spin, and steers from path curvature.
  - New `Scripts/Destructible.cs`:
    - `All` registry; kinds Drum, Plate, Crate, Trailer;
    - HP; chain explosions (`Effects.BombBlast`); XP and score;
    - plates pop up from a ride trigger.
  - `Scripts/Bolt.cs`:
    - also hits `Destructible.All`;
    - per-prefab life, so turret rounds reach about 140 m;
    - bounds via `World.OutOfBounds`.
  - `Scripts/WorldCast.cs`: the enemies cast also includes destructibles (the crosshair point).
  - `RoadStage.cs`:
    - Swap: fade, then the jeep at B's gate, the squadmate (a greybox capsule for now) in the driver's seat, the Warden at the gun;
    - Ride: CLEAR THE ROAD, `TARGETS n/m`;
    - Done: `MilestoneComplete(3)`.
  - `Scripts/GameManager.cs` and `Menus.cs`: the milestone number as a parameter.
  - `Editor/SceneBuilder.Road.cs`: B's boom gate, closed until the ride (a block), and greybox targets from `RoadLayout` slots.
- **Tests:**
  - EditMode `TurretTests`: limits, the no-fire cone, slew, the heat cycle.
  - EditMode `RideTests`: the ride follows the path within 0.05 m; its duration is 45-60 s; pauses happen; pop-ups trigger at their s.
  - EditMode `DestructibleTests`: drum chain radius; trailer chain; a plate takes one round.
  - PlayMode `RidePlayTests`: GUN at B starts the ride; holding fire at a drum destroys it; the ride ends in "MILESTONE 3 COMPLETE"; nothing shoots the squadmate's seat.
- **Acceptance:** the greybox ride is fun with the mouse; targets read at distance; no frame drop during chains.

### 10. Gun camera and touch gun
- **Files:**
  - `Scripts/DriveCamera.cs`: gun Close and Wide poses.
  - `CameraFollow.cs`: `gunCam`; V switches Close and Wide (`wz.view.gun`); zoom on the right mouse button or ZOOM.
  - `TouchLayout` and `TouchControls`: the Gun scheme.
  - `VehicleController` (or `RoadStage`): touch aim assist (section 6), default on.
  - `Hud` / `MissionHud`: heat bar, a barrel marker when the gun lags the crosshair, the target counter.
- **Tests:**
  - EditMode: gun pose follows the aim; aim assist slows look near a target and never pulls through cover.
  - PlayMode (`ForceTouch`): a drag anywhere aims; FIRE held fires; the VIEW badge is hidden in the gun.
- **Acceptance:** the ride is finished on touch in iPhone emulation, then on a real phone.

### 11. Tripo art (art agent, in parallel from iteration 1)
- Section 2. Deliveries go into the art worktree: `Assets/Art/Campaign/Vehicles/*.fbx` (FBX only; metas are made in the main checkout), `ArtSource/M3Vehicles/` (README, requests, tasks, contact sheets, measured sizes and pivots) and the M3 section of `Assets/Art/Campaign/TRIPO_LOG.md`.
- **Acceptance:** contact sheets reviewed; each model accepted or rejected with a reason, like M2.

### 12. Art import and fit
- **Files:**
  - `Editor/CampaignImport.cs`: a `Vehicles` set (real size, textures extracted, Medium mesh compression).
  - `Editor/ArtImportSettings.cs`: texture caps from section 2.4.
  - `Editor/SceneBuilder.Road.cs`:
    - prefabs `Jeep`, `Targets/*` and `Outpost/*` built from the art;
    - the body's nose found by PCA, like the chopper;
    - wheels at the measured arch centres; the steering wheel on the column; the yoke on the pedestal; gun trunnions on the yoke;
    - blocks fitted to the outpost props.
  - `Editor/AddressablesSetup.cs`: the `Vehicles` group (decision 0.5).
  - `VehicleRig` / `RoadStage`: instantiate the loaded prefabs under the greybox pivots, and wait for the load if needed (same-site and cached, so short).
- **Tests:**
  - `ContentTests`: props imported; wheel radius within 3% of `Jeep.WheelRadius`; pivots inside the expected parts; outpost blocks within 0.25 m of the prop bounds.
  - Both bots still pass.
  - No duplicate bundle dependencies (the analyse rule, run from a test or a builder check).
- **Acceptance:**
  - stills next to the Warden pass the style check (user sign-off);
  - `Vehicles` bundle at most 5 MB;
  - jungle bundle growth from M2 at most 1.5 MB;
  - **heap at Low still 286 MB**, measured.

### 13. Warden and squadmate presentation
- **Files:**
  - `Editor/Warden3DImport.cs` and `SceneBuilder.Warden3D.cs`: the `sit` (and `greet_01`) clips; `Seated` and `Wave` base states.
  - New `Scripts/VehiclePose.cs` (after the Animator, like `TraversalPose`):
    - driver: hands on the wheel's grips through `WardenHandIK.LeftHold` / `RightHold`, turning with the wheel; feet on the pedals; head looking into the turn; body leaning against lateral acceleration;
    - gunner: standing, hands on the spade grips, body turning with the yoke, knees soft over bumps.
  - `Scripts/WardenModelView.cs`: `Seated`; `MakeSquadmate()` (clone, tint, helmet, no controller).
  - The enter and exit moves get a short step, not a slide.
- **Tests (PlayMode):**
  - hands within 0.1 m of the wheel grips at full lock, and of the gun grips at full pitch;
  - rifle slung while seated, and back in his hands within 0.5 s after exit;
  - the squadmate's hands track the wheel.
- **Acceptance:** side-cam stills (`&sidecam`) of sitting, driving, turning, gunning and the squadmate look right.

### 14. Feel, sound and effects
- **Files:**
  - `Scripts/SynthAudio.cs`: an engine loop (pitch from speed through three simulated gears, load from throttle), tyre skid, an HMG burst, a suspension clunk. No audio files.
  - `VehicleRig`: dust from the rear wheels scaled by speed (halved at Low); impact shake; a muzzle flash and light on the gun.
  - `CameraFollow`: shake on impacts and while firing.
  - Optional: one shallow creek ford on the drive (a short water strip with the stream material; slows to the mud cap; a splash).
- **Tests:** engine pitch rises with speed; impact strength maps to shake (EditMode).
- **Acceptance:** play-test notes addressed.

### 15. Ship
- Update `README.md` (controls, cameras, vehicles, debug URLs, measurements) and this file's status.
- Run `scripts/publish-web.sh` and commit `web/` (LFS).
- Measure:
  - first load: target 22.8 MB or less, hard cap 25;
  - jungle and `Vehicles` bundles;
  - wasm heap at Low and High in the jungle (Low must stay 286 MB);
  - fps at High 1080p while driving and firing, and with iPhone emulation at Low;
  - a real iPhone Safari run of A to the end of the ride.
- PR with the Vercel preview, reviewer agent, user play-test, merge.

---

## 2. Tripo assets

Workflow as in M2:
- `T="node --env-file=.env.local tools/tripo/tripo.mjs"`, then `$T balance` first. The key is never printed.
- Concept: image-to-image (`chat_image_2.5_sunburst`, medium, transparent) with `docs/warden-hero.png` as a **colour reference only** (olive, gunmetal, small cyan accents). Prompts ask for **realistic, photoreal PBR** to match the CC0 jungle and the M2 props.
- Model: image-to-model `v3.1-20260211`, texture + pbr, standard, `auto_size`. The jeep body uses image-to-multiview, then multiview-to-model, like the chopper.
- FBX converted from the task id.
- Before spending, check the docs for a part-segmentation option (`generate_parts` or similar) that works with texture on v3.1. If it exists, try it once on a whole jeep as a candidate. Otherwise use the separate-parts route below (proven with the M1 chopper and rotor).

Separate moving parts are made as **separate models**. The body is asked for without wheels, steering wheel or gun, the way the chopper was asked for without rotor blades.

Last balance: **1720**.

### 2.1 The jeep and its gun

| # | Asset | Prompt (concept) | Part split | face_limit | Credits |
| --- | --- | --- | --- | --- | --- |
| V1 | `jeep_body` | Photoreal three-quarter front view of an open-top military light utility vehicle, M151-style jeep, modernised: flat olive drab steel body, gunmetal bumpers and roll bar, small cyan LED marker lights. Fold-down windshield frame lowered flat on the hood, no glass. Two front bucket seats and a rear cargo bed with a central steel pedestal post (no gun, just the post and a flat top plate). **No wheels**: the body stands on four black jack stands, with empty wheel arches and visible hub plates. **No steering wheel**: a bare steering column. Jerry can on the back, mud on the lower panels, scratches. Neutral studio light, transparent background, whole vehicle in frame. | Static hull, seats and pedestal. Wheels, steering wheel, yoke and gun are separate. | 20000 | 10 concept + 10 multiview + 30 model + 5 convert = **55** |
| V2 | `jeep_wheel` | A single off-road military jeep wheel: chunky mud-terrain tyre, deep tread, olive steel rim with five lug nuts, dried mud. Three-quarter view showing the tread. | Used x4, mirrored on the left. The bbox centre is the hub; the thin axis is the spin axis. | 3000 | 45 |
| V3 | `jeep_steering` | Military three-spoke steering wheel, black rubber rim, steel spokes, olive centre cap, short column stub. | Turns about the column axis | 1500 | 45 |
| V4 | `gun_yoke` | Vehicle pedestal gun cradle: a steel U-shaped yoke on a round traverse ring with a small curved olive armour shield, gunmetal hardware, **no gun**, empty trunnion slots. | Yaws on the pedestal | 4000 | 45 |
| V5 | `gun_hmg` | Vehicle-mounted heavy machine gun on its own, side view: long ribbed barrel with flash hider, gunmetal box receiver, twin spade grips with a butterfly trigger at the rear, ammo can on the left feeding a belt, small cyan-lit reflex sight, trunnion pins on the sides. Worn metal. | Pitches about the trunnions; `Muzzle` at the flash hider | 6000 | 45 |

The art agent records, from the renders and from Unity measurements:
- the arch centres and the column-stub position in V1;
- the trunnion positions in V4 and V5;
- the yoke's ring centre;
- each model's front axis (M2: Tripo's +X front ends on Unity -X).

### 2.2 Targets (the ride)

| # | Asset | Prompt (concept) | face_limit | Credits |
| --- | --- | --- | --- | --- |
| T1 | `fuel_drum` | A 200-litre steel fuel drum, olive paint with a red flammable band and stencil, dented and rusty, closed top with bung caps. | 1500 | 45 |
| T2 | `target_plate` | A steel pop-up shooting target: a flat soldier-silhouette plate, olive with a white circular hit mark, small hinge bracket at the bottom edge, bullet dents. **The plate only** (it rises from behind cover). | 1200 | 45 |
| T3 | `sandbag_nest` | A U-shaped military sandbag emplacement, three courses of tan-olive bags, about 3 m wide and 1 m tall, a few torn bags, mud at the base. | 5000 | 45 |
| T4 | `watchtower` | A rough jungle watchtower: four lashed timber legs with cross bracing, a plank platform 5 m up with a waist-high plank wall, a corrugated tin roof, a ladder. | 6000 | 45 |
| T5 | `fuel_trailer` | A small two-wheel military fuel tank trailer, olive cylindrical tank with hazard markings, tow bar. | 5000 | 45 |
| T6 | `roadblock` | A jungle road barricade: heavy logs on X-shaped steel hedgehogs with coils of razor wire and a hand-painted warning sign. | 6000 | 45 |

### 2.3 Checkpoint B outpost

| # | Asset | Prompt (concept) | face_limit | Credits |
| --- | --- | --- | --- | --- |
| O1 | `hesco_wall` | A section of HESCO barrier: four wire-mesh cubes lined with tan geotextile, filled with earth, 1.4 m tall, 4 m long. | 3000 | 45 |
| O2 | `field_tent` | An olive canvas military field tent, A-frame, guy ropes and pegs, flap tied open. | 5000 | 45 |
| O3 | `boom_post` | Military checkpoint boom gate post: a concrete-weighted pivot housing with a counterweight, **no arm**. | 2500 | 45 |
| O4 | `boom_arm` | The striped red-and-white steel arm of a checkpoint boom gate, 5 m, with a pivot hub at one end. | 800 | 45 |

### 2.4 Clips, reuse and textures

**Clips.** One retarget batch on the Warden rig task `e73d30f3-…` with the M1 options: `sit` (seated hold for the driver, the gunner's rest pose and the squadmate) and `greet_01` (the squadmate waving at B). Cost: 2 x 10 + 5 convert = **25**. Judge them on a contact sheet. The fallback is a procedural seat pose in `VehiclePose`: hips to the seat, thighs forward, shins down.

**Reuse (0 credits):**
- the M1 beacon (checkpoint B) and jump helmet (squadmate);
- `supply_crate` / `supply_crate_b` (ride crates, outpost stores);
- `fallen_log` (cover for pop-ups, roadside logs);
- `rock_outcrop`, `rock_ledge`, `gate_rubble` (soft bounds at bends);
- the Warden model (squadmate).

**Texture caps (`ArtImportSettings`):**

| Model | Colour | Normals and metallic-smoothness | Emission |
| --- | --- | --- | --- |
| Jeep body | 1024 | 256 | 128 (markers) |
| Wheel, steering, yoke, gun | 512 | 128 | Sight only |
| Tower, tent, trailer, roadblock, nest | 512 | 128 | none |
| Drum, plate, hesco, boom parts | 256 | 128 | none |

Low halves all of them (as M2). Meshes use Medium compression. Estimated `Vehicles` bundle: about 4-5 MB.

### 2.5 Budget

| Spend | Credits |
| --- | --- |
| Jeep and gun (V1-V5) | 235 |
| Targets (T1-T6) | 270 |
| Outpost (O1-O4) | 180 |
| Clips | 25 |
| **Total** | **710** |
| Re-roll reserve (jeep body x2 about 100; gun parts x1, 45; two others, 90) | 235 |
| **Cap** | **945** |

That leaves at least about 775 credits for M4 (soldiers and creatures) even if every re-roll is used. Quality wins over cost within the cap. Log every spend.

**Order for the art agent:**
1. V1 (concept, then multiview: review before the model).
2. V2-V5.
3. Clips.
4. T1-T6.
5. O1-O4.

---

## 3. Handling model (`Vehicle`, plain C#)

### 3.1 State and geometry
- **State:** position (chassis origin on the ground), yaw, planar velocity (split each step into forward and lateral), yaw rate, vertical speed (airborne only), steer angle, `Airborne`, `Surface`, `Stuck`, `ResetOffered`.
- **Geometry:** wheelbase 2.2 m, track 1.4 m, wheel radius 0.38 m. Footprint for collision: three circles of radius 0.85 m at -1.1, 0 and +1.1 m along the jeep.
- **Ground:** four wheel points sample `World.SurfaceAt(p, chassisY)`.
  - Chassis height is the mean; pitch comes from front against rear, roll from left against right.
  - Each is clamped to ±28 degrees, so it can never flip.
  - The visual body adds a spring on top (`VehicleRig`).
- **Sub-steps:** at most 0.25 m of travel or 1/120 s each (as M2 review 5).

### 3.2 Along the jeep
- Throttle: `a = 6.0 x (1 - (v / vmax)²)` m/s², with vmax 22 m/s on the road.
- Brake: 12 m/s². Reverse after a stop: 3.5 m/s² up to 7 m/s.
- Coasting drag: 0.6 m/s² plus 0.012 v².
- **Slope:** subtract `g sin(pitch)`. With 6.0 m/s² of drive, slopes of about 27 degrees or more can't be climbed, so the banks are natural soft bounds.

### 3.3 Turning
- **Speed-sensitive steering:** the maximum wheel angle eases from 28 degrees (4 m/s and below) to 9 degrees at vmax. Keyboard steer slews at 3.5 per second; the touch stick is smoothed at 8 per second.
- **Bicycle yaw:** the target yaw rate is `v tan(δ) / L`. The actual rate approaches it with a 0.08 s time constant.
- **Grip limit:** `v x yaw rate` is capped at `μg`. Past it the jeep understeers and slides outward.
  - μ by surface: road 0.9, shoulder 0.75, off-road 0.6, mud or water 0.45.
- **Lateral slip:** decays as `exp(-grip x dt)`. Grip by surface: road 8, grass 5, mud 3.
- **Handbrake:** rear grip drops to 1.5 and the yaw rate gets 1.4x, for a controllable slide.
- **Side slopes over 22 degrees** add a downhill lateral slide.

### 3.4 Surfaces, contact and air
- **Surfaces:** from the road's lateral distance (`RoadPath.Nearest`), not the splat map, so tests are exact.

| Zone | Distance from the centreline | Speed cap |
| --- | --- | --- |
| Road | 3 m or less | 22 m/s |
| Shoulder | 3-6 m | 16 m/s |
| Forest floor | 6-14 m | 10 m/s |
| Corridor edge (dense trunks, rocks and banks) | 14 m and beyond | Hard clamp at `CorridorHalf` 18 m, hidden behind them |

- **Collisions:**
  - Each footprint circle is resolved against `PushOutOfTrunks` (grid), rocks (as ground circles) and `PushOutOfBlocks` (vehicle blocks skipped for itself).
  - The normal speed component bounces at -0.25x; the tangential component keeps 0.8x.
  - Yaw is nudged by the contact's lever arm.
  - The impact strength drives the shake, the clunk and the dust.
- **Air:** when the ground falls away faster than the chassis can follow at speed, it goes airborne under gravity, with no steering grip. Landing compresses the visual suspension. No damage in M3. The road profile keeps crests gentle (vertical curvature limits in `RoadTests`).

### 3.5 Drive assist (touch default on, pause menu)
- With the stick centred on the road, the jeep turns gently toward the road tangent, at no more than 30% of full steer.
- Throttle eases off when the curvature ahead needs less than 80% of the current speed.
- Off on desktop by default.

### 3.6 Stuck, flip and reset (no soft-locks)

| Rule | Offer RESET | Auto-reset |
| --- | --- | --- |
| Throttle or reverse held, speed under 0.6 m/s | After 2.5 s | No (he can get out) |
| Tilt over 24 degrees (pitch or roll clamp reached) | After 1 s | After 4 s |
| More than 14 m off the road | After 4 s | After 10 s |
| Desktop R while under 1 m/s | Any time | |

- **Reset:** a 0.3 s fade, then the jeep on the road centre at the nearest road point, facing toward B, speed 0. Then a camera cut.
- **On foot:** the road is always walkable to B (iteration 2 test), and the jeep always waits where he left it until B brings it to the gate.

---

## 4. Route layout

World XZ in metres. A is at (22, 32); the patch is x and z -80..80; the tile is x -48..336, z 80..464.

### 4.1 Drive: motor pool by A to checkpoint B (about 530 m, 40-45 s)

| Point | XZ | Note |
| --- | --- | --- |
| D0 | (31, 41) | Motor pool and A's parking spot, heading north-east; 12.7 m from A, clear of the capture ring, the terrace and the spawn offsets |
| D1 | (42, 56) | Climbs the valley rim, cut into it |
| D2 | (50, 80) | Crosses into the tile at the rim height (7 m) |
| D3 | (62, 112) | |
| D4 | (96, 140) | |
| D5 | (140, 150) | A long straight (top speed) |
| D6 | (200, 150) | |
| D7 | (240, 180) | A hairpin, with an M2 outcrop on the outside |
| D8 | (230, 226) | |
| D9 | (190, 240) | |
| D10 | (160, 270) | Optional creek ford (iteration 14) |
| D11 | (172, 310) | |
| D12 | (210, 330) | |
| B | (240, 352) | Outpost clearing: beacon, capture ring (radius 6), parking spot, hesco line and the boom gate across the ride road |

### 4.2 Ride: B to the roadblock (about 330 m, 45-55 s)

| Point | XZ | Beat | Speed |
| --- | --- | --- | --- |
| R0 | (240, 352) | Through B's gate | 5 |
| R1 | (262, 388) | Three drums and a pop-up behind a log (warm-up) | 8 |
| R2 | (240, 418) | Sandbag nest with two plates; M2 crate stack | 7 |
| R3 | (196, 424) | Watchtower: a plate on the platform, a drum pile at its foot (chain) | 7 |
| R4 | (150, 410) | Fuel dump: trailer plus six drums. The jeep slows to 4 m/s and pauses 2 s | 4 |
| R5 | (110, 420) | Two plates either side | 8 |
| R6 | (68, 410) | | 8 |
| R7 | (30, 418) | Roadblock in view: two nests, three plates | 6 |
| End | (4, 400) | Stops 12 m short of the roadblock. Ends when its plates are down, or after 8 s | 0 |

### 4.3 Road and corridor

**Road cross-section** (`RoadLayout.Carve`):
- The surface is level across, within 3 m of the centreline, at the road's own profile height.
- The profile rises from about 0 m at D0 to 7 m at D2. After that the grade is at most 8%; the tile's base is a valley that reaches about 25-30 m at B and stays near that height along the ride.
- The shoulder blends 3-6 m out. Banks rise 2-5 m between 8 and 16 m, steeper (over 27 degrees) at bends, to the tile's hills beyond.
- The road is carved only inside the patch (D0-D2) and the tile.

**Corridor:** dense trunks 12-18 m out, rocks at the outside of every bend, steep banks, and the hard clamp at 18 m (on foot, 20 m), never visible.

**Seams:** the tile's south edge on x -48..80 meets the patch's flat rim (7 m). Its other edges blend to the landscape's own 16 m interpolation over 32 m, so all three terrains meet without gaps.

### 4.4 What the terrain growth costs

| Item | Now | M3 (estimate; measured in iteration 2) |
| --- | --- | --- |
| Patch | 160 m, 257 heightmap, 857 KB asset | Same, with the road cleared and carved |
| Road tile | none | 384 m, 513 heightmap (0.75 m), alphamap 512 x 4 layers, detail 256, about 5-6k corridor trees and shrubs plus far cards. Asset 2.0-2.6 MB; LZ4 in the bundle 1.3-1.8 MB |
| Landscape | 8192 m, 513, 2.5 MB | Larger hole; about 1.5k fewer far trees |
| New ground textures | | None (the four existing layers) |
| Heap, CPU side | | About 2.2 MB (heights 0.5, alphamap 1.0, details 0.3, trees 0.4), plus the bundle bytes |
| Draw cost | | Visible trees at Low are about the patch's (a corridor at the same density, 120 m tree distance) |

**Rejected:** a loop inside the existing patch. It would be under 200 m, would cross the stream and the course, would leave no room for the ride, and would make the rim an invisible wall.

---

## 5. Camera rules

Contexts, resolved by `CameraContext` (M2 rules unchanged on foot):

| Context | When | View |
| --- | --- | --- |
| Set piece | Swap fade, reset fade, ride start and end shots | Own shots or cuts |
| Driving | Seated as the driver | Chase (`driveCam`); V gives High (vehicle framing) |
| Gunner | On the gun | Gun Close; V gives Gun Wide |
| Combat, Platforming, Explore | On foot | As M2 |

Rules:
1. **Enter and exit.** Entering the driver's seat or getting out is a context change: any pin clears, and the view blends 0.6 s EaseInOut (`GameplayBlends`) to the chase camera, or back to the behind view on exit.
2. **No automatic switch while seated.** The combat context is ignored while driving or gunning. M4 keeps the chase camera in a fight while driving.
3. **V while driving (Auto)** pins High until he gets out.
4. **Manual mode:** V cuts between Chase and High, remembered as `wz.view.drive` (separate from `wz.view.jungle`). Automatic changes never write it (M2 review 8).
5. **Gun:** V switches Close and Wide with a 0.3 s blend, remembered as `wz.view.gun` in both modes. The VIEW AUTO badge is hidden.
6. **Resets and the swap** cut (`Snap()`) behind the fade.
7. **Chase rig:**
   - pivot 1.7 m above the jeep's origin;
   - distance 6.8 m at rest, rising to 8.3 m at top speed; pitch 10 degrees;
   - FOV 58 at rest, rising to 68 at speed;
   - yaw follows the heading through a spring (half-life 0.18 s, lag up to 25 degrees, so the jeep's side shows in turns);
   - look input orbits ±150 degrees yaw and -10 to +35 degrees pitch, and eases back (half-life 0.35 s) 1.2 s after the last look input. It holds while reversing;
   - collision through `WorldCast`: a 0.3 m sphere that skips the jeep's own block, at least 0.8 m above the ground;
   - the foliage fade (`FoliageFade`) is centred on the jeep;
   - portrait: +1.5 m distance and a wider lens, like `CameraFollow.Rig`.
8. **High view while driving:** world-aligned, as on foot. 16 m up and 13 m back, with look-ahead along the velocity (0.8 s x v, up to 14 m).
9. **Gun rig:** the camera is the aim authority, and the turret slews to it (240 degrees a second with a mouse, 140 on touch). A small marker shows where the barrel points while it lags.
   - Close: pivot at the yoke plus 1.0 m, 2.6 m back, 0.35 m right, FOV 50; zoom FOV 32.
   - Wide: 6 m back, 2.2 m up, FOV 60.

---

## 6. Touch layout

Landscape, canvas units as in `TouchLayout`. Slots reuse the M2 buttons. Each scheme is tested for being on screen and free of overlaps at 16:9, 19.5:9 and 9:19.5. Portrait uses the M2 column on the right at 0.85x.

### 6.1 Drive

| Slot (M2 button) | Control |
| --- | --- |
| Left half | Floating stick; its x is steering (dead zone 0.12) |
| Right half, empty area | Drag to orbit the chase camera (recentres) |
| Corner, big (JUMP) | **GAS**, held |
| Left of it, big (FIRE) | **BRAKE / REV**, held; reverses once stopped |
| Above GAS, small (DROP) | **EXIT**, shown only below 2.5 m/s |
| Arc (DASH) | **HANDBRAKE**, held |
| Arc (BOMB) | **RESET**, shown only when offered |
| Top right | **VIEW** (Chase / High, AUTO badge as M2) |

On foot near the jeep, the context button reads **DRIVE**; at B it reads **GUN**.

### 6.2 Gun

| Slot | Control |
| --- | --- |
| Anywhere off the buttons (both halves) | Drag to aim |
| Corner, big (JUMP slot) | **FIRE**, held; a drag that starts on it also aims |
| Above FIRE, small | **ZOOM** (toggle) |
| Top right | **VIEW** (Close / Wide) |
| Others | Hidden |

**Aim assist on touch** (default on, sharing the DRIVE ASSIST toggle):
- look slows to 50% within 3 degrees of a live target;
- a gentle pull of 15 degrees a second toward its centre within 4 degrees, never through scenery (`WorldCast`);
- the heat bar sits beside FIRE.

---

## 7. Risks

| Risk | Mitigation |
| --- | --- |
| **Heap step to 344 MB at Low** | Probe the headroom first (iteration 0). Jungle bundle +1.5 MB at most, with no new textures. Art goes in the `Vehicles` group, loaded after the jungle's load peak. Offsets ready (landscape alphamap and heightmap). Measure in iterations 2, 12 and 15. |
| Addressables duplicate shaders or shared assets into `Vehicles` | Analyse rule check; a `Shared` group for shaders if needed; a test or builder check fails on duplicates. |
| The terrain seam between three terrains of different resolutions | The tile's edges blend to the landscape's own 16 m interpolant; stills from the air in iteration 2. |
| Handling feels floaty or twitchy without physics | Numbers in `GameConfig`; an EditMode spec for each behaviour; a greybox user look in iteration 5 before the art; speed-sensitive steering; drive assist on touch. |
| Soft-locks (wedged between trunks, nose in a bank, a gap in the tree wall) | Stuck, tilt and off-road rules with RESET and auto-reset; a hidden corridor clamp; the road walkable on foot; noisy-bot tests in CI. |
| Tunnelling at speed | 0.25 m sub-steps; dt = 0.1 tests. |
| `WorldCast` cost with 3-4x the trunks and HMG rounds | Trunk grid (iteration 2), equal to brute force in tests; fps measured while firing. |
| Bolts dying at the old 66 m bounds, or the old clamp walls in the tile | `World.Bounds` / `OutOfBounds` replace `HalfW` / `HalfD` in the jungle (iteration 2); PlayMode test. |
| Tripo fuses the wheels or gun into the body | "No wheels, on jack stands" prompts plus multiview review before the model; re-roll budget; try part segmentation if the docs offer it. |
| The squadmate clone reads as "two Wardens" | Tint, helmet and seated framing; the play-test decides; the autopilot fallback is a code change. |
| The `sit` clip doesn't fit a jeep seat | The procedural `VehiclePose` seat is the primary path for legs; the clip is judged on a contact sheet. |
| Touch: driving and aiming on a phone | Familiar left-stick steering plus pedal buttons; assists; real-phone checks in iterations 7, 10 and 15. |
| First load creeping (clips, code) | Measure after iteration 13. If over 22.8 MB, move the vehicle clips into a jungle-only `AnimatorOverrideController` (as planned in M2). |
| M2 tests that assume A ends the slice | Updated deliberately in iteration 5; the M2 course tests are kept bit-for-bit (`Height` unchanged at blocks). |
| Two agents, one repo | The art agent writes only `Assets/Art/Campaign/Vehicles/` (FBX, no metas), `ArtSource/M3Vehicles/` and the TRIPO_LOG M3 section. The engineer merges `m3-art` before iteration 12. |

---

## 8. Out of scope

- Enemies that shoot back, vehicle damage or health, ambushes, real Stage 2 soldiers and creatures: **M4**. M3's targets are placeholders.
- The corridor beyond the ride's end, checkpoint C onward, corridor bounds outside the road tile: **M4**.
- A river boat, a quad bike, other vehicles, passengers besides the squadmate, the Warden riding as a passenger.
- PhysX, real rollovers, deformation, destructible trees, the jeep driving over blocks or ledges (blocks are walls to it).
- Gamepad, mirrors, a speedometer, headlights or night driving, horn.
- Weather, final audio and the quality tuning pass: **M5**.
- Section streaming of the whole jungle (unloading the drop's assets). M3 only adds the `Vehicles` group; the general streaming design is M5.
- Stage 1 changes.

## 9. Open questions for the user

None blocking. Two confirmations that can wait for the greybox look (iteration 5):
1. The squadmate as a re-tinted Warden in the jump helmet, or the "autopilot" jeep with no driver?
2. Continue after finishing M3 replays the ride from B. Is that fine until M4 adds checkpoint C?

---

## 10. Review changes and user decisions (binding; they override sections 0-9)

PM decisions (2026-10-12; the user can override after the greybox look):
- **User decision (overrides the PM call): a re-coloured Warden squadmate drives the ride**, as in section 0.2. He is cloned at runtime from the live model, with no x-ray materials, tinted desaturated tan and olive with the cyan off through a `MaterialPropertyBlock`, and wears the M1 jump helmet. He uses the `sit` clip and hand IK on the steering wheel. Measure the cost of the second skinned Warden at Low on phones. If it hurts, cull his updates when he is off screen. No autopilot light.
- **Continue after the end replays the ride from B** (user confirmed), which starts automatically (item 5). There is no save at the ride's end.
- **Cuts:** the creek ford, the outpost tent, the boom post and arm (the gate reuses the M2 rubble and crates), and turret slew and the barrel marker (the gun follows the camera exactly).
- **HMG fire rate:** state it, and tune heat so a player firing in bursts rarely overheats over a 50 s ride. Overheat is a short penalty, not a dead stop.

Reviewer changes (all required):
1. `World.Ground` picks the terrain by rectangle (patch, road tile, landscape). The jeep, `TrunkBase`, the `WorldCast` ground march, the landing, `Surface()` and the fog all use it. PlayMode tests: `World.HeightAt` matches the tile terrain on the road and at both seams, and a short drive on the real terrain.
2. Jeep scale fits the 2.65 m Warden (`ModelScale` 1.4): about 3.1 m wheelbase, 2.0 m track, 0.53 m wheels and 1.2 m footprint circles, decided in iteration 3. Re-check road width, speeds and camera distances. Fit the Tripo body to the `GameConfig` numbers.
3. Seams:
   - a flat rim band of about ±8 m across z = 80 with no road carve on the seam;
   - a skirt mesh down the tile's free edges;
   - an EditMode test that the patch and the tile agree and are flat along the shared edge.
4. Re-route the climb from A to D2 so the grade stays at 10% or less, the bank zone stays clear of A's capture ring, and D3 sits at least 40 m from the tile edge. Otherwise state the exceptions before iteration 1.
5. At B, once secured (and on Continue at B), the swap to the gun starts automatically after a short beat. Drop the GUN prompt and the `InVehicle` save field. Add `CheckpointB` to the `CampaignSave.Load` whitelist.
6. The HMG is hitscan through `WorldCast` with destructibles as targets, plus a tracer visual. Test: hits at dt = 1/20.
7. No pausing during the reset and swap fades (like `StageLoader.Busy`), or a scaled-time fade drawn below the menus. PlayMode test.
8. Cameras: seat views that keep pointer lock; `driveCam` and `gunCam` in `OnDisable`, `Cut`/`Snap` and `GameplayBlends`; the swap and resets run in Play mode, not `BeginCinematic`. Test: the lock is kept through get-in, get-out and the swap.
9. While seated, `PlayerController` returns before any `Consume*` or prompt call, and `VehicleController` owns the buttons. The press that enters the jeep must not also jump. Add `Seated` to the `Sling` rule.
10. Reset the new shared state:
    - `World.Use` resets `Bounds`/`OutOfBounds`, the trunk grid and the vehicle block;
    - `Destructible.All` is cleared in `GameManager.ClearWorld` and on scene unload;
    - the `Vehicles` load handle is released when leaving the jungle.
11. Addressables: the jungle scene never references `Vehicles` prefabs directly; it loads them by address. The greybox is only a fallback if the load fails, never a wait that can hang. The jeep stays hidden until its art arrives. A builder check confirms the Jungle bundle holds no `Vehicles` assets.
12. Destructibles give score only, or level-ups wait until the ride ends. No upgrade picker mid-ride.
13. Order: the drive camera (iteration 6) comes before iteration 5's user look. The V1 prompt asks for a body with no wheels and empty arches; stray geometry is cut at import.

Adopted suggestions:
- Touch RESET shows after 2 s under 1 m/s, matching R. If every exit is blocked, reset first, then get out.
- The ride corridor joins the bounds only once the ride starts.
- Wheel `SurfaceAt` samples skip the jeep's own block. He cannot stand on the jeep.
- Memory probe: allocate in chunks. The fold-back rule depends on a measured build. Record the High heap too.
- Measure fps at D2 at Low.

---

### Critical files for implementation
- /Users/rajg/Codes/combat_game/unity/Assets/Scripts/World.cs
- /Users/rajg/Codes/combat_game/unity/Assets/Scripts/JungleLayout.cs
- /Users/rajg/Codes/combat_game/unity/Assets/Scripts/JungleStage.cs
- /Users/rajg/Codes/combat_game/unity/Assets/Scripts/CameraFollow.cs
- /Users/rajg/Codes/combat_game/unity/Assets/Editor/SceneBuilder.Jungle.cs

Also central: `Scripts/PlayerController.cs`, `Scripts/TouchControls.cs` / `TouchLayout.cs`, `Scripts/CampaignSave.cs` / `Campaign.cs`, `Scripts/Bolt.cs` / `GameConfig.cs` (`PointInWall` and `ResolveCircle` read the centred `HalfW` / `HalfD` bounds and must change for the road tile), and `Editor/AddressablesSetup.cs`.