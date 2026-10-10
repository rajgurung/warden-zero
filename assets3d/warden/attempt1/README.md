# Warden 3D model (Tripo, attempt 1: image with rifle)

Generated on 2026-10-10 from `docs/warden-hero.png` (front view, rifle held across
the body) with the Tripo v3 API and `tools/tripo/tripo.mjs`.

The binaries listed below were removed from the tree to keep the folder small.
Get them from commit d4fdf0d (`git show d4fdf0d:assets3d/warden/warden_animated.glb > x.glb`).

Verdict: the static model is good. The rig works. The animations are not usable
as-is, mainly because the rifle is part of the body mesh and bends with the arms.

## Files

| File | What | Size |
| --- | --- | --- |
| `warden_static.glb` | Textured static mesh | 4.4 MB |
| `warden_rigged.glb` | Same mesh, skinned to a 23-bone Mixamo-named skeleton, no clips | 4.5 MB |
| `warden_animated.glb` | Rigged mesh + 5 baked clips: `idle`, `run`, `shoot`, `hurt`, `fall` | 5.1 MB |
| `warden_animated.fbx` | FBX 7.4 binary of the animated GLB, textures embedded, takes `Armature\|idle` etc. | 6.3 MB |
| `requests/` | Exact request bodies sent | |
| `tasks/` | Task records returned by Tripo (URLs replaced by file names) | |
| `previews/` | Renders from headless Chromium + three.js, plus Tripo's own renders | |

## Model facts

- Triangles: 23,843. Vertices: 14,697 (GLB). One mesh, one PBR material.
- Textures: 2048x2048 base colour (JPEG), normal (PNG), ORM (JPEG).
- Height: 1.90 units (metres, `auto_size: true`). Width 0.86, depth 1.27 including the rifle.
- glTF space: +Y up, character faces **+X**. Rigged files have feet at y = 0.
  Unity's glTF/FBX importers will need a -90 degree Y rotation (or a parent) so he faces +Z.
- Rest pose is the art pose (arms bent around the rifle), not a T-pose.
  Unity Humanoid will need "Enforce T-Pose" in the avatar configuration.

## Skeleton

23 joints: `Root` then `mixamorig:Hips, Spine, Spine1, Spine2, Neck, Head,
Left/RightShoulder, Left/RightArm, Left/RightForeArm, Left/RightHand,
Left/RightUpLeg, Left/RightLeg, Left/RightFoot, Left/RightToeBase`.
Same names in the FBX. No finger bones. Every bone Unity Humanoid requires is present
(Hips, Spine, Head, upper/lower arms and legs, hands, feet), so auto-mapping
should work. Not yet verified inside Unity.

## Animation quality (inspected in previews/anim_*.png)

- `idle` (15.4 s): arms hang at the sides; the rifle floats at hip height. No breakage,
  but it is not a weapon-ready idle.
- `run` (1.27 s, in place): the rifle visibly bends into a curve. The cycle drifts upward:
  the planted foot sits at y 0.04, then 0.19, then 0.42 within one loop before snapping back,
  so the run will bob/pop. The hips are also offset about 0.94 m forward of the origin.
- `shoot` (9.1 s): a kneeling shot. The rifle crumples into the hands and mostly disappears.
- `hurt` (13.9 s): sits/lies on the ground for the whole clip. Not a hit reaction.
- `fall` (3.0 s): falls backward and ends curled on the ground. Usable as a rough death
  stand-in; the rifle stretches mid-fall.

## Tripo tasks and credits

Balance before: 1000. Balance after: 890. Total spent: 110.

| Step | Endpoint | Task ID | Key parameters | Credits |
| --- | --- | --- | --- | --- |
| Upload image | `POST /v3/files` | file_2e809ca9-37f9-4515-b0f7-babe0ffa2f01 | | 0 |
| Image to model | `generation/image-to-model` | 04ddfe84-74d7-49dd-930e-1d716416cf93 | model v3.1-20260211, texture + pbr, texture_quality standard, face_limit 25000, auto_size | 30 |
| Rig check | `animations/rig-check` | cf89696d-a6c0-4abf-a2b0-079377b25f55 | | 0 (returned `riggable: false`, `rig_type: biped`) |
| Auto rig | `animations/rig` | f2015678-7b9c-49f9-8e7f-6cc535536358 | model v1.0-20240301, biped, spec mixamo, glb | 25 |
| Retarget | `animations/retarget` | 9a970772-cd7a-440c-87b7-5b915c687c7c | preset:biped:idle/run/shoot/hurt/fall, glb, bake, in place | 50 |
| Convert | `models/convert` | a3c81349-8f6c-4baa-90f1-eaf7161fc3f8 | input = retarget task, format FBX, defaults | 5 |
