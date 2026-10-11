# Tripo log: Milestone 1 polish (Warden clips and HALO gear)

Made on 2026-10-10 with the Tripo v3 API (`openapi.tripo3d.ai/v3`, international) and the
`tools/tripo` helper from the `warden-3d` branch (not committed here).

**Credits: 485 -> 215, 270 spent** (cap for this job: 300).

What each asset is for, the attach values and the rejected clips are in
`unity/ArtSource/M1Polish/README.md`. Request bodies are in `ArtSource/M1Polish/requests/`
and task records in `ArtSource/M1Polish/tasks/` (expiring URLs replaced by file names,
storage keys stripped). Convert bodies were sent inline: `{"input": "<task id>", "format": "FBX"}`.

All retargets ran on the existing Warden rig task `e73d30f3-ec0e-466e-b3bf-553da60a7ac9`
(rig v1.0-20240301, biped, spec mixamo), so the clips share the bone names of
`warden_rigged.fbx` and use its Humanoid avatar. Retarget options: glb, bake_animation,
export_with_geometry, animate_in_place. Every FBX was converted from a task id.

The gear art was made with image-to-image from `docs/warden-hero.png`
(uploaded as `file_8ea7b25d-…`) used as a style and colour reference.

| # | Step | Endpoint | Task ID | Settings | Credits | Balance after |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Retarget batch 1 | `animations/retarget` | a242d79f-07bb-4021-a4df-3ec8c20812e5 | standing_relax, wait, fall, dive, jump_down | 50 | 435 |
| 2 | Retarget batch 2 | `animations/retarget` | 211350b5-7d6e-44d7-9528-14f567ef7c43 | jump, swim, surf, climb, look_around | 50 | 385 |
| 3 | Pack art | `generation/image-to-image` | 6244c25a-92ed-4129-840e-57f39f05798a | chat_image_2.5_sunburst, medium, 1024x1024, transparent | 10 | 375 |
| 4 | Helmet art | `generation/image-to-image` | db7933bd-4a0c-4db7-9195-da9910cd4ec5 | same | 10 | 365 |
| 5 | Pack model | `generation/image-to-model` | 64c1ca82-05e3-4432-b68d-87382f8151cf | v3.1-20260211, texture + pbr, standard, face_limit 5000, auto_size | 30 | 335 |
| 6 | Batch 1 FBX | `models/convert` | 18e7480d-f697-4bc3-a608-98d1ce12f24b | FBX -> `Anims/warden_anims_m1_idle_land.fbx` | 5 | 330 |
| 7 | Batch 2 FBX | `models/convert` | 72f1d1ed-31cd-4109-a949-1263046077e6 | FBX -> `Anims/warden_anims_m1_fall_look.fbx` | 5 | 325 |
| 8 | Helmet model | `generation/image-to-model` | e5aa71c0-7f7b-4a2e-ba81-fe0008febc1e | v3.1, texture + pbr, face_limit 4000, auto_size | 30 | 295 |
| 9 | Pack FBX | `models/convert` | 8484ea16-d898-483c-97fd-c4a458c377c4 | FBX -> `Campaign/Gear/halo_pack.fbx` | 5 | 290 |
| 10 | Helmet FBX | `models/convert` | f7e6865b-8ad0-41fd-b93e-1d80417b3134 | FBX -> `Campaign/Gear/halo_helmet.fbx` | 5 | 285 |
| 11 | Altimeter art | `generation/image-to-image` | e635dc88-d1eb-4c25-a90d-b844e008df3a | same as the pack art | 10 | 275 |
| 12 | Retarget batch 3 | `animations/retarget` | d49158b3-1ac0-4e04-828d-b6984c7ba10d | cheer, victory_celebration | 20 | 255 |
| 13 | Altimeter model | `generation/image-to-model` | 79842b45-d6f7-4a97-ba11-fb9f81959d97 | v3.1, texture + pbr, face_limit 1500, auto_size | 30 | 225 |
| 14 | Batch 3 FBX | `models/convert` | 2d4201e8-acb6-48f0-bc1f-826e9c4c5e9f | FBX -> `Anims/warden_anims_m1_canopy.fbx` | 5 | 220 |
| 15 | Altimeter FBX | `models/convert` | 6aca99eb-88d8-4c7e-9d4b-5113cfca51bc | FBX -> `Campaign/Gear/altimeter.fbx` | 5 | 215 |

One helmet-art request was refused with HTTP 429 (too many tasks at once) and cost nothing.
It was re-sent as step 4. File uploads (hero art, the three concept images) are free.
