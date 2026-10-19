#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
WORK_DIR="$(mktemp -d /private/tmp/spacepilot-audit-feature-build.XXXXXX)"

swiftc -module-cache-path "$WORK_DIR/module-cache" \
  "$REPO_ROOT/apps/macos/SpacePilotMac/Sources/SpacePilotMac/Models.swift" \
  "$REPO_ROOT/apps/macos/SpacePilotMac/Sources/SpacePilotMac/Formatters.swift" \
  "$REPO_ROOT/apps/macos/SpacePilotMac/Sources/SpacePilotMac/Services.swift" \
  "$SCRIPT_DIR/mac-feature-probe.swift" \
  -o "$WORK_DIR/mac-feature-probe"

# The probe creates and removes its own UUID-named fixture under /private/tmp.
"$WORK_DIR/mac-feature-probe"
