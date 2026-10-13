# Campaign props from Tripo (milestone 1)

Made on 2026-10-10 with the Tripo v3 API (`openapi.tripo3d.ai/v3`) and the `tools/tripo`
helper from the `warden-3d` branch (not committed here).

**Credits: 675 -> 485, 190 spent** (cap for the milestone: 250).

Sources, kept outside `Assets` so Unity does not import them (`unity/ArtSource/Campaign/`):
- `art/`: the reference art Tripo drew (chopper, rotor, canopy, beacon) and the chopper's four multiview images.
- `previews/`: Tripo's own renders of each model.
- `requests/`: the exact request bodies. `tasks/`: task records, with the expiring URLs replaced by file names.

Style: every reference picture was made with image-to-image from `docs/warden-hero.png`
(uploaded as `file_7d2c9a90-…`), asking for the same olive armour, gunmetal and cyan
glow, so the props match the Warden.

## Files in Unity

| File | What | Faces | Notes |
| --- | --- | --- | --- |
| `Chopper/chopper.fbx` | Transport helicopter body, no rotor blades | 19,222 | Built from a three-quarter view, so the fuselage lies diagonally in the file. SceneBuilder finds its long axis (PCA) and turns it nose +Z, 17 m long. |
| `Rotor/rotor.fbx` | Four-blade rotor with hub | 2,804 | Used twice: main rotor (7.6 m radius) and tail rotor (1.5 m), spun in code. |
| `Canopy/canopy.fbx` | Ram-air canopy, no lines | 4,864 | Scaled to a 9 m span; lines are LineRenderers to his shoulders. |
| `Beacon/beacon.fbx` | Checkpoint beacon: armoured base, tripod, mast, flag, lamp | 5,805 | 3.4 m tall. |

All four are FBX converted from the task IDs (not re-uploaded files), Z-up with a
270-degree root rotation that SceneBuilder keeps. Textures were extracted to each
`Textures/` folder (base colour, normal, metallic, roughness). The chopper keeps 1024 px,
the rest 512 px.

## Tasks

| Step | Endpoint | Task ID | Settings | Credits | Balance after |
| --- | --- | --- | --- | --- | --- |
| Chopper art (no rotor blades) | `generation/image-to-image` | cd92981d-be47-408f-9c0a-128fdce9ba83 | chat_image_2.5_sunburst, medium, 1536x1024, transparent | 10 | 665 |
| Chopper multiview | `generation/image-to-multiview` | 797fca47-90c6-45af-803f-96ebf2691812 | from the chopper art | 10 | 655 |
| Rotor art | `generation/image-to-image` | 96d9ee0f-6619-4580-9d59-3413dd0dd7ef | from the chopper art, 1536x1024 | 10 | 645 |
| Canopy art | `generation/image-to-image` | c1f96150-e2e2-4377-9150-c6a6c7294200 | from the Warden art, 1536x1024 | 10 | 635 |
| Chopper model | `generation/multiview-to-model` | 415fed22-6c29-4337-bc5b-d86fae08e9c6 | v3.1-20260211, texture + pbr, standard, face_limit 20000, auto_size | 30 | 605 |
| Beacon art | `generation/image-to-image` | 77589eab-9b61-4811-9a10-b099588bf73e | from the Warden art, 1024x1536 | 10 | 595 |
| Rotor model | `generation/image-to-model` | 1ba75997-44c6-469b-bebe-e7204e87b860 | v3.1, texture + pbr, face_limit 3000, auto_size | 30 | 565 |
| Canopy model | `generation/image-to-model` | 240d7424-798f-4b98-9ce7-fcd156e8fd6d | v3.1, texture + pbr, face_limit 5000, auto_size | 30 | 535 |
| Beacon model | `generation/image-to-model` | ba448a34-89bb-4f37-b208-e2448f09150e | v3.1, texture + pbr, face_limit 6000, auto_size | 30 | 505 |
| Chopper FBX | `models/convert` | b76537fa-6e5a-44a6-8781-9371dca6318d | format FBX | 5 | 500 |
| Rotor FBX | `models/convert` | 2bad3148-be44-4638-b679-ffe158dc2ee1 | format FBX | 5 | 495 |
| Canopy FBX | `models/convert` | 81a7deb8-1ecd-4d95-b0ee-313c0f257a3d | format FBX | 5 | 490 |
| Beacon FBX | `models/convert` | af9476ac-0d88-49f3-9601-ebcaa3d93e1b | format FBX | 5 | 485 |

One canopy-art request was refused with HTTP 429 (too many tasks at once) and cost nothing;
it was re-sent when the queue cleared. The optional props (sandbags, crate, ruin) were
skipped to stay well inside the cap.

# Traversal props from Tripo (milestone 2)

Made on 2026-10-11 with the Tripo v3 API and `tools/tripo` (now committed). Credits were topped
up to about 2,200 for M2 (`unity/M2_TASKS.md` section 8).

**Credits: 2215 -> 1720 for the whole M2 art job, 495 spent.** Props cost 360 and clips 135
(clips: `Assets/Art/Warden3D/TRIPO_LOG_M2.md`).

Files: `Traversal/*.fbx`, raw FBX with embedded 2048 px PBR textures. Sources, scales, measured
bounds and the accept/reject table: `unity/ArtSource/M2Traversal/README.md`. Concept art was
made with image-to-image from `docs/warden-hero.png` (uploaded as `file_0c4d6ec4-…`) as a
colour reference, with prompts asking for realistic PBR to match the CC0 jungle textures.
Each concept was uploaded (free) and fed to image-to-model. Every FBX was converted from the task id.

| Step | Endpoint | Task ID | Settings | Credits | Balance after |
| --- | --- | --- | --- | --- | --- |
| Log art | `generation/image-to-image` | 4fa112ee-35f7-40e7-a055-aa3d88500381 | chat_image_2.5_sunburst, medium, 1536x1024, transparent | 10 | 2105 |
| Wall art | `generation/image-to-image` | 2078f0aa-ffef-41ec-995a-a67e3a306281 | same | 10 | 2095 |
| Crate art | `generation/image-to-image` | 54ad3a68-b332-4aec-9805-e99153dbfe0d | 1024x1024 | 10 | 2085 |
| Log model | `generation/image-to-model` | b875498b-48bc-4d55-8524-96ee344832cb | v3.1-20260211, texture + pbr, standard, face_limit 5000, auto_size | 30 | 2055 |
| Wall model | `generation/image-to-model` | 93474def-7410-4a3e-9185-cb120c997998 | face_limit 8000 | 30 | 2025 |
| Crate B art | `generation/image-to-image` | b4e63f7e-bfc0-4d19-aca4-81c9b4e4a4f7 | 1024x1024 | 10 | 1985 |
| Crate model | `generation/image-to-model` | b1d6be18-f750-4749-bd3c-628b55424fcf | face_limit 2500 | 30 | 1955 |
| Ledge art | `generation/image-to-image` | 0855e9c0-f4d0-4a49-a4cf-088567439024 | 1536x1024 | 10 | 1940 |
| Crate B model | `generation/image-to-model` | a393278b-3089-4c81-a717-75af41e9f405 | face_limit 2500 | 30 | 1910 |
| Log FBX | `models/convert` | 0a759d5b-896e-44bf-a17f-576c8c2084f4 | FBX -> `Traversal/fallen_log.fbx` | 5 | 1905 |
| Wall FBX | `models/convert` | 6ec56303-5b52-4d21-a7ed-76bc36691718 | FBX -> `Traversal/ruin_wall.fbx` | 5 | 1900 |
| Rubble art | `generation/image-to-image` | a956f9b1-e6af-41b3-af49-1e4019adaf48 | 1536x1024 | 10 | 1890 |
| Ledge model | `generation/image-to-model` | 693030ab-9693-4aa6-bd35-898990d15c03 | face_limit 6000 | 30 | 1860 |
| Crate FBX | `models/convert` | 0330519a-bfc0-45c5-a5d5-84d987b7b58a | FBX -> `Traversal/supply_crate.fbx` | 5 | 1855 |
| Crate B FBX | `models/convert` | 89e9c783-2c7f-4b40-bd3d-73fa031644ef | FBX -> `Traversal/supply_crate_b.fbx` | 5 | 1850 |
| Outcrop art | `generation/image-to-image` | 0fe6706e-e46e-4b6e-a5da-ce76a221f1ea | 1536x1024 | 10 | 1840 |
| Rubble model | `generation/image-to-model` | e130c4f6-1f99-4df2-9265-954946dc738a | face_limit 6000 | 30 | 1810 |
| Outcrop model | `generation/image-to-model` | 5e11c98c-8cab-4c79-8702-634f427e5cf6 | face_limit 6000 | 30 | 1780 |
| Ledge FBX | `models/convert` | 86f40d7b-6d9d-4215-883c-6957a9c7772c | FBX -> `Traversal/rock_ledge.fbx` | 5 | 1775 |
| Tall rubble art (re-roll) | `generation/image-to-image` | 199ec280-2376-4285-9f4d-73ecb44e5d47 | 1536x1024 | 10 | 1765 |
| Rubble FBX | `models/convert` | c247d3c7-ae71-4a2f-ab9b-ca5944e4426f | FBX -> `Traversal/gate_rubble.fbx` | 5 | 1760 |
| Outcrop FBX | `models/convert` | 464b5f9d-a063-45ba-9d07-7b475b526fe9 | FBX -> `Traversal/rock_outcrop.fbx` | 5 | 1755 |
| Tall rubble model | `generation/image-to-model` | 806c21b4-8452-4c2c-897d-4ef792d6adfb | face_limit 6000 | 30 | 1725 |
| Tall rubble FBX | `models/convert` | c163f972-d745-4534-af80-2c0d39ec3b90 | FBX -> `Traversal/gate_rubble_tall.fbx` | 5 | 1720 |

Balances follow the order of all M2 tasks, so the clip tasks fill the gaps.
One wall-art request was refused with HTTP 429 (too many image tasks at once). It cost nothing and was re-sent.
The tall rubble was a re-roll for readability: the first rubble's ends slope to the ground,
so it does not look impassable beside the wall. Both are kept.

### Import into the course (iteration 8)

`SceneBuilder.Jungle.BuildCourse` fits each prop to its `CourseLayout` block rather than using
one fixed scale: the footprint matches the block, the prop's walkable top (log 0.82 of its
height, crates 0.97, wall 0.99, ledge 0.975, tall rubble 0.78, heap 0.6, crag 0.8) sits at the
block's top, and its base just under the lowest ground. The gate is two 5 m wall pieces 1.5 m
thick (the plain one turned so its vines face away), tall rubble against the west end with
the low heap at the far end, and three crags east across the stream. The crate stack is two
`supply_crate`, the first step is `supply_crate_b`, and `rock_ledge` serves the shelf, the ford
blocks, the ledge and the terrace. Textures: colour 512, normals and the generated metallic-smoothness and emission maps
128, and only the crates keep an emission map; Low halves all of them. Meshes use Medium
compression. The jungle bundle grew 2.19 MB. With the wall's colour at 1024 and normals at 256
(+3.55 MB) the jungle's wasm heap stepped from 286 to 344 MB; at these sizes it stays at 286 MB.

# Vehicles from Tripo (milestone 3: jeep and gun)

Made on 2026-10-12 with the Tripo v3 API and `tools/tripo`. Targets and the outpost come later.

**Credits: 1720 -> 1330 for the M3 art so far, 390 spent.** Jeep and gun cost 335 (body 155; wheel,
steering, yoke and HMG 45 each) and clips 55 (`Assets/Art/Warden3D/TRIPO_LOG_M3.md`). Plan cap for all M3 art: 945.

Files: `Vehicles/*.fbx`, raw FBX with embedded 2048 px PBR textures. Sources, measured pivots, scales and the
accept/reject table: `unity/ArtSource/M3Vehicles/README.md`. The jeep concept used `docs/warden-hero.png`
(uploaded as `file_60faf8e6-…`) as a colour reference; the parts used the jeep concept task. Models took the
concept task id as input, so the only uploads were the reference and the side view for the multiview.
Every FBX was converted from its task id (body `{"input": "<task id>", "format": "FBX"}`, sent inline).

| Step | Endpoint | Task ID | Settings | Credits | Balance after |
| --- | --- | --- | --- | --- | --- |
| Jeep art | `generation/image-to-image` | 06455900-32d7-40fd-b8a7-8d19c002e504 | chat_image_2.5_sunburst, medium, 1536x1024, transparent | 10 | 1710 |
| Jeep art, stands removed | `generation/image-to-image` | 6545be3d-e0ee-40f8-a992-5f3b5e78ca1e | edit of the jeep art | 10 | 1650 |
| Wheel art | `generation/image-to-image` | 6e49cf60-18c0-4b61-a48d-d3d5ecc33739 | 1024x1024, jeep art as reference | 10 | 1640 |
| Wheel model | `generation/image-to-model` | 92ed7745-a480-4155-8169-c7f6d7548e7d | v3.1-20260211, texture + pbr, standard, face_limit 3000, auto_size | 30 | 1610 |
| Jeep side view | `generation/image-to-image` | 3d645e01-ad8b-4e41-9910-f9ee444d9783 | for the multiview | 10 | 1600 |
| Steering art | `generation/image-to-image` | 62d7768a-32ac-4d0b-963d-f4f5be867a65 | 1024x1024 | 10 | 1585 |
| Jeep multiview (rejected) | `generation/image-to-multiview` | 6cdf69b5-af14-4f23-aa59-8127c9824127 | from the side view (uploaded `file_009b2642-…`) | 10 | 1575 |
| Steering model | `generation/image-to-model` | 3349e39d-5bc4-4859-b84f-2e1571ae0aca | face_limit 1500 | 30 | 1545 |
| Wheel FBX | `models/convert` | be120268-9167-4e64-8063-06f1132e776c | FBX -> `Vehicles/jeep_wheel.fbx` | 5 | 1540 |
| Jeep model, roll 1 (rejected) | `generation/image-to-model` | d49634d9-7bdb-4920-88fa-9f63ca403f06 | face_limit 20000, from the stands-removed art | 30 | 1510 |
| Yoke art | `generation/image-to-image` | 326bf1f0-3daa-4bad-b844-5dd3c9c73108 | 1536x1024 | 10 | 1500 |
| Steering FBX | `models/convert` | 4a83ba13-3b04-4017-8aa8-8c2a19948a2a | FBX -> `Vehicles/jeep_steering.fbx` | 5 | 1495 |
| HMG art | `generation/image-to-image` | f2dcc3b0-01a4-405f-b8b6-9df99a466378 | 1536x1024 | 10 | 1485 |
| Yoke model | `generation/image-to-model` | 9998953d-a08c-45ed-a395-21e5f2189d2b | face_limit 4000 | 30 | 1455 |
| Jeep art, dashboard | `generation/image-to-image` | 132c2aed-1f52-4289-9128-41575e8f2fd3 | edit: dashboard and short column instead of the floor lever | 10 | 1445 |
| HMG model | `generation/image-to-model` | 2b2abd70-1fb5-409d-a45f-47b0a6569990 | face_limit 6000 | 30 | 1415 |
| Yoke FBX | `models/convert` | bb40ab20-9ba9-4ba8-9dfc-886883910919 | FBX -> `Vehicles/gun_yoke.fbx` | 5 | 1410 |
| Jeep model, roll 2 (accepted) | `generation/image-to-model` | 2f950f47-3ead-4b87-945d-01c68ff3548b | face_limit 20000, from the dashboard art | 30 | 1380 |
| Jeep art, plain dashboard | `generation/image-to-image` | 0764633d-35c7-4ba9-90eb-6b2dd20f57ec | edit: no column at all | 10 | 1370 |
| HMG FBX | `models/convert` | 3ff8ffb0-5d1b-49d6-82c8-00f459576a9b | FBX -> `Vehicles/gun_hmg.fbx` | 5 | 1365 |
| Jeep model, roll 3 (rejected) | `generation/image-to-model` | b45ff691-641d-4b9b-ae7a-4c088e7abd18 | face_limit 20000, from the plain-dashboard art | 30 | 1335 |
| Jeep body FBX | `models/convert` | ceae2bc3-8eb9-4d98-a9b6-96ded61c7be1 | roll 2 -> `Vehicles/jeep_body.fbx` | 5 | 1330 |

Balances follow the creation order of all M3 tasks, so the clip tasks fill the gaps.
Two image requests were refused with HTTP 429 (one image task at a time). They cost nothing and were re-sent.

All re-rolls were for the body. Roll 1's steering column came out as a long floor lever ending over the
seat. Roll 3 still grew a wheel and lost the olive paint. Roll 2 is kept; its small fused dash wheel is cut
at import (box in the README). The multiview route was dropped because it came back as four three-quarter
views with an invented steering wheel.

## Targets and outpost (milestone 3)

Made on 2026-10-12, after the jeep was accepted. **Credits: 1330 -> 985, 345 spent** (seven models at 45 each,
plus one 30-credit hesco re-roll). The tent, boom post and boom arm were cut by the review. **M3 art total so
far: 1720 -> 985, 735 spent** (cap 945).

Concepts used the jeep concept task `06455900-…` as the style reference; models took the concept task id;
every FBX was converted from its task id. Measurements, scales and verdicts: `unity/ArtSource/M3Vehicles/README.md`.

| Step | Endpoint | Task ID | Settings | Credits | Balance after |
| --- | --- | --- | --- | --- | --- |
| Drum art | `generation/image-to-image` | 7fedb597-7077-4b38-9f15-0523bd841e31 | chat_image_2.5_sunburst, medium, 1024x1024, transparent | 10 | 1320 |
| Plate art | `generation/image-to-image` | 7ae01276-f6c9-43da-b60d-f0c48ba8f8ee | 1024x1536 | 10 | 1310 |
| Drum model | `generation/image-to-model` | 94d47068-be50-46f8-a887-f2b328dfe1be | v3.1-20260211, texture + pbr, standard, face_limit 1500, auto_size | 30 | 1280 |
| Nest art | `generation/image-to-image` | 4019b843-6c67-4d84-8ab7-6ade891109bf | 1536x1024 | 10 | 1270 |
| Plate model | `generation/image-to-model` | 4bb37761-17d7-4a4c-949c-50d98f9febc6 | face_limit 1200 | 30 | 1240 |
| Tower art | `generation/image-to-image` | 9f726676-ef2f-4b95-819e-f84da62692a2 | 1024x1536 | 10 | 1230 |
| Trailer art | `generation/image-to-image` | 8e35e237-f223-4179-a8e1-b88b4bc4b206 | 1536x1024 | 10 | 1220 |
| Nest model | `generation/image-to-model` | adbba5fe-2451-40d9-b52b-d255024eb799 | face_limit 5000 | 30 | 1190 |
| Roadblock art | `generation/image-to-image` | 1365c42d-57aa-4ece-bf9a-e8978b5ca363 | 1536x1024 | 10 | 1180 |
| Tower model | `generation/image-to-model` | 745c65c4-1fa7-4499-8020-26cbbcf69eba | face_limit 6000 | 30 | 1150 |
| Drum FBX | `models/convert` | 6ec927c7-642b-4f85-859d-d96d123fcab0 | FBX -> `Vehicles/fuel_drum.fbx` | 5 | 1145 |
| Trailer model | `generation/image-to-model` | aad94d3d-cac1-4cff-8676-febe4c2465fb | face_limit 5000 | 30 | 1115 |
| Hesco art | `generation/image-to-image` | f5e18870-d891-4991-aaa4-a4c423626c89 | 1536x1024 | 10 | 1105 |
| Plate FBX | `models/convert` | b25980f8-d7c9-4df1-8bc4-fd08bbb13c79 | FBX -> `Vehicles/target_plate.fbx` | 5 | 1100 |
| Roadblock model | `generation/image-to-model` | 85cfef55-d503-4fdb-97c6-4bdb91bcf5e2 | face_limit 6000 | 30 | 1070 |
| Hesco model | `generation/image-to-model` | 449ad7c5-d3a2-4f36-aa99-54d03b9e6cb2 | face_limit 3000 | 30 | 1040 |
| Nest FBX | `models/convert` | 59d08421-2475-4c8c-8c10-244cee57305e | FBX -> `Vehicles/sandbag_nest.fbx` | 5 | 1035 |
| Tower FBX | `models/convert` | a20278f8-a52f-40ba-8b84-354f2e1a675d | FBX -> `Vehicles/watchtower.fbx` | 5 | 1030 |
| Trailer FBX | `models/convert` | 3f6876ca-be88-40ce-a1ae-98c0248d1d57 | FBX -> `Vehicles/fuel_trailer.fbx` | 5 | 1025 |
| Hesco model, roll 2 (rejected) | `generation/image-to-model` | ae0f0b64-c62a-45ce-8ffc-5f2c46b912f8 | face_limit 3000, model_seed 7 | 30 | 995 |
| Roadblock FBX | `models/convert` | e6f59fda-8ef2-45a4-8106-d2935df02246 | FBX -> `Vehicles/roadblock.fbx` | 5 | 990 |
| Hesco FBX | `models/convert` | 24ce4f07-1da6-43de-9b62-02aec77929cd | roll 1 -> `Vehicles/hesco_wall.fbx` | 5 | 985 |

Balances are a running total in creation order; the account reads 985. The image tasks ran one at a time
(parallel image requests get HTTP 429, which costs nothing). The hesco re-roll tried for an axis-aligned
mesh; it came out aligned but lost the wire-mesh texture, so roll 1 is kept with a yaw at import.
