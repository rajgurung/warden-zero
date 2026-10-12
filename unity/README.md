# Warden Zero: Unity prototype

A trial port of Warden Zero to Unity 6.3 LTS (6000.3.26f1), built for the web (WebGL).
One arena, the Warden, two waves of grunts, swarmers and runners. The Babylon.js game
in `../src` is untouched.

## Controls

Desktop: WASD or arrows to move, the mouse aims, hold the left button to fire, Space (or
Shift) dashes, E or right click bombs, V switches the camera, Esc or P pauses, 1 2 3 pick an
upgrade on level-up. In the jungle Space is the context move (jump, vault, climb, up), Shift
dashes and C lets go (or lowers him over the edge of a top); a chip under the crosshair says
what Space and C would do.

Touch (phones and tablets): drag anywhere to move; the Warden aims and fires at the
nearest enemy by himself; DASH and BOMB buttons sit on the right. In the jungle the corner
button is JUMP, labelled JUMP, VAULT, CLIMB or UP; DROP shows above it when he can let go.
Running into a low log or crate vaults it by itself (pause menu: AUTO-VAULT).

The jeep (milestone 3): Space at a door gets in (the chip says DRIVE), W/S or the arrows
drive and brake (reverse when stopped), A/D steer, Space is the handbrake, C gets out on
the free side when stopped, R resets onto the road behind a short fade, V switches between
the chase and the high view. On the gun the mouse aims, the left button fires (hold it and
the barrel heats up; overheated, it locks for 1.3 s), the right button zooms and V switches
Close and Wide. Touch: the stick's x steers, GAS and BRAKE are held, HANDBRAKE, EXIT and
RESET reuse the jungle's buttons; on the gun a drag anywhere aims, FIRE is held, ZOOM.
DRIVE ASSIST (pause menu, on by default on touch) keeps the jeep near the road and pulls
the gun's aim onto targets.

Debug URLs: `?boss` jumps to the Colossus, `?wave=N` starts at wave N.

## Cameras

Two gameplay views; **V** (or the touch **VIEW** button) switches them, and each stage
remembers its choice (PlayerPrefs `wz.view.arena`, `wz.view.jungle`). The arena opens on the
high view (its horde combat is tuned for it); the jungle opens behind the Warden. Greenfang
keeps the high view.

In the jungle the camera follows the context (`CameraContext`, pause menu CAMERA · AUTO /
MANUAL, PlayerPrefs `wz.cam.auto`): a live enemy within 18 m (or a combat zone) switches to
the high view, which holds until 4 s clear of 25 m; otherwise he is seen from behind. At
least 2 s pass between automatic switches, none happens while he is off the ground or on a
block, and the views blend over 0.6 s. V pins the other view until the context changes; VIEW
shows AUTO while the context is in charge. Manual is the toggle above. Near an edge, or on
the move, the behind view centres him and steps back a little to see the top.

- High view: as before, the mouse aims at the cursor's point.
- Behind view: a Cinemachine third-person camera over his right shoulder (`CameraFollow`,
  settings in `GameConfig`: pivot 2.3 m, shoulder 0.9 m right and 0.5 m up, 4.2 m back,
  8 degrees down, 55 degree lens; portrait screens pull in the shoulder and step back).
  Click to lock the pointer, then the mouse looks; Esc releases it and pauses. WASD is
  relative to the camera; he faces the camera's way when moving or firing (his legs turn
  toward a strafe). He fires at whatever is under the centre crosshair. `CameraCollision`
  keeps the camera out of trunks, rocks, walls and the ground (`WorldCast`: the game has no
  physics colliders); jungle plants (`FoliageLit` shader) fade near the lens and around him.
  Touch: the left half is the stick, a drag on the right half looks, FIRE fires while held.
- The drop (play-test polish, clips and gear from `ArtSource/M1Polish`): he wears the HALO
  pack, jump helmet and altimeter from boarding to the landing. Off the door he dives
  cleanly into a still, symmetric arch (`SkydivePose`, no flutter; the `fall_loop` clip was
  compared and read as swimming). Under the canopy `canopy_hold` has his fists on the
  toggles, the lines run to his hands and A/D pull one down. He lands with the `land` clip
  (deeper and slower when hard), unclips the pack, which stays on the ground, and takes the
  rifle back. At rest he stands in `idle_relaxed` and looks around every 20-40 s.
- The arena's behind view adds a mild exposure lift (`ArenaBehindFill` volume).
- Set pieces keep their own Cinemachine shots; freefall and canopy are chase views behind
  and above him, looking ahead and down along his flight.

## Campaign (milestone 1, the drop slice)

The menu's **Campaign** plays the arena; the Colossus calls in the chopper. Hold the LZ
for 20 s (a door gunner helps), walk into the cabin door, and the chopper lifts off. The
jungle loads during the climb; a short flight (Space or Enter skips), then the jump:
Space / JUMP leans out of the door, A/D turn, W tracks, S brakes, Space / DEPLOY opens the
chute (an automatic opener fires at 100 m, a hard landing), A/D steer the canopy, Space /
FLARE just above the ground. Walk to checkpoint A and hold it: the run is saved and
**Continue** resumes there (or at the flight, if the save is from boarding).

Touch: the stick steers, a context button shows BOARD, JUMP, DEPLOY or FLARE, SKIP skips
the flight. Quality: High on desktop, Low on phones (a coarse pointer); the pause menu
switches it.

Debug URLs: `?campaign=extraction|flight|jump|jungle|course|walk|checkpoint` start at that
part (`course`: on foot at the LZ, facing the traversal course);
`&sidecam` adds a side view of the Warden (or the chopper) in the corner; `&autoflare`
flares by itself at 6 m.

The jungle (`Assets/Scenes/Jungle.unity`) is generated by `SceneBuilder.Jungle.cs` and
ships as Addressables content (`AddressablesSetup`): `WebGLBuilder` builds the bundles
first, and the player build copies them to `StreamingAssets/aa`, so they load from the same
site after the first page load. `Campaign.Begin` starts the download in the background and
lets the bundles go once they are in the browser's cache; the scene load then revalidates
them (a 304). While a load is under way the overlay blocks clicks and pause is refused; a
failed load offers RETRY and MAIN MENU. Chrome measurements (M1 Pro): the wasm heap is
138 MB in Stage 1 and 286 MB once the jungle is in at Low. On High, with the M2 course
props, the jungle load sometimes takes it one growth step further, to 344 MB.

## Traversal (milestone 2)

`Traversal` (plain C#) moves him in the jungle over `World.Blocks`, turned solid boxes from
`CourseLayout`: steps up to 0.5 m, jumps 1.1 m, vaults 0.5-1.4 m tops, climbs onto tops up to
2.6 m, jump-grabs edges up to 3.7 m and hangs (shimmy, climb up, let go), climbs vine faces,
and lowers himself over an edge with C. Falls up to 5 m don't hurt; longer ones do, never
lethally. The camera, the crosshair and aimed bolts stop at blocks (`WorldCast`). The course
runs from the LZ to checkpoint A: a log, crate steps and a rock shelf, stepping blocks over the
ford, a rock ledge, the ruin-wall gate (the one mandatory beat: climb the vines, walk the top,
hang and shimmy off the far side) and a terrace. Its props are Tripo models fitted to the
blocks (`Assets/Art/Campaign/TRIPO_LOG.md`); the clips (takeoff, ledge reach, climb steps, the
vine climb) are in `Assets/Art/Warden3D/TRIPO_LOG_M2.md`, and `TraversalPose` poses the rest
(hang, vault, hand holds) and slings the rifle while he climbs. Known gaps for M4: enemies
and high-view bolts ignore block heights, and the jungle has no corridor bounds yet (the gate
can't be walked round within 16 m of the trail).

## Vehicles (milestone 3)

After checkpoint A the campaign goes on: the Warden takes an open-top jeep from the motor
pool down a 569 m jungle dirt road to checkpoint B, which saves. At B a squadmate (a runtime
copy of the Warden, tinted tan with the cyan off, in the jump helmet) takes the wheel and the
Warden mans the rear heavy machine gun for a 52 s on-rails ride past 27 destructible
targets (drums chain) to a roadblock: MILESTONE 3 COMPLETE. Continue resumes at A or at B
(after the end it replays the ride from B).

- Handling: `Vehicle` (plain C#, no physics engine) samples the ground at four wheels
  through `World.HeightAt` and collides with trunks, rocks and blocks; `GameConfig.Jeep`
  holds the numbers. `RoadPath` and `RoadLayout` define the road, its corridor (18 m each
  side for the jeep, 20 m on foot) and the ride. `DriveBot` drives it for tests.
- Look: `VehicleRig` poses the body (pitch, roll and bounce), wheels and steering; dust off
  the rear wheels, a muzzle flash and light per round. `VehicleAudio` plays the synthesised
  engine (three gears), skid and clunk (`SynthAudio`), and shakes the camera on knocks.
  `VehiclePose` seats the driver (the `seated` clip, hands on the wheel at ten and two) and
  stands the gunner at the grips.
- Cameras: `DriveCamera` (a chase camera behind and above the jeep, the high view while
  driving; Close and Wide on the gun), blended by `CameraFollow`; `CameraContext` adds
  Driving and Gunner.
- Gun: `Turret` (8 rounds a second, heat, no fire into the cab), `TurretRide`, hitscan
  rounds through `WorldCast` and `Destructible.Cast`, `AimAssist` on touch.
- Art: the Tripo jeep, gun, targets and outpost (`ArtSource/M3Vehicles/README.md`) are one
  `VehicleArt` prefab built by `SceneBuilder.Vehicles.cs` in its own Addressables group,
  `Vehicles` (4.1 MB), loaded after the jungle by `VehicleArtLoader`. The greybox stays
  under the pivots and shows if the load fails.
- The road is a terrain tile (`SceneBuilder.Road.cs`) set into a valley cut in the landscape.

Measurements (Chrome, M1 Pro, the published build): first load 22.46 MB (wasm 8.92 MB,
data 13.40 MB); jungle bundle 40.56 MB, `Vehicles` 4.12 MB. Wasm heap 286 MB at Low (iPhone
13 emulation) driving and on the ride; at High 286 or 344 MB depending on the run, as in M2.
60 fps (vsync) at High 1920 x 1080 driving and firing, and at Low on the climb to the rim
and on the ride. Not yet measured on a real iPhone.

Debug URLs: `?campaign=drive` (in the jeep at the motor pool), `checkpointb` (at B, as
Continue there), `ride` (on the gun, the ride under way); `?mem` shows the wasm heap in the
corner; `?memprobe=N` allocates N MB 3 s after landing and logs whether the heap grew.

Third-party assets and licences: `Assets/ThirdParty/CREDITS.md`. Tripo props:
`Assets/Art/Campaign/TRIPO_LOG.md`. `Snapshots.Capture` renders stills of the scenes to
`Logs/Snapshots`.

## Open in the editor

Open this `unity/` folder in Unity Hub with editor 6000.3.26f1, or use the Unity CLI:

```sh
~/.unity/bin/unity --no-banner open /path/to/repo/unity
```

The scene is `Assets/Scenes/Arena.unity`. Menu **Warden Zero** has
**Rebuild Arena Scene** and **Build WebGL**.

## Regenerate the scene

The scene, materials, prefabs and generated textures come from
`Assets/Editor/SceneBuilder.cs`. Re-run it after changing it. Hand edits to the scene
are overwritten.

```sh
~/.unity/bin/unity --no-banner --non-interactive run /path/to/repo/unity \
  -- -buildTarget WebGL -executeMethod WardenZero.EditorTools.SceneBuilder.Build
```

## Build for the web

```sh
~/.unity/bin/unity --no-banner --non-interactive run /path/to/repo/unity \
  -- -buildTarget WebGL -executeMethod WardenZero.EditorTools.WebGLBuilder.Build
```

The output goes to `Build/WebGL`, which git ignores. The first build takes a few
minutes. Later builds take about a minute.

Serve it with any static server:

```sh
cd /path/to/repo/unity/Build/WebGL && python3 -m http.server 8765
# open http://localhost:8765
```

The page uses the project's own WebGL template (`Assets/WebGLTemplates/WardenZero`):
the canvas fills the window, renders at device resolution capped at 2x, and shows a
Warden Zero loading bar. The build uses gzip with the decompression fallback. It loads without special server
headers. A server that sends `Content-Encoding: gzip` for the `.unityweb` files starts
it faster. The first load is about 22.4 MB (wasm 8.9 MB, data 13.4 MB); the jungle's
bundles add 40.7 MB (LZ4; WebGL players cannot read LZMA bundles), fetched during Stage 1.

## Tests

```sh
~/.unity/bin/unity --no-banner --non-interactive test /path/to/repo/unity --mode EditMode
~/.unity/bin/unity --no-banner --non-interactive test /path/to/repo/unity --mode PlayMode
```

The EditMode tests cover the arena maths. The PlayMode tests load the real scene and
drive it with virtual keyboard and mouse devices. They cover movement and frames,
mirroring, dash, firing, kills, wave clear, and game over and restart.

## Layout

- `Assets/Scripts`: runtime code (`WardenZero` assembly).
  - `GameConfig`: tuning numbers ported from `src/config/*.ts`, plus wall collision.
  - `PlayerController`: input, movement, dash, firing and sprite frame choice.
  - `Enemy`, `Bolt`, `GameManager` (waves, score, game over), `Hud`, `CameraFollow`,
    `Billboard`.
  - `Effects`: particle bursts (hit sparks, death bursts, hurt and dash bursts) and
    camera shake, ported from `src/systems/Effects.ts`.
- `Assets/Shaders/AdditiveGlow.shader`: unlit additive HDR shader for rings, sparks,
  trails and the muzzle flash.
- `Assets/Editor`: `SceneBuilder`, `WebGLBuilder` and `ArtImportSettings`. The last one
  sets texture import options in code.
- `Assets/Art/Warden3D`: the rigged Tripo Warden, its two clip FBXs and the rifle (from
  the `warden-3d` branch, `assets3d/warden/README.md`). `Assets/Editor/Warden3DImport.cs`
  sets the import options; `SceneBuilder.Warden3D.cs` builds the materials, the animator
  controller and the Warden. `SceneBuilder.UseModelWarden = false` brings back the sprite Warden.
- `Assets/Art/Hero`: the hero frames from `public/assets/sprites/hero`, cleaned. The
  opaque black boxes were made transparent with a flood fill from the edges. Only pure
  black pixels connected to the border were cleared. Stray bits of neighbouring frames
  were dropped. Each frame was then padded to 512x512.
- `Assets/Art/Enemies`: grunt and runner frames, padded from 192x256 to 256x256.
  WebGL only DXT-compresses power-of-two textures here. Unpadded, each hero frame
  shipped as 1 MB of raw RGBA.
- `Assets/Audio`: a few sound effects from `public/assets/audio`.

## Notes

- Render pipeline: URP, from the `Universal 3D` template. Unity's CLI recommends it,
  and the Built-in pipeline is deprecated from Unity 6.5. WebGL uses the "Mobile"
  quality level. The scene builder sets it to full render scale, 4x MSAA, HDR, soft
  shadows and a 2048 shadow map.
- Look: matched to `src/render/Stage.ts`. A global Volume
  (`Assets/Settings/ArenaPostFX.asset`) adds bloom, ACES tone mapping, exposure 1.2,
  contrast +15 and a vignette. Bloom stands in for Babylon's glow layer. Only HDR
  emissives cross its threshold: the rims, strip, bolts, rings and sparks. Thin
  additive halo strips along the wall rims widen the glow toward Babylon's 48 px
  blur. Lit surfaces use the specular workflow with environment reflections off.
  Unity's default grey reflection otherwise lifts the whole floor.
- Sprites use the URP `Sprite-Unlit-Default` shader, so lighting does not wash them out.
- Collision is the same circle-against-box maths as the Babylon version. There is no
  physics engine.
