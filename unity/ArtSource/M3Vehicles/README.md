# M3 vehicles: jeep, gun, seated clips, targets and outpost (Tripo)

Art for Milestone 3 (`unity/M3_TASKS.md` sections 2.1-2.4, with the coordinator's review changes:
the Warden is 2.65 m, the jeep is about 1.4x a real M151, nothing modelled under the body, a re-coloured
Warden squadmate drives the ride, no tent, boom post or boom arm). Targets and the hesco wall are in
[Targets and outpost](#targets-and-outpost) at the end.
Credits and task ids: the M3 section of `Assets/Art/Campaign/TRIPO_LOG.md` (props) and
`Assets/Art/Warden3D/TRIPO_LOG_M3.md` (clips).

What is here:
- `contact_props.jpg`: each accepted part from six angles (front = Tripo +X, then 45, 90, 135, 180 degrees and top).
- `contact_assembly.jpg`: all parts assembled with the numbers below (Blender, from the task GLBs).
- `fit_unity.jpg`: the same assembly built from the FBXs in a throwaway Unity 6000.3 project (untextured).
- `fit_cockpit.jpg`: the dashboard before the cut, after the cut, and with `jeep_steering` in place.
- `rejected_props.jpg`: the two rejected jeep bodies. `art/v1c_jeep_multiview_rejected.jpg`: the rejected multiview.
- `contact_clips.jpg` / `rejected_clips.jpg`: key frames (side, then front) of each clip.
- `art/` concept images, `previews/` Tripo's own renders, `requests/` the request bodies,
  `tasks/` task records (expiring URLs replaced by file names, storage keys stripped).

## Pipeline

- Concepts: image-to-image `chat_image_2.5_sunburst`, medium, transparent. The jeep concept used
  `docs/warden-hero.png` (uploaded as `file_60faf8e6-…`) as a colour reference; the four parts used the jeep
  concept task as their reference, so they share its olive, gunmetal and wear.
- Models: image-to-model `v3.1-20260211`, texture + pbr, standard, `auto_size`, input = the concept task id.
- FBX converted from each task id.
- Part segmentation was checked first (developers.tripo3d.ai, 2026-10-12): `generate_parts` is rejected with
  texture or pbr on, and `mesh/segment` is a separate untextured-geometry step. So the parts are separate models.
- **No multiview for the body.** Image-to-multiview turned the side view into four three-quarter views (the
  body would have come out diagonal, like the chopper) and invented a steering wheel in two views. Rejected
  without spending on a model. Single-image image-to-model came out axis-aligned instead.
- Measured in Blender 5.2 (vertex queries on the GLBs) and checked in Unity 6000.3 (default import, every
  pivot below re-measured against the FBX geometry; `fit_unity.jpg`).

## Accepted / rejected

| Part | File | Verdict | Reason |
| --- | --- | --- | --- |
| Jeep body, roll 1 | - | Rejected | Good body, but the "steering column stub" came out as a 0.75 m lever rising from the floor, ending over the seat cushion. A wheel on it would sit in the driver's lap. Also a cyan smear on the bed's rear wall. |
| Jeep body, roll 2 | `jeep_body.fbx` | **Accepted, with one cut** | Best paint and detail. Empty arches with bare hubs, seats, roll bar, pedestal post and plate, jerry can, dashboard with gauges. Tripo modelled a small fused steering wheel on the dash; cut it at import (box below). |
| Jeep body, roll 3 | - | Rejected | Asked for a plain dashboard, Tripo still added a small wheel on a column. Paler, washed-out paint, windshield frame half raised. |
| Wheel | `jeep_wheel.fbx` | Accepted | Mud-terrain tyre, olive five-slot rim with hub cap, open dish behind. Round to 1 mm; centred on its spin axis. |
| Steering wheel | `jeep_steering.fbx` | Accepted | Three-spoke, black rim, olive cap with a small cyan light, column stub. |
| Gun yoke | `gun_yoke.fbx` | Accepted | Traverse ring, U yoke with two open trunnion rings, small olive shield. |
| HMG | `gun_hmg.fbx` | Accepted | Ribbed barrel, flash hider, box receiver, twin spade grips, ammo can with belt, cyan reflex sight, trunnion pins on both sides. |

Face counts: body 18,441 (limit 20,000), wheel 2,829 (3,000), steering 1,452 (1,500), yoke 3,763 (4,000), HMG 5,756 (6,000).

## Conventions (same as M1 and M2)

- Raw FBX with four embedded 2048 px PBR textures each (base colour, normal, metallic, roughness).
  Extract them and cap them through `ArtImportSettings` (section 2.4 of the plan).
- The FBX root has a 270-degree X rotation (Z-up FBX). **Keep it**: put each FBX under your own pivot
  GameObject and transform the pivot. All numbers below are in that pivot's space (Unity axes, metres as
  imported, scale 1), unless they say "jeep space".
- Origin at the bounding-box centre of every file.
- Unity = glTF with X negated (checked on the HMG muzzle and the dash cut). Tripo's "front" (+X) is Unity -X.
- **The body already faces Unity +Z** (nose +Z, Y up). No PCA turn is needed. Unity -X is the vehicle's
  left: the driver seat (with the steering wheel) is on -X, so it is left-hand drive like an M151.

## Jeep space (body pivot at the origin, scale 1)

Body size 2.185 x 1.800 x 4.657 (X x Y x Z). Lowest point y -0.900.

**Game scale.** Uniform `k = 1.187` gives a 3.10 m wheelbase. Then the body is 2.59 x 2.14 x 5.53 m and the
wheels have a 0.522 m radius. For exactly 0.53 m use `k = 1.205` (wheelbase 3.15 m). The body is wider
than an M151 for its wheelbase, so the flush track is 2.18 m (see the wheels).

**Wheels** (`jeep_wheel.fbx`, radius 0.325, width 0.294, spin axis = local X, rim face on local -X):

| | x | y | z |
| --- | --- | --- | --- |
| Front left / right | -0.92 / +0.92 | -0.675 | +1.568 |
| Rear left / right | -0.92 / +0.92 | -0.675 | -1.044 |

- Scale 1.354 (radius 0.44 in jeep space). Left wheels (-X) unrotated; right wheels yaw 180 so the rim faces out.
- The centres are level (same y) so the body sits level. They are the largest-clearance fit near the modelled
  hubs. The hub discs are at (±, -0.624, +1.648) front and (±, -0.707, -0.984) rear. They are not centred
  in the arches, so the fit is 0.07-0.10 off them; the hubs end up inside the wheel dish.
- Free radius in the arches: at least 0.44 for both axles. A bigger wheel touches the arch lips.
- x ±0.92 puts the tyre face flush with the fender flares (body max |x| 1.092): track 1.84, or 2.18 m at k 1.187.
  For a 2.0 m track use x ±0.842; the tyre then sits 5 cm inside the flares, still clear of the body.
- Tyre bottoms at y -1.115, so the body origin is 1.115 above the ground (1.32 m at k 1.187).
  Ground clearance under the body: 0.215 (0.26 m).
- Wheelbase 2.612, front to rear axle.

**Stray geometry to cut at import (body):** the fused dash steering wheel. Drop every triangle whose three
vertices are inside the box min (-0.63, -0.02, 0.22), max (-0.29, 0.30, 0.455). That removes 219 triangles
and leaves a clean dash face plus a 4 cm stub that the steering wheel's own column hides (`fit_cockpit.jpg`).
Nothing else needs cutting: the body has no stands under it. The dark fill inside the arches stays behind the tyres.

**Steering wheel** (`jeep_steering.fbx`, rim outer radius 0.177, tube centre radius 0.158, spin axis = local X,
rim face on local -X at x -0.095, column stub to local +x 0.125):

- Pivot position (-0.46, 0.092, 0.445), rotation `Quaternion.LookRotation(Vector3.left, new Vector3(0, 0.917, 0.398))`, scale 1.26.
- That puts the rim centre at (-0.460, 0.140, 0.335), facing the driver along (0, 0.398, -0.917): tilted 23.5
  degrees back from vertical, like the fused wheel it replaces. Rim outer radius 0.223 (0.26 m at k 1.187).
- **Turn it about the pivot's local X.** Hand grips (tube centre, 9 and 3 o'clock) in pivot space:
  left (-0.095, 0, +0.158), right (-0.095, 0, -0.158). In jeep space at zero steer: left (-0.659, 0.140, 0.335),
  right (-0.261, 0.140, 0.335). Each is within 0.02 of rim vertices.

**Seats** (cushion top, then the front face of the back rest):

| | cushion x range | cushion top y | cushion z range | back rest front z | back rest top y |
| --- | --- | --- | --- | --- | --- |
| Driver (left) | -0.649 to -0.255 (centre -0.452) | -0.265 | -0.297 to +0.219 | -0.376 | 0.374 |
| Passenger | +0.252 to +0.641 (centre +0.447) | -0.266 | -0.295 to +0.218 | -0.372 | 0.373 |

Driver footwell floor about y -0.54 (z 0.22-0.47), so the cushion is 0.27 above it (0.33 m at k 1.187).
The steering rim centre is 0.41 above the cushion and 0.71 in front of the back rest.

**Bed and pedestal:** bed floor top about y -0.15 (z -1.95 to -1.0). Side walls top 0.538, tailgate top 0.225.
Roll bar hoop top y 0.899 at z -0.84 to -0.94. Pedestal post centre (0.003, -, -1.359), radius 0.065;
top plate centre (0.007, 0.640, -1.361), diameter 0.36, top at y 0.640.

## Gun

**Yoke** (`gun_yoke.fbx`, 0.678 x 0.500 x 0.680, ring base at local y -0.250, yaw axis = local Y through x 0 z 0,
shield on local -X, trunnion rings at local (0.022, 0.188, ±0.26), axis local Z, inner faces 0.40 apart, centred z +0.010):

- Pivot position (0.007, 0.890, -1.361), rotation `Quaternion.Euler(0, 90, 0)` (shield forward, +Z), scale 1.0.
  The ring sits on the plate top. **Yaw it about the pivot's Y.**
- Trunnion axis in jeep space: through (0.017, 1.078, -1.383), along X.

**HMG** (`gun_hmg.fbx`, 1.070 x 0.850 x 2.752, muzzle at local -Z):

| Point (pivot space) | x | y | z |
| --- | --- | --- | --- |
| Trunnion axis (pins along X) | 0.060 | 0.114 | 0.727 |
| Muzzle (flash hider mouth) | 0.077 | 0.114 | -1.376 |
| Barrel axis | x 0.077, y 0.114, along Z | | |
| Spade grip, gunner's right | -0.155 | 0.04 (grip spans -0.16 to 0.25) | 1.30 |
| Spade grip, gunner's left | 0.219 | 0.04 | 1.31 |
| Sight lens | 0.054 | 0.373 | 0.603 |

- Pivot position (0.059, 0.997, -0.869), rotation `Quaternion.Euler(0, 180, 0)`, scale 0.707 (2.31 m long at
  k 1.187, a real M2 at 1.4x). Its trunnion then lands on the yoke's: (0.017, 1.078, -1.383).
- For pitching, add a `GunPitch` node at the trunnion and parent the gun under it. Jeep space at zero yaw and
  pitch: muzzle (0.005, 1.078, 0.104), gunner's right grip (0.169, 1.025, -1.788), left grip (-0.096, 1.025, -1.795),
  sight (0.021, 1.261, -1.295).
- Clearance: pointing forward, the barrel clears the roll bar hoop by 0.13, so forward depression is about
  16 degrees before it touches. Over the sides and rear there is no limit from the body.
- **Grip height is low for a 2.65 m Warden.** Grips are 1.18 above the bed floor (1.39 m at k 1.187), about
  his belt. If the gunner pose reads hunched, add a code-built riser (a gunmetal cylinder, radius 0.065, the
  post's own size) and lift the yoke and gun by the same amount: +0.35 puts the grips at about 1.8 m. The
  grips are 0.43 behind the pedestal, so he stands near the tailgate (z about -1.9 to -2.0).

## Clips

File: `Assets/Art/Warden3D/Anims/warden_anims_m3_vehicle.fbx`. One retarget on the Warden rig task
`e73d30f3-…` with the M1 options (glb, bake_animation, export_with_geometry, animate_in_place), converted to
FBX from the task id. Same bones as `warden_rigged.fbx`, so it uses that Humanoid avatar. 24 fps.
Takes in Unity: `Armature|sit`, `Armature|play_video_game`, `Armature|wave_goodbye_01`, `Armature|greet_02`,
`Armature|greet_01` (rejected, do not import).

Checked in a throwaway Unity 6000.3 project (Generic import, `AnimationClip.SampleAnimation`): hips, hands and
feet match the GLB to 0.01 m, X negated. Not checked through a Humanoid Animator graph. Heights below are at
scale 1 (multiply by 1.4 in game).

| Suggested clip | Take | Trim | Loop | Use |
| --- | --- | --- | --- | --- |
| `seated` | `sit` | 0-1.9 s | yes | **Seated hold (legs and body)** for the driver, the squadmate and the gunner's rest. Upright, hands on the thighs. Hips 0.67 above the feet, feet flat (0.09). It is static, so the loop is clean. 2.0-6.6 s is a chin-on-hand "thinker" gesture, then he returns. |
| `drive_hold` | `play_video_game` | 0-3.0 s (or 0-12.6) | yes | **Driver's arms.** Seated, leaning back a little, both hands forward together at 0.5 in front of the hips and 0.23-0.35 above them, 0.27 apart, with small fidgets. Use it on the upper body with hand IK onto the steering grips. The hands drop at 15.0-15.75 s. |
| `drive_wave` | `wave_goodbye_01` | 1.5-5.2 s | no | **Squadmate's wave from the driver seat.** Both hands at wheel height (0.21 above the hips, 0.5 in front), then the left hand comes off and waves overhead (2.4-4.6 s, up to 1.63), then back. The left hand is the door side in this left-hand-drive jeep. Upper body only: his right knee is raised (foot 0.51 up) for the whole take, so keep the legs on `seated`. |
| `wave_standing` | `greet_02` | 2.2-4.6 s | no | Optional: the squadmate standing at B, one right-hand wave (up to 1.97). Feet planted, hips steady at 1.02. |

**Seat height mismatch.** In `sit` his seat is about 0.8 m (game) above his feet (estimated from the hips at 0.67). The jeep's cushion is
0.33 m above the footwell. So with the hips on the cushion, the knees ride high unless the feet are put on the
footwell or pedals with IK. The plan's procedural `VehiclePose` legs are the right tool for that.

**Gunner:** no retarget. Nothing in the preset list holds spade grips. Use the existing standing idle or
`fire` with hand IK onto the grips above (unverified).

### Rejected

| Take | Why |
| --- | --- |
| `greet_01` | He walks forward while waving, and the hips climb 0.30 m over 3.5 s (1.02 to 1.32). Not a seated or standing wave. |

The coordinator's first note dropped the squadmate; the second restored it. The batch had already been run,
so no clip was re-bought.

## Targets and outpost

Same pipeline as the jeep parts: concepts by image-to-image with the jeep concept task as the style and
colour reference, image-to-model `v3.1-20260211` (texture + pbr, standard, `auto_size`), FBX from the task id.
Files are in `Assets/Art/Campaign/Vehicles/` next to the jeep. Cut by the review: the tent, the boom post and the
boom arm (the gate reuses the M2 rubble and crates). The plan has no wreck states, so every target is one
intact mesh and the destruction is effects. The only moving part is the pop-up plate, which turns on its own hinge.

What is here: `contact_targets.jpg` (each accepted model from six angles, one row each, in the order of the
table), `fit_targets_lineup.jpg` (all of them at the suggested scales beside the 2.65 m Warden),
`rejected_targets.jpg` (hesco roll 1 on the left, the rejected roll 2 on the right).

### Accepted / rejected

| # | File | Tris (limit) | Verdict | Reason |
| --- | --- | --- | --- | --- |
| T1 | `fuel_drum.fbx` | 1,314 (1,500) | Accepted | Olive drum, wide red band with a flammable diamond, two ribs, bung caps, rust. Reads as "explosive" at range. |
| T2 | `target_plate.fbx` | 1,118 (1,200) | Accepted | Head-and-torso steel plate, white hit circle on both faces, hinge pin along the full bottom edge. |
| T3 | `sandbag_nest.fbx` | 4,926 (5,000) | Accepted | U of three courses of tan sandbags, torn bags, open at the back. |
| T4 | `watchtower.fbx` | 5,120 (6,000) | Accepted | Four lashed legs with X bracing, plank platform with a waist wall, rusty tin roof, ladder. Clean, thin silhouette. |
| T5 | `fuel_trailer.fbx` | 4,556 (5,000) | Accepted | Olive tank with hazard stripes and a red diamond, two wheels with mudguards, A-frame tow bar on a drop leg. |
| T6 | `roadblock.fbx` | 5,703 (6,000) | Accepted | Three logs on two X-frames, razor-wire loops along the top, skull sign. The X-frames read as sawhorses more than welded hedgehogs; fine at game distance. |
| O1 | `hesco_wall.fbx` | 2,766 (3,000) | Accepted, needs a yaw | Four mesh-and-geotextile cells with earth on top. Tripo laid it diagonally (see below). |
| O1 roll 2 | - | 2,702 | Rejected | Axis-aligned, but the texture lost the wire mesh, so it reads as four fabric sacks. |

### Measurements

Unity axes, the FBX under your own pivot with its 270-degree X root kept, scale 1. Every origin is the
bounding-box centre, so each prop's base is at y = -height/2. "Front" means Tripo's +X, which is Unity -X.

| File | Size X x Y x Z | Suggested scale | Result (m) | Notes |
| --- | --- | --- | --- | --- |
| `fuel_drum` | 0.486 x 0.650 x 0.484 | 1.9 | 0.92 x 1.24 x 0.92 | Upright on Y. Shell radius 0.232 (ribs 0.243). Bung caps on the top (y 0.30 to 0.325). Blast centre: the origin. |
| `target_plate` | 0.122 x 1.200 x 0.620 | 1.4 | 0.17 x 1.68 x 0.87 | **Hinge:** pin axis along Z through x -0.003, y -0.571 (0.029 above the base), z -0.31 to +0.31, radius about 0.03 (a probe on the axis end is 0.02 from the pin's vertices). The plate body is 0.056 thick, from x +0.005 to +0.061, just behind the pin. Put a pivot node on the hinge axis and turn it about Z: about ±90 degrees lying flat, 0 upright. Both faces have the hit circle. |
| `sandbag_nest` | 2.580 x 1.200 x 3.823 | 1.2 | 3.10 x 1.44 x 4.59 | The closed wall runs along Z at x -1.29 to -0.61 and faces -X (the front). The arms run toward +X at z -1.91 to -1.1 and +0.9 to +1.91, so the U opens to +X. Wall top y +0.48 to +0.60, 1.08-1.2 above the ground. Free ground inside: x -0.6 to +1.29, z -1.1 to +0.9. Plates go inside, behind the closed wall. |
| `watchtower` | 2.749 x 6.000 x 3.272 | 1.4 | 3.85 x 8.40 x 4.58 | Ground y -3.0; the legs splay to x ±1.37, z ±1.64. **Deck** top y +0.87 (3.87 above the ground, 5.4 m at scale 1.4), centre (0, 0.87, +0.255), footprint x ±1.03, z -0.79 to +1.30. Plank wall top y +1.58 (0.71 above the deck), roof eave +2.22, ridge +3.0. A plate on the deck shows its upper half over the wall. |
| `fuel_trailer` | 1.669 x 1.500 x 2.585 | 1.4 | 2.34 x 2.10 x 3.62 | Ground y -0.75. Tow bar forward to +Z, ring at (0, -0.155, +1.14 to +1.29). Tank y 0 to +0.75, z -1.18 to +0.34; its centre (0, 0.375, -0.42) is the big-chain blast centre. Wheels at abs(x) 0.58 to 0.83. The red diamond side is the front (-X). |
| `roadblock` | 1.040 x 1.200 x 3.024 | 2.0 | 2.08 x 2.40 x 6.05 | Long along Z; X-frame feet at y -0.6, abs(x) at most 0.52. Razor wire y +0.4 to +0.6. The skull sign faces the front (-X); turn that side toward the jeep. |
| `hesco_wall` | 1.742 x 0.600 x 1.555 as imported | 2.33 (plan size) or 2.8 (Warden scale) | 4.41 x 1.40 x 1.31, or 5.29 x 1.68 x 1.57 | **Lies at a diagonal.** Pivot yaw `Euler(0, -41.2, 0)` puts its length on X (then 1.891 x 0.600 x 0.560, centre offset (0, 0, -0.012)); `Euler(0, 48.8, 0)` puts it on Z. Both measured in Unity. Four cells along the length. |

Scales follow the jeep: real sizes times about 1.4 for the 2.65 m Warden, or the plan's own size where it
gives one (hesco 1.4 m tall, roadblock 6 m wide). See `fit_targets_lineup.jpg`.

### Memory notes (the `Vehicles` group's 5 MB budget)

Measured from the task GLBs' texture sets (all 2048 px: base colour, normal, and an ORM map):
- **Occlusion is constant 1.0 and metallic is flat near zero** in all 12 Vehicles models (metallic mean
  0.02-0.10, spread at most 0.05; the yoke 0.13). Drop both maps and set metallic as a constant per material.
- **Roughness is flat** (spread at most 0.07) on the tower, trailer, roadblock, hesco and steering wheel. Those need
  only colour and normal, with constant smoothness (1 - roughness): tower 0.25, trailer 0.56, roadblock 0.17,
  hesco 0.07, steering 0.52.
- Roughness varies on the body, wheel, HMG, drum, plate, nest and yoke (spread 0.08-0.27). Give those the
  128 px generated metallic-smoothness map M2 used, rather than an alpha channel on the colour map
  (which would double the colour map's size).
- **Sharing a material:** each model has its own UV layout, so they cannot share a material as they are.
  The drum, plate and hesco (256 px colour by the plan) could share one 512 px atlas if their UVs are scaled by
  0.5 and offset at import. That saves materials, not memory. Repeated drums, plates and wheels already share
  one mesh and material each, so instancing or static batching covers them.
- Unique triangles in the group: about 32,200 for the jeep set (the wheel mesh is used four times) and about
  25,500 for the targets and hesco, so about 58,000 in all.
- Rough texture size at the plan's caps with DXT1 colour, BC5 normals and mips (estimate, not measured):
  colour about 2.2 MB, normals about 0.3 MB, the smoothness maps under 0.1 MB. Meshes come on top. The real
  figure needs an Addressables build.
