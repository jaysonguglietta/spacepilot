#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

mkdir -p .build/macos-validation

export CLANG_MODULE_CACHE_PATH="${CLANG_MODULE_CACHE_PATH:-$PWD/.build/clang-module-cache}"
export SWIFT_MODULE_CACHE_PATH="${SWIFT_MODULE_CACHE_PATH:-$PWD/.build/swift-module-cache}"
mkdir -p "$CLANG_MODULE_CACHE_PATH" "$SWIFT_MODULE_CACHE_PATH"

ARCH="${ARCH:-$(uname -m)}"

swiftc \
  -target "$ARCH-apple-macosx14.0" \
  -module-cache-path "$CLANG_MODULE_CACHE_PATH" \
  apps/macos/SpacePilotMac/Sources/SpacePilotMac/Models.swift \
  apps/macos/SpacePilotMac/Sources/SpacePilotMac/Formatters.swift \
  apps/macos/SpacePilotMac/Sources/SpacePilotMac/Services.swift \
  apps/macos/SpacePilotMac/Validation/CoreValidation.swift \
  -o .build/macos-validation/spacepilot-macos-core-validation

.build/macos-validation/spacepilot-macos-core-validation
