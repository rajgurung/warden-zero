# Tripo log: Milestone 2 (Warden traversal clips)

Made on 2026-10-11 with the Tripo v3 API (`openapi.tripo3d.ai/v3`, international) and
`tools/tripo` (committed on this branch).

**Credits for clips: 135** (3 retargets, 1 convert). The whole M2 art job ran from 2215 to 1720 (495 spent). The prop spend is in
`Assets/Art/Campaign/TRIPO_LOG.md`.

Accept/reject reasons, trims and root settings are in `unity/ArtSource/M2Traversal/README.md`.
Request bodies are in `ArtSource/M2Traversal/requests/` and task records in `ArtSource/M2Traversal/tasks/`.
The convert body was sent inline: `{"input": "<task id>", "format": "FBX"}`.

All retargets ran on the existing Warden rig task `e73d30f3-ec0e-466e-b3bf-553da60a7ac9`
(rig v1.0-20240301, biped, spec mixamo). Options: glb, bake_animation, export_with_geometry,
animate_in_place. The preset list was re-checked on the docs first and is unchanged.

| # | Step | Endpoint | Task ID | Settings | Credits | Balance after |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Retarget test batch 1 | `animations/retarget` | 92ea0d22-2a97-4952-9b35-4b5897510b5b | run_upstairs, flip, lift_heavy, press-up, basketball_shot | 50 | 2165 |
| 2 | Retarget test batch 2 | `animations/retarget` | eea104cf-c53f-47eb-a48a-b0872f385bfb | volleyball, jump_rope_01, football_save, flee_01, warm_up | 50 | 2115 |
| 3 | Retarget, accepted only | `animations/retarget` | 1275e299-ddfd-4fd5-8a99-a00499967624 | basketball_shot, run_upstairs, jump_rope_01 | 30 | 1995 |
| 4 | Accepted FBX | `models/convert` | 9b39f1e1-2f31-445d-b134-183f1634a0fa | FBX -> `Anims/warden_anims_m2_traversal.fbx` | 5 | 1950 |

The balance column follows the order of all M2 tasks (props included).
Batches 1 and 2 were judged from their GLBs and never converted, so no rejected take is in Assets.

## Import verdicts (iteration 6, judged in Unity)

Side stills through the Warden's Animator, against the procedural `TraversalPose`. The clip
table is `Warden3DImport.Clips`; the base-layer states are built in `SceneBuilder.Warden3D`.

| Clip | Source | Root Y | Verdict | Use / reason |
| --- | --- | --- | --- | --- |
| `jump_takeoff` | basketball_shot 1.45-2.05 s | Feet | Accepted | `JumpUp`: crouch and spring, held in the air. Feet baking keeps the takeoff on the ground (lowest foot within 0.03 m of the idle's), so the README's -0.2 m offset was not needed. Traversal moves the height. |
| `jump_grab` | basketball_shot 1.45-2.2 s | Feet | Accepted | `JumpGrab`: a jump at a high ledge, both arms reaching up (the rifle is slung for it); `TraversalPose` takes the hands to the edge. |
| `climb_step` | run_upstairs 0-0.75 s, loop | NotBaked | Accepted | `ClimbUp` (mantle and climb up): high-knee steps under the hands on the edge. Plays in place. |
| `climb_ladder` | Step A: M1 `climb`, loop | NotBaked | Accepted | `WallClimb`: knees and hands up the vine face, plays in place at the climb rate (`TravSpeed`); hands still go to the face through IK. Better than the procedural climb. |
| `jump_hop` | jump_rope_01 2.9-3.6 s | - | Rejected | A second takeoff; `jump_takeoff` covers jumps. |
| `jump_m1` | Step A: M1 `jump` | - | Rejected | Small bounces on the spot, no takeoff. |
| `step_off` | Step A: M1 `jump_down` 0-2.2 s | - | Rejected | A crouch and a leap that ends crouched, not a step off a ledge. |

Vault, hang, lower and drop have no clip and stay procedural (`TraversalPose`), as the plan
expects. `land` (M1) plays for drops over 2 m.
