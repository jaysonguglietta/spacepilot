#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
WORK_DIR="$(mktemp -d /private/tmp/spacepilot-audit-compiler.XXXXXX)"

swiftc -module-cache-path "$WORK_DIR/module-cache" \
  "$REPO_ROOT/apps/macos/SpacePilotMac/Sources/SpacePilotMac/Models.swift" \
  "$REPO_ROOT/apps/macos/SpacePilotMac/Sources/SpacePilotMac/Services.swift" \
  "$SCRIPT_DIR/mac-quarantine-probe.swift" \
  -o "$WORK_DIR/mac-quarantine-probe"

# The probe always creates its own new private fixture directory.
"$WORK_DIR/mac-quarantine-probe"
