# Warden 3D model (Tripo)

The current model is attempt 2: an unarmed A-pose Warden plus a separate rifle prop.
It was generated on 2026-10-10 with the Tripo v3 API and `tools/tripo/`.
Attempt 1 (rifle fused into the body) is kept for reference in `attempt1/`.

Overview: `previews/overview.png`. It shows, left to right: the original art,
the unarmed edit, the 3D front, the 3D back, the game camera view, and the rifle in hand.

## Files

| File | What | Size |
| --- | --- | --- |
| `warden.glb` | **Main file.** Rigged mesh with 7 baked clips. Hips root track cleaned (see below). For Babylon/web, and the reference for Unity. | 4.5 MB |
| `warden_rigged.fbx` | Rigged mesh, A-pose, no clips. Use it as the Unity Humanoid base avatar. | 4.6 MB |
| `warden_anims_tripo_raw.fbx` | Rig + mesh + takes `Armature\|idle`, `walk`, `run`, `hit_to_body_01`, `defeat_02`. Straight from Tripo, so root track **not** cleaned. | 5.5 MB |
| `warden_anims_extra_tripo_raw.fbx` | Same, with takes `Armature\|fire` and `defeat_03`. | 4.9 MB |
| `rifle.glb` | Rifle prop. 1.30 m long, origin at the pistol grip, muzzle +Z, up +Y. | 3.5 MB |
| `rifle.fbx` | Rifle prop. Tripo normalised it to 1.0 m long with the origin at the bbox centre (see below). | 4.0 MB |
| `art/` | `warden_unarmed_v1.png` (edited art), `multiview/` (Tripo's 4 views), `rifle_v1.png` (rifle art) | |
| `requests/` | Exact request bodies sent | |
| `tasks/` | Task records from Tripo (URLs replaced by file names) | |
| `previews/` | Headless three.js renders | |

All FBX files are FBX 7.4 binary with 4 embedded images.

## Model facts

- **Size:** 24,142 triangles and 14,434 vertices. One mesh, one PBR material.
- **Textures:** 2048x2048 base colour (JPEG), normal (PNG) and ORM (JPEG).
- **Height:** 1.90 m (`auto_size`). The feet are at y = 0 and the hips at 1.05 m.
- **Axes:** in glTF space +Y is up and the character faces **+X**. His right side is +Z.
- **Rest pose:** A-pose. The upper arms are about 30-33 degrees from vertical, measured from bone positions. Elbows are near straight and the legs stand straight.
  Tripo's rig check said `riggable: true`. Attempt 1 said false.
- **Skeleton:** 23 joints. `Root`, then `mixamorig:Hips, Spine, Spine1, Spine2, Neck, Head,
  Left/RightShoulder, Left/RightArm, Left/RightForeArm, Left/RightHand, Left/RightUpLeg,
  Left/RightLeg, Left/RightFoot, Left/RightToeBase`.
  The FBX files have the same 22 `mixamorig:` names. There are no finger bones.
  Every bone Unity Humanoid requires is present.

## Clips (inspected in `previews/v2_anim_*_{gamecam,side}.png`)

| Clip | Length | Verdict |
| --- | --- | --- |
| `idle` | 15.4 s | Good. A subtle weight shift with the arms at the sides. Not a weapon-hold idle. |
| `walk` | 2.4 s | Good after the root fix. |
| `run` | 1.27 s | Good after the root fix. The arms pump, so it has no weapon carry. |
| `hit_to_body_01` | 1.3 s | Usable as a short flinch. It starts and ends in a boxer guard turned about 45-90 degrees sideways, so it needs a blend in and out of idle. |
| `fire` | 1.5 s | **Standing**, not kneeling. Both arms are raised forward as if shouldering a rifle, with a small recoil. With the rifle prop at the right hand it reads as aiming (`previews/rifle_in_hand.png`). |
| `defeat_03` | 5.6 s | **Use this as the death.** He stumbles, falls forward and ends lying face down. |
| `defeat_02` | 8.5 s | A standing slump. **Not** a death; it could serve as a "dejected" idle. |

**Root-track problems in Tripo's raw output, fixed in `warden.glb` only:**
1. Walk and run climb every loop. The hips end 0.21 m (walk) and 0.42 m (run) higher
   than they start, then snap back. `tools/tripo/fix_loop_drift.py` removes the linear drift.
   After the fix, the planted foot stays at y 0.05-0.09 through the whole loop.
2. Every clip starts with the hips shifted horizontally away from the rest pose:
   run 1.06 m, defeat_03 0.47 m, idle 0.25 m, hit 0.17 m, defeat_02 0.12 m.
   This puts the mesh away from the GameObject origin. The same script moves the first
   key back to the rest position.

Tripo's retarget takes at most 5 clips per task, so the GLB was put together from
two tasks with `tools/tripo/merge_clips.py`.

The FBX files are **not** fixed. I uploaded the fixed GLB to Tripo's convert endpoint
(task d4a1bc77), but uploaded models lose their animations and get normalised to 1 m,
so I discarded that output. The fixed FBX would need Blender: import `warden.glb`, then export FBX.
Blender was not available here.

## Rifle prop

- Made by Tripo's image editor, which isolated the rifle from the original art (`art/rifle_v1.png`), then image-to-model.
- 3,740 triangles, one material with 2048x2048 textures.
- **`rifle.glb`:**
  - Wrapped in a parent node `rifle_grip_pivot` by `tools/tripo/wrap_transform.py` (scale 0.544, yaw 180°).
  - Length 1.30 m. The size matches the art, where the rifle is about 0.68 of body height.
  - The origin is at the pistol grip, about where the palm closes. The stock reaches 0.31 m behind it and the muzzle 0.99 m ahead.
  - The muzzle points +Z and the scope is on +Y.
- **`rifle.fbx`:**
  - Tripo dropped the wrapper. The rifle is 1.0 m long, the origin is at its centre and the muzzle points +Z.
  - To match the GLB, give the mesh scale 1.3 and local position (0, 0.13, 0.338) under the attach point.
- **Attaching:**
  - Parent it to `mixamorig:RightHand`. The hand bone origin is the wrist, so add a small forward offset.
  - Set the rotation by eye in Unity. The bone's axes are Tripo's, not checked in Unity.
  - The preview only places the rifle at the hand's world position facing the character's forward.

## Unity import (recommended, not yet verified in Unity)

- **`warden_rigged.fbx`:**
  - Model tab: Scale Factor 1 with Convert Units on. Check that he is 1.9 m tall.
  - Rig tab: Animation Type Humanoid, Avatar Definition "Create From This Model".
  - In Configure, the bones should auto-map. Use Pose > Enforce T-Pose, because the rest pose is an A-pose.
  - Materials: extract the embedded textures and materials.
- **Animation FBXs:**
  - Rig tab: Humanoid, "Copy From Other Avatar" pointing at the `warden_rigged` avatar.
  - Per clip, set Root Transform Rotation to Bake Into Pose.
  - Set Root Transform Position (XZ) to Bake Into Pose, based on Center of Mass. This should remove the constant horizontal offsets.
  - Set Position (Y) to Bake Into Pose, based on Feet.
  - Turn on Loop Time and Loop Pose for idle, walk, run and fire.
  - Loop Pose should remove the walk/run climb. If it does not, use `warden.glb` through Blender, or glTFast.
- **Facing:** the mesh faces +X in glTF terms.
  - The Humanoid avatar uses the body's own orientation, but the GameObject will look sideways.
  - Put the model under a parent and rotate the child about Y (±90°) until he faces +Z.
- **Weapon-ready animations:** none of Tripo's presets carries a rifle with two hands except `fire`.
  The skeleton uses Mixamo names and `warden_rigged.fbx` is a clean A-pose base, so the
  Mixamo rifle packs (rifle idle, walk, run, hit, death) are the obvious next source.

## Tripo tasks and credits

**Attempt 2** (balance 890 -> 680, 210 credits).

| Step | Endpoint | Task ID | Key parameters | Credits | Balance after |
| --- | --- | --- | --- | --- | --- |
| Edit image (unarmed) | `generation/image-to-image` | 60769452-35f3-469c-ad9c-c1eb1028c20a | chat_image_2.5_sunburst, medium, 1024x1536, transparent | 10 | 880 |
| Upload unarmed art | `files` | file_94cd8303-aa8d-45fb-9a4c-d56cdb144779 | | 0 | 880 |
| Image to multiview | `generation/image-to-multiview` | f5f84df5-a472-4aec-a5fc-6719489255cb | | 10 | 870 |
| Multiview to model | `generation/multiview-to-model` | 3226a01f-28b1-4005-81a2-60f5f15f5edc | v3.1-20260211, texture + pbr, standard, face_limit 25000, auto_size | 30 | 840 |
| Rifle image | `generation/image-to-image` | 159d7dd5-9e67-4e63-a809-bb34fd482a79 | chat_image_2.5_sunburst, medium, 1536x1024, transparent | 10 | 830 |
| Rig check | `animations/rig-check` | 9c3093d2-cc14-4982-8944-eb416f95a4b1 | returned riggable true, biped | 0 | 830 |
| Auto rig | `animations/rig` | e73d30f3-ec0e-466e-b3bf-553da60a7ac9 | v1.0-20240301, biped, mixamo, glb | 25 | 805 |
| Rifle model | `generation/image-to-model` | b316f88e-190b-4304-a41b-0172cf8a2a35 | v3.1, texture + pbr, face_limit 4000, auto_size | 30 | 775 |
| Retarget (5 clips) | `animations/retarget` | 5ba7eee8-445b-4a5e-ab1c-7b681ae2bc7c | idle, walk, run, hit_to_body_01, defeat_02; glb, bake, in place | 50 | 725 |
| Retarget (2 clips) | `animations/retarget` | 9e28f931-a359-4105-abcf-ec6c1c19ca5f | fire, defeat_03; same options | 20 | 705 |
| Convert rig to FBX | `models/convert` | 3535780e-dd41-43e1-af3d-de54f59f07e9 | FBX -> `warden_rigged.fbx` | 5 | 700 |
| Convert fixed GLB (upload) | `models/convert` | d4a1bc77-73ef-4911-b438-5167550dae5e | FBX; lost animations and scale, discarded | 5 | 695 |
| Convert rifle GLB (upload) | `models/convert` | ac2915d8-a7e9-4237-9ecc-698d72e92e82 | FBX -> `rifle.fbx` (normalised to 1 m) | 5 | 690 |
| Convert retarget 1 | `models/convert` | ebf003af-a7ee-4821-aece-1d568e0baae2 | FBX -> `warden_anims_tripo_raw.fbx` | 5 | 685 |
| Convert retarget 2 | `models/convert` | 6127e549-af77-4e80-a560-6528c41217ab | FBX -> `warden_anims_extra_tripo_raw.fbx` | 5 | 680 |

A sixth clip in one retarget task was rejected (`animations size must be <= 5`) at no cost.

**Attempt 1** (balance 1000 -> 890, 110 credits). Details are in `attempt1/README.md`.

**Whole task: 320 credits spent. Balance 680.**
