#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-release}"
VERSION="${VERSION:-0.1.2}"
REQUIRE_RELEASE_SIGNING="${SPACEPILOT_MACOS_REQUIRE_RELEASE_SIGNING:-false}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

PACKAGE_PATH="apps/macos/SpacePilotMac"
ARTIFACT_ROOT="artifacts/macos"
APP_NAME="SpacePilot.app"
APP_DIR="$ARTIFACT_ROOT/$APP_NAME"
EXECUTABLE_NAME="SpacePilotMac"

export CLANG_MODULE_CACHE_PATH="${CLANG_MODULE_CACHE_PATH:-$PWD/.build/clang-module-cache}"
export SWIFT_MODULE_CACHE_PATH="${SWIFT_MODULE_CACHE_PATH:-$PWD/.build/swift-module-cache}"
mkdir -p "$CLANG_MODULE_CACHE_PATH" "$SWIFT_MODULE_CACHE_PATH"

swift build --package-path "$PACKAGE_PATH" -c "$CONFIGURATION"
bash scripts/macos/validate-core.sh

EXECUTABLE_PATH="$PACKAGE_PATH/.build/$CONFIGURATION/$EXECUTABLE_NAME"
if [[ ! -x "$EXECUTABLE_PATH" ]]; then
  echo "Missing executable at $EXECUTABLE_PATH" >&2
  exit 1
fi

rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
cp "$EXECUTABLE_PATH" "$APP_DIR/Contents/MacOS/$EXECUTABLE_NAME"
cp "apps/macos/packaging/Info.plist" "$APP_DIR/Contents/Info.plist"
cp "apps/macos/packaging/SpacePilot.icns" "$APP_DIR/Contents/Resources/SpacePilot.icns"
cp "apps/macos/packaging/PrivacyInfo.xcprivacy" "$APP_DIR/Contents/Resources/PrivacyInfo.xcprivacy"

if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$ ]]; then
  echo "Invalid app version: $VERSION" >&2
  exit 1
fi
bundle_version="${VERSION%%-*}"
plutil -replace CFBundleShortVersionString -string "$bundle_version" "$APP_DIR/Contents/Info.plist"
plutil -replace CFBundleVersion -string "$bundle_version" "$APP_DIR/Contents/Info.plist"

if [[ "$REQUIRE_RELEASE_SIGNING" == "true" ]]; then
  : "${SPACEPILOT_MACOS_SIGNING_IDENTITY:?A Developer ID signing identity is required}"
  : "${SPACEPILOT_MACOS_TEAM_ID:?An Apple Developer Team ID is required}"
  : "${SPACEPILOT_MACOS_KEYCHAIN:?The release signing keychain is required}"
  codesign --force --deep --options runtime --timestamp \
    --sign "$SPACEPILOT_MACOS_SIGNING_IDENTITY" \
    --keychain "$SPACEPILOT_MACOS_KEYCHAIN" "$APP_DIR"
  codesign --verify --deep --strict --verbose=2 "$APP_DIR"
  if ! codesign -dv --verbose=4 "$APP_DIR" 2>&1 | grep -Fqx "TeamIdentifier=$SPACEPILOT_MACOS_TEAM_ID"; then
    echo "Signed app does not have the configured Apple Developer Team ID." >&2
    exit 1
  fi
elif command -v codesign >/dev/null 2>&1; then
  codesign --force --deep --sign - "$APP_DIR" >/dev/null
fi

ditto -c -k --keepParent "$APP_DIR" "$ARTIFACT_ROOT/SpacePilot-macOS.zip"
shasum -a 256 "$ARTIFACT_ROOT/SpacePilot-macOS.zip" > "$ARTIFACT_ROOT/SpacePilot-macOS.zip.sha256"

echo "Built $APP_DIR"
echo "Packaged $ARTIFACT_ROOT/SpacePilot-macOS.zip"
