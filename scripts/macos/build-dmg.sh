#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

VERSION="${VERSION:-0.1.0}"
SKIP_APP_BUILD="${SKIP_APP_BUILD:-false}"
ARTIFACT_ROOT="artifacts/macos"
APP_DIR="$ARTIFACT_ROOT/SpacePilot.app"
DMG_ROOT="$ARTIFACT_ROOT/dmg-root"
DMG_PATH="$ARTIFACT_ROOT/SpacePilot-macOS.dmg"
VERSIONED_DMG_PATH="$ARTIFACT_ROOT/SpacePilot-$VERSION-macOS.dmg"

if [[ "$SKIP_APP_BUILD" != "true" ]]; then
  bash scripts/macos/build-app.sh
fi

if [[ ! -d "$APP_DIR" ]]; then
  echo "Missing app bundle at $APP_DIR" >&2
  exit 1
fi

rm -rf "$DMG_ROOT" "$DMG_PATH" "$VERSIONED_DMG_PATH" "$DMG_PATH.sha256" "$VERSIONED_DMG_PATH.sha256"
mkdir -p "$DMG_ROOT"
cp -R "$APP_DIR" "$DMG_ROOT/SpacePilot.app"
ln -s /Applications "$DMG_ROOT/Applications"

hdiutil create \
  -volname "SpacePilot" \
  -srcfolder "$DMG_ROOT" \
  -ov \
  -format UDZO \
  "$DMG_PATH" >/dev/null

cp "$DMG_PATH" "$VERSIONED_DMG_PATH"

(
  cd "$ARTIFACT_ROOT"
  shasum -a 256 "$(basename "$DMG_PATH")" > "$(basename "$DMG_PATH").sha256"
  shasum -a 256 "$(basename "$VERSIONED_DMG_PATH")" > "$(basename "$VERSIONED_DMG_PATH").sha256"
)

rm -rf "$DMG_ROOT"

echo "Built $DMG_PATH"
echo "Built $VERSIONED_DMG_PATH"
