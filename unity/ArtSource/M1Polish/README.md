# M1 polish: Warden clips and HALO gear (Tripo)

Feedback from the Milestone 1 playtest: during the jump the arms look "wavy" (the freefall
pose was procedural, with flutter), the hands look odd on landing, and at rest he kneels
instead of standing relaxed. These assets give the gameplay code real clips and HALO gear.
All of it came from Tripo. Credits and task ids: `Assets/Art/Warden3D/TRIPO_LOG_M1_POLISH.md`.

`contact_sheet.png` shows every accepted clip (key frames) and every gear piece off and on
the Warden. `previews/rejected_clips.png` shows the rejected clips.
`previews/fit_*_threejs.png` are the fitting renders. `art/` holds the concept images,
`requests/` the request bodies and `tasks/` the task records.

## Clips

The clips are retargeted onto the Warden's own rig task (`e73d30f3-…`, spec mixamo), so they
use the `warden_rigged.fbx` Humanoid avatar. `Warden3DImport.M1Clips` sets them up:
- Humanoid, with the avatar copied from `warden_rigged.fbx`.
- Root rotation and position baked into the pose, like the existing clips.
- Each clip renamed and trimmed. Takes not listed are rejected and not imported.

| Unity clip | File (`Assets/Art/Warden3D/Anims/`) | Tripo preset | Length | Loop | Use |
| --- | --- | --- | --- | --- | --- |
| `idle_relaxed` | `warden_anims_m1_idle_land.fbx` | `standing_relax` | 17.6 s | yes | **Main rest idle.** Standing, arms loose at the sides, slow weight shift. Replaces the kneel. |
| `idle_hands_on_hips` | same | `wait` | 6.0 s | yes | Variant: stands with hands on the belt/hips. |
| `land` | same | `jump_down`, trimmed 2.2 s to end | 1.47 s | no | **Landing.** Starts at touchdown in a deep crouch, hands down, then stands up. Blend into an idle at the end. |
| `idle_look_around` | `warden_anims_m1_fall_look.fbx` | `look_around` | 15.5 s | yes | Variant: standing, turns his head and shoulders to scan. |
| `fall_loop` | same | `swim` | 5.7 s | yes | **Freefall.** Prone and horizontal, belly down, arms sweeping wide and back. A slow stroke (5.7 s) instead of the flutter. Play it at 0.5-0.7x speed for a calmer arch. |
| `canopy_hold` | `warden_anims_m1_canopy.fbx` | `victory_celebration`, trimmed 1.3-3.8 s | 2.5 s | yes | **Under canopy.** Both arms straight up with closed fists (hands on the toggles), legs hanging straight. Hands are at about 2.3 m, so the riser lines can run to `LeftHand`/`RightHand`. |

Root settings:
- Ground clips (idles and `land`) have height based on the feet.
- Air clips (`fall_loop`, `canopy_hold`) have height based on the original hips, which stay at about 1.05 m.

These were checked through an Animator graph in Unity (applyRootMotion off):
- Feet stay at y 0.05-0.10 in the idles, and in `land` from touchdown to standing.
- In `land` the hips dip from 0.69 to 0.49 m and rise to 0.99 m.
- `fall_loop` keeps the hips at 1.05 m with no drift.
- The looping clips match at start and end.

**Hands:** the rig has no finger bones, so no clip can change the hand shape. The fists are in
the mesh. In `land` the arms hang low and forward through the crouch, then relax at the sides.
Whether that cures the odd hands is unverified in game. Changing the hand shape itself needs
a mesh or rig change.

### Rejected

| Preset | Why |
| --- | --- |
| `fall` | A stagger and collapse to the floor: a knockdown or death, not a freefall. |
| `dive` | A run-up and forward handspring flip. It ends 0.4 m above the floor and does not work as a parachute landing roll. |
| `jump` | Repeated hops on the spot. |
| `climb` | A ladder climb. The body rises 2.4 m over the clip. |
| `surf` | A crouched, side-on board stance. |
| `cheer` | One arm waving while the body turns. Not a steady hold. |

Not tried (budget): `flip`, `jump_rope_*`, `flee_*`, `swagger`, `run_upstairs`. No preset carries a rifle.

### All Tripo biped presets (rig v1.0-20240301, from developers.tripo3d.ai/en/docs/animations-retarget)

afraid, agree, angry_01, angry_02, angry_03, basketball_shot, bow, box_01, box_02, box_03,
cast_a_spell, cheer, chop, clap, climb, complain_01, complain_02, cross_body_crunch,
crossover_dribble, cry, dance_01 to dance_06, defeat_02, defeat_03, depressed, dig, dive,
dribble, fall, fire, flee_01, flee_02, flip, fold_arms, football_catch, football_save,
football_pass, freaky, frightened, front_kick_01, front_kick_02, frustrated_01,
frustrated_02, golf, greet_01 to greet_04, heart_pose, hit_to_body_01, hit_to_body_02,
hit_to_head, hit_to_side, hit_to_stomach, hug, hurt, idle, jump_down, jump, jump_rope_01,
jump_rope_02, laugh_01, laugh_02, lift_heavy, look_around, make_a_call_01, make_a_call_02,
pitch_baseball, play_mobile_game, play_video_game, press-up, run_upstairs, run, scared_01,
scared_02, scratch, shoot, shovel, sing_01 to sing_04, sit, slash, sob, standing_relax,
surf, swagger, swim, turn, victory_celebration, volleyball, wait, walk, warm_up,
wave_goodbye_01, wave_goodbye_02 (all as `preset:biped:<name>`).
The newer rig v2.5 has only 11 biped presets (idle, walk, run, dive, climb, jump, slash,
shoot, hurt, fall, turn) and would need a new rig.

## HALO gear

All three are in `Assets/Art/Campaign/Gear/`. They follow the same import path as the campaign props (`CampaignImport.Gear`):
- Plain mesh, no animation, no materials.
- Textures extracted to `Gear/Textures/<name>/` (base colour, normal, metallic, roughness; 512 px via `ArtImportSettings`).
- Unlike the older props, these FBXs keep Tripo's real size in metres.
- The origin is at the bounding-box centre.
- The file root has a 270-degree X rotation (Z-up FBX), the same as the other props.

| File | What | Tris | Size in file (m) | Front of the prop |
| --- | --- | --- | --- | --- |
| `halo_pack.fbx` | Parachute container: olive hard shell, cyan stripes, webbing harness, pilot-chute pouch at the bottom | 4,804 | 0.42 wide, 0.55 tall, 0.36 deep | Outer shell faces Tripo +X |
| `halo_helmet.fbx` | Jump helmet with cyan-rimmed goggles, oxygen mask and side hose. Fits over his own helmet | 3,727 | 0.33 x 0.35 x 0.29 | Visor faces Tripo +X |
| `altimeter.fbx` | Wrist altimeter: olive housing, glowing cyan dial, short strap | 1,380 | 0.14 x 0.10 x 0.22 | Dial faces up (+Y); strap along Z |

### Attaching (verified in Unity, see the contact sheet)

For each piece:
1. Make an empty holder as a child of the bone.
2. Set the holder's local position, rotation and scale from the table.
3. Instantiate the FBX under the holder and leave its own root transform alone.

The values are in the bone's local space, in metres. They are for `warden_rigged.fbx` at
scale 1, and they still hold under the game's 1.4x model scale because the holder inherits it.

| Gear | Bone (`HumanBodyBones`) | Local position | Local rotation | Scale |
| --- | --- | --- | --- | --- |
| `halo_pack` | `mixamorig:Spine2` (`UpperChest`) | (0, -0.10, -0.25) | Euler (0, 270, 0) | 1.55 |
| `halo_helmet` | `mixamorig:Head` (`Head`) | (0, 0.13, 0.03) | Euler (0, 90, 0) | 1.25 |
| `altimeter` | `mixamorig:LeftForeArm` (`LeftLowerArm`) | (0, 0.22, -0.075) | `new Quaternion(-0.5f, -0.5f, 0.5f, 0.5f)` | 1.1 |

Notes:
- The HALO helmet encloses his own helmet, so nothing needs hiding. His visor glow is
  replaced by the goggles' cyan rim.
- The pack's shoulder straps rest on his shoulder plates. With `canopy_hold` the arms go up
  through the straps' space, which looks fine at game-camera distance.
- The altimeter sits on the outside of the left forearm guard, dial facing out, near the wrist.
- Equivalent three.js (glTF) values, for web previews: same positions with X negated
  (all zero here). The rotations are the mirrored ones: pack Y +90, helmet Y -90, altimeter Euler XYZ (-90, 90, 0).
