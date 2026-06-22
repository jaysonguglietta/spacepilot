#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-release}"
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

if command -v codesign >/dev/null 2>&1; then
  codesign --force --deep --sign - "$APP_DIR" >/dev/null
fi

ditto -c -k --keepParent "$APP_DIR" "$ARTIFACT_ROOT/SpacePilot-macOS.zip"
shasum -a 256 "$ARTIFACT_ROOT/SpacePilot-macOS.zip" > "$ARTIFACT_ROOT/SpacePilot-macOS.zip.sha256"

echo "Built $APP_DIR"
echo "Packaged $ARTIFACT_ROOT/SpacePilot-macOS.zip"
