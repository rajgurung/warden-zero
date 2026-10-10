# Warden Zero: Unity prototype

A trial port of Warden Zero to Unity 6.3 LTS (6000.3.26f1), built for the web (WebGL).
One arena, the Warden, two waves of grunts, swarmers and runners. The Babylon.js game
in `../src` is untouched.

## Controls

WASD or arrows to move. The mouse aims. Hold the left button to fire. Space dashes.
R restarts after game over.

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

The build uses gzip with the decompression fallback. It loads without special server
headers. A server that sends `Content-Encoding: gzip` for the `.unityweb` files starts
it faster. The download is about 13.3 MB: wasm 7.8 MB, data 6.1 MB.

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
- `Assets/Editor`: `SceneBuilder`, `WebGLBuilder` and `ArtImportSettings`. The last one
  sets texture import options in code.
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
  quality level. The scene builder sets it to full render scale with 4x MSAA and a
  2048 shadow map.
- Sprites use the URP `Sprite-Unlit-Default` shader, so lighting does not wash them out.
- Collision is the same circle-against-box maths as the Babylon version. There is no
  physics engine.
