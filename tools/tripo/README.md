# Tripo 3D API helper

`tripo.mjs` is a tiny client for the Tripo **v3** API (`https://openapi.tripo3d.ai/v3`).
It has no dependencies and needs Node 20+.

The key comes from the `TRIPO_API_KEY` environment variable. Keep it in the
gitignored `.env.local` at the repo root and load it with `--env-file`. The script
never prints the key.

```sh
T="node --env-file=.env.local tools/tripo/tripo.mjs"

$T balance                                   # available and frozen credits
$T upload docs/warden-hero.png               # -> file_token=file_...
$T create generation/image-to-model @body.json   # prints balance, then task_id=...
$T create animations/rig-check '{"input":"<task_id>"}'
$T poll <task_id>                            # waits; prints status and credits_consumed
$T download <task_id> some/dir               # saves every *_url output + <task_id>.json
```

`create` takes any v3 endpoint path and a JSON body (inline or `@file`). Endpoints used so far:
`generation/image-to-model`, `animations/rig-check`, `animations/rig`,
`animations/retarget`, `models/convert`. Request bodies for the Warden are in
`assets3d/warden/requests/`.

Notes:
- Output URLs expire within minutes. Run `download` straight after `poll` succeeds.
- The saved `<task_id>.json` has the URLs replaced by local file names.
- Failed or cancelled tasks are refunded. Check with `balance`.
- Prices (Oct 2026): image-to-model with texture 30, rig check free, rig 25,
  retarget 10 per animation, basic convert 5 (10 if you set face_limit,
  texture_size, scale_factor and similar options).

## Previews

`preview/` holds a headless three.js renderer for checking GLB/FBX output.
Copy it to a scratch directory outside the repo, then:

```sh
npm i playwright@1.55.0 three@0.169.0
node render.mjs /abs/path/model.glb out static-shots.json
node render.mjs /abs/path/animated.glb out anim-shots.json
```

Chromium runs with SwiftShader (`--use-gl=angle --use-angle=swiftshader`).
Tripo models face +X, so `azim: 90` is the front view.
