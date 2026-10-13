# Tripo log: Milestone 3 (Warden vehicle clips)

Made on 2026-10-12 with the Tripo v3 API (`openapi.tripo3d.ai/v3`, international) and `tools/tripo`.

**Credits for clips: 55** (1 retarget of 5 presets, 1 convert). The whole M3 art job so far ran from 1720 to
1330 (390 spent). The jeep and gun spend is in `Assets/Art/Campaign/TRIPO_LOG.md`.

Trims, root notes and the accept/reject reasons are in `unity/ArtSource/M3Vehicles/README.md`.
The request body is `ArtSource/M3Vehicles/requests/c1-retarget.json`; task records are in `ArtSource/M3Vehicles/tasks/`.
The convert body was sent inline: `{"input": "<task id>", "format": "FBX"}`.

The retarget ran on the existing Warden rig task `e73d30f3-ec0e-466e-b3bf-553da60a7ac9`
(rig v1.0-20240301, biped, spec mixamo). Options: glb, bake_animation, export_with_geometry, animate_in_place.
The preset list on developers.tripo3d.ai was re-checked first: `sit`, `greet_01`-`04`, `wave_goodbye_01`/`02`
and `play_video_game` exist; nothing holds a vehicle wheel or spade grips.

| # | Step | Endpoint | Task ID | Settings | Credits | Balance after |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Retarget | `animations/retarget` | 2356ac36-4d29-46e6-8d43-936b09728ca2 | sit, greet_01, greet_02, wave_goodbye_01, play_video_game | 50 | 1660 |
| 2 | FBX | `models/convert` | 5577b4f3-404b-4604-96fa-274b6e436be8 | FBX -> `Anims/warden_anims_m3_vehicle.fbx` | 5 | 1595 |

The balance column follows the order of all M3 tasks (props included).
Unlike M2, the accepted takes were not re-retargeted on their own: four of five were kept, so the one
rejected take (`greet_01`) stays in the FBX and is simply not imported (the M1 approach). That saved 40 credits.

| Take | Verdict | Use |
| --- | --- | --- |
| `sit` | Accepted | `seated`, 0-1.9 s: the seated hold for the driver, the squadmate and the gunner's rest. |
| `play_video_game` | Accepted | `drive_hold`: seated with both hands forward at wheel height; upper body, with hand IK on the steering grips. |
| `wave_goodbye_01` | Accepted | `drive_wave`: a seated left-hand wave from the wheel, for the squadmate. Upper body only. |
| `greet_02` | Accepted (optional) | `wave_standing`: one standing right-hand wave. |
| `greet_01` | Rejected | Walks forward while waving; the hips climb 0.30 m. |

Judged from Blender contact sheets of the task GLB (`ArtSource/M3Vehicles/contact_clips.jpg`), then sampled
from the FBX in a throwaway Unity 6000.3 project (Generic import): positions match the GLB with X negated.
Not yet checked through the Warden's Humanoid Animator.
