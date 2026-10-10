#!/usr/bin/env bash
# Builds the Unity WebGL game and copies it to web/, which Vercel serves as-is.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY_CLI:-$HOME/.unity/bin/unity}"

"$UNITY" --no-banner --non-interactive run "$ROOT/unity" \
  -- -buildTarget WebGL -executeMethod WardenZero.EditorTools.WebGLBuilder.Build

test -f "$ROOT/unity/Build/WebGL/index.html" || { echo "Build output missing" >&2; exit 1; }

rm -rf "$ROOT/web"
cp -R "$ROOT/unity/Build/WebGL" "$ROOT/web"
echo "web/ updated. Commit it and open a PR; merging to main deploys it."
