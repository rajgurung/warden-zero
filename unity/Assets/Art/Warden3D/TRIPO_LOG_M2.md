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
