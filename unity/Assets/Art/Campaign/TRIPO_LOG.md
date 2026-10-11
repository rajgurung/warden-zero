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
