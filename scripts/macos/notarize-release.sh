#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

: "${VERSION:?A validated release version is required}"
: "${SPACEPILOT_NOTARY_APPLE_ID:?An Apple ID is required for notarization}"
: "${SPACEPILOT_NOTARY_PASSWORD:?An app-specific password is required for notarization}"
: "${SPACEPILOT_MACOS_TEAM_ID:?An Apple Developer Team ID is required}"

APP_DIR="artifacts/macos/SpacePilot.app"
ZIP_PATH="artifacts/macos/SpacePilot-macOS.zip"
DMG_PATH="artifacts/macos/SpacePilot-macOS.dmg"
VERSIONED_DMG_PATH="artifacts/macos/SpacePilot-$VERSION-macOS.dmg"

if [[ ! -d "$APP_DIR" || ! -s "$ZIP_PATH" ]]; then
  echo "Build and sign the macOS app before notarizing it." >&2
  exit 1
fi

notarize() {
  local archive="$1"
  local response
  response="$(xcrun notarytool submit "$archive" \
    --apple-id "$SPACEPILOT_NOTARY_APPLE_ID" \
    --password "$SPACEPILOT_NOTARY_PASSWORD" \
    --team-id "$SPACEPILOT_MACOS_TEAM_ID" \
    --wait --output-format json)"
  printf '%s' "$response" | python3 -c '
import json
import sys
result = json.load(sys.stdin)
if result.get("status") != "Accepted":
    raise SystemExit("Notarization failed: " + str(result.get("status", "unknown")) + " (submission " + str(result.get("id", "unknown")) + ")")
print("Notarization accepted:", result.get("id", "unknown"))
'
}

# The ZIP must be notarized first so the app can carry its own stapled ticket.
notarize "$ZIP_PATH"
xcrun stapler staple "$APP_DIR"
xcrun stapler validate "$APP_DIR"
spctl --assess --type execute --verbose=2 "$APP_DIR"
ditto -c -k --keepParent "$APP_DIR" "$ZIP_PATH"
shasum -a 256 "$ZIP_PATH" > "$ZIP_PATH.sha256"

VERSION="$VERSION" SKIP_APP_BUILD=true bash "$SCRIPT_DIR/build-dmg.sh"
notarize "$DMG_PATH"
xcrun stapler staple "$DMG_PATH"
xcrun stapler validate "$DMG_PATH"
cp "$DMG_PATH" "$VERSIONED_DMG_PATH"
(
  cd artifacts/macos
  shasum -a 256 "$(basename "$DMG_PATH")" > "$(basename "$DMG_PATH").sha256"
  shasum -a 256 "$(basename "$VERSIONED_DMG_PATH")" > "$(basename "$VERSIONED_DMG_PATH").sha256"
)
