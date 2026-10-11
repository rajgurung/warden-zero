#!/usr/bin/env bash
# Builds the Unity WebGL game and copies it to web/, which Vercel serves as-is.
# WebGLBuilder builds the Addressables content first (Stage 2, the jungle); the player
# build copies those bundles to StreamingAssets/aa, so they end up in web/StreamingAssets
# and load from the same site with relative URLs.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY_CLI:-$HOME/.unity/bin/unity}"

"$UNITY" --no-banner --non-interactive run "$ROOT/unity" \
  -- -buildTarget WebGL -executeMethod WardenZero.EditorTools.WebGLBuilder.Build

test -f "$ROOT/unity/Build/WebGL/index.html" || { echo "Build output missing" >&2; exit 1; }
ls "$ROOT"/unity/Build/WebGL/StreamingAssets/aa/WebGL/*.bundle >/dev/null 2>&1 || { echo "Addressables bundles missing" >&2; exit 1; }

rm -rf "$ROOT/web"
cp -R "$ROOT/unity/Build/WebGL" "$ROOT/web"
# Build-time only (it lists the types the bundles need kept); no use on the site.
rm -rf "$ROOT/web/StreamingAssets/aa/AddressablesLink"
du -sh "$ROOT/web/Build" "$ROOT/web/StreamingAssets"
echo "web/ updated. Commit it and open a PR; merging to main deploys it."
