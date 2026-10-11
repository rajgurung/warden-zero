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
