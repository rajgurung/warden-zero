# Warden Zero: Unity prototype

A trial port of Warden Zero to Unity 6.3 LTS (6000.3.26f1), built for the web (WebGL).
One arena, the Warden, two waves of grunts, swarmers and runners. The Babylon.js game
in `../src` is untouched.

## Controls

Desktop: WASD or arrows to move, the mouse aims, hold the left button to fire, Space
dashes, E or right click bombs, Esc or P pauses, 1 2 3 pick an upgrade on level-up.

Touch (phones and tablets): drag anywhere to move; the Warden aims and fires at the
nearest enemy by himself; DASH and BOMB buttons sit on the right.

Debug URLs: `?boss` jumps to the Colossus, `?wave=N` starts at wave N.

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
it faster. The download is about 16.6 MB: wasm 8.2 MB, data 8.2 MB.

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
