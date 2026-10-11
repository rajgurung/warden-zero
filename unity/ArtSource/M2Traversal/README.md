# M2 traversal: Warden clips and course props (Tripo)

Art for Milestone 2 (`unity/M2_TASKS.md`, sections 2 and 8). Everything came from Tripo.
Credits and task ids: `Assets/Art/Warden3D/TRIPO_LOG_M2.md` (clips) and the M2 section of
`Assets/Art/Campaign/TRIPO_LOG.md` (props).

What is here:
- `contact_clips.jpg`: the accepted clips, key frames, side and front.
- `rejected_clips.jpg`: the rejected clips.
- `contact_props.jpg`: every accepted prop from five angles.
- `art/`: the concept images Tripo drew (image-to-image from `docs/warden-hero.png`).
- `previews/`: Tripo's own render of each model.
- `requests/`: the exact request bodies. `tasks/`: task records, with expiring URLs replaced
  by file names and storage keys stripped.

## Clips

Preset list re-checked on developers.tripo3d.ai/en/docs/animations-retarget on 2026-10-11.
It is unchanged from the M1 snapshot in `ArtSource/M1Polish/README.md`. There is still no vault,
ledge hang, pull-up or mantle preset for the v1.0 rig.

Two test batches of five presets were run (10 untried presets). The three keepers were then
retargeted again on their own (`a3`), so the FBX in Assets holds only accepted takes.

All retargets ran on the Warden rig task `e73d30f3-…` with the M1 options (glb,
bake_animation, export_with_geometry, animate_in_place). The FBX was converted from the
task id. It has the same bones as `warden_rigged.fbx`, so it uses that Humanoid avatar.

File: `Assets/Art/Warden3D/Anims/warden_anims_m2_traversal.fbx`. It holds 3 takes at 24 fps.
In Unity they are named `Armature|basketball_shot`, `Armature|jump_rope_01` and `Armature|run_upstairs`.

| Suggested Unity clip | Take | Trim | Loop | Root Y (suggested) | Use |
| --- | --- | --- | --- | --- | --- |
| `jump_grab` | `basketball_shot` | 1.45-2.95 s | no | Original, Offset about -0.2 m (see below) | **Jump to a ledge.** Crouch at 1.6-1.75 s, spring with both arms straight up. Hands are at 2.13-2.15 m at scale 1 (2.05-2.3 s), so about 3.0 m at the game's 1.4x. Then he lands at 2.5 s. For a grab, stop at about 2.2 s and hand over to the hang pose. Use 1.45-2.05 s as a generic jump takeoff. |
| `climb_step` | `run_upstairs` | 0-0.75 s | yes | **NotBaked** | **Mantle / step-up legs and wall-climb legs.** A high-knee stair step. The hips rise 0.5 m per cycle (1.05 to 1.56 m) and snap back at the loop. So the rise must become discarded root motion, or the loop pops. |
| `jump_hop` | `jump_rope_01` | 2.90-3.60 s | no | Feet | **Short gap jump (optional).** One small two-footed hop, arms low. Feet leave the ground by about 0.3-0.5 m and land at 3.6 s. It is a fallback in case the step A `jump` re-trim reads badly. |

Checked how:
- Contact sheets were rendered from the task GLB with three.js.
- The FBX was then sampled in a throwaway Unity 6000.3 project (Generic import, `AnimationClip.SampleAnimation`).
- Hips, hand and foot heights match the GLB to 0.01 m at every sample. X is negated, which is the normal FBX handedness flip.
- three.js's FBXLoader shows these Tripo FBXs with wrong poses. That includes the M1 FBXs, which are known good in Unity. So judge the clips from the sheets here, not from an FBXLoader preview.

**Root Y in `basketball_shot`.** The feet start on the floor (0.05 m). From about 1.3 s they
stay about 0.2 m up (hips at 1.20 m instead of 1.02 m), through the jump and after landing.
Either set a root Y offset of about -0.2 m, or let `Traversal` drive the height and play
the clip as an air pose. The engineer's Animator feet check (iteration 6) should confirm this.
Not verified through a Humanoid Animator graph here.

No preset covers vault, hang, wall climb or drop. Those stay with step A re-trims
(`climb`, `jump_down`) and the procedural `TraversalPose`, as the plan says.

### Rejected

| Preset | Batch | Why |
| --- | --- | --- |
| `flip` | a1 | A sideways cartwheel / round-off with the body inverted. It reads as gymnastics and does not fit a vault over a log or crate. |
| `lift_heavy` | a1 | Squat, lift, squat. The hands stay at floor and knee height, never on a ledge top, and it is 19.5 s of mostly squatting. Not usable as mantle arms. `land` already covers rising from a crouch. |
| `press-up` | a1 | Prone push-ups. Horizontal and floating at hip height with animate_in_place. A mantle needs a vertical body. |
| `volleyball` | a2 | A one-arm spike jump that reads as an attack, then a deep crouch landing. The existing `land` already covers that. |
| `football_save` | a2 | A low crouched goalkeeper save. Nothing traversal-like. |
| `flee_01` | a2 | A run with a look back. |
| `warm_up` | a2 | Stretches and lunges. |

Not tried: rig v2.5 `climb` and `jump` (they would need a new rig with different bones, as the plan says).

## Props

Pipeline:
- image-to-image (`chat_image_2.5_sunburst`, medium, transparent) with `docs/warden-hero.png` as a colour reference only. The prompts ask for a realistic, photoreal PBR look, not the comic style, to match the CC0 jungle textures.
- image-to-model `v3.1-20260211`, texture + pbr, standard, `auto_size`.
- FBX converted from the task id.

Files are in `Assets/Art/Campaign/Traversal/` as raw FBX. Each FBX embeds its four 2048 px
PBR textures (base colour, normal, metallic, roughness). Extract them on import like `CampaignImport.Gear`,
to `Traversal/Textures/<name>/`, and cap them through `ArtImportSettings`.

**Conventions, measured in Unity 6000.3 with default import (same as the M1 props):**
- Size is Tripo's `auto_size` guess in metres. That guess is not the size the course needs, so every prop needs a scale. See the table.
- Origin at the bounding-box centre. The base is at `-height/2`, so lift the prop by `scale.y * height/2` to stand it on the ground.
- File root has a 270-degree X rotation (Z-up FBX). Keep it.
- After import: Y up, the long axis along local Z, depth along local X.
- The face shown as "front" in the sheets (Tripo +X) ends up on Unity local **-X**. This was checked on the wall: the vine and cornice geometry is on -X.

| File | Tris | Size in file X x Y x Z (m) | Suggested scale | Result X x Y x Z (m) | Top surface | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `fallen_log.fbx` | 4,829 | 0.607 x 0.500 x 2.199 | 2.45 uniform | 1.49 x 1.23 x 5.39 | Trunk top at 0.82 of the height, so 1.0 m at this scale. Branch stubs reach 1.23 m. | Vault (top 1.0 m). Round section; fit the block to the trunk, not the stubs. |
| `supply_crate.fbx` | 2,042 | 0.612 x 0.500 x 0.640 | 2.2 | 1.35 x 1.10 x 1.41 | Flat at 0.97 of the height | Olive wooden crate, steel corners, rope handles, cyan stripe. Stackable. |
| `supply_crate_b.fbx` | 2,214 | 0.667 x 0.500 x 0.948 | 2.2 (or 1.8 for 0.9 m) | 1.47 x 1.10 x 2.09 | Flat at 0.97 | Steel and olive hard case with latches and a cyan light (on -X). Same height as crate A at 2.2, so they stack and mix. |
| `ruin_wall.fbx` | 6,852 | 0.754 x 1.500 x 2.844 | (2.0, 3.0, 2.11) for 1.5 x 4.5 x 6.0; or 3.0 uniform | 1.51 x 4.50 x 6.00 (or 2.26 x 4.50 x 8.53) | Flat walkable top at 0.99, so 4.45 m | **Gate wall.** Vine curtain over the full -X face. Cornice ledge along the full width at about 0.69 of the height, so about 3.1 m. That figure is estimated from the front render, not measured; hand-set the ledge block. The back face is plain stone. The non-uniform option stretches the vines about 1.4x vertically, which is fine at game distance. |
| `rock_ledge.fbx` | 5,825 | 2.561 x 1.200 x 2.478 | 2.0 | 5.12 x 2.40 x 4.96 | Flat at 0.96-0.99, so 2.3-2.38 m | Grey granite shelf, steep sides, crisp top edge on every side. For the ford stepping blocks, scale Y down (for example (0.5, 0.4, 0.5)). |
| `gate_rubble.fbx` | 5,773 | 1.733 x 1.200 x 2.278 | 3.0 | 5.20 x 3.60 x 6.83 | Uneven mound: median 0.60, so 2.2 m; peak 3.6 m | Collapsed ruin heap with a column drum and lintel. **Its ends slope to the ground.** Use it as outer dressing, away from the wall, and back it with a tall block. |
| `gate_rubble_tall.fbx` | 5,721 | 0.448 x 0.600 x 0.811 | 7.5 | 3.36 x 4.50 x 6.08 | Jagged: median 0.78, so 3.5 m; peaks 4.1-4.5 m | **Re-roll made for the gate.** A steep, near-vertical barrier of jammed ruin blocks, tall along its whole length. Put it right against the wall's ends. Same stone as the wall. |
| `rock_outcrop.fbx` | 5,848 | 0.499 x 0.500 x 0.547 | 12 | 5.99 x 6.00 x 6.56 | Pointed, irregular: 0.63-1.0, so 3.8-6.0 m | Sheer dark granite crag with an overhang. Clearly unclimbable. Use it for the other side of the gate. |

The face counts are within the plan's limits: log 5000, crates 2500, wall 8000, ledge, rubble and outcrop 6000.

### Accepted / rejected

| Prop | Verdict | Reason |
| --- | --- | --- |
| fallen_log | Accepted | Clean, uniform mossy trunk with splintered ends. Reads at 5.4 m long. |
| supply_crate | Accepted | Matches the Warden palette. Flat lid, clean silhouette. |
| supply_crate_b | Accepted | A distinct second shape (long hard case) in the same palette. |
| ruin_wall | Accepted | Flat top, full vine face, clear full-width cornice ledge, square ends. |
| rock_ledge | Accepted | Flat table top with a sharp grabbable edge. |
| gate_rubble (mound) | Accepted, with a limit | It looks good, but its low, sloped ends do not read as impassable next to the wall. That is why the tall barrier was made. Use the mound only as outer dressing. |
| gate_rubble_tall | Accepted | The re-roll for the gate flank. Steep and tall end to end. |
| rock_outcrop | Accepted | Sheer and overhanging. Unmistakably a wall of rock. |

No model was broken. The only re-roll was the gate rubble, made for gameplay readability.
One wall-art request got HTTP 429 (too many image tasks at once). It cost nothing and was re-sent.
