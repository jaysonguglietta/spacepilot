# macOS QA Matrix

Use this matrix before distributing SpacePilot for macOS.

## Platforms

| Platform | Apple Silicon | Intel | Notes |
| --- | --- | --- | --- |
| macOS 26.5.1 (25F80) | Required | Optional | Primary requested target. |
| Latest generally available macOS | Required | Optional | Confirms current compatibility. |
| Clean user account | Required | Optional | Validates first-run and local data creation. |

## Install And Launch

| Scenario | Expected Result |
| --- | --- |
| `swift run --package-path apps/macos/SpacePilotMac -c release SpacePilotMac` | App launches with SpacePilot window. |
| `bash scripts/macos/validate-core.sh` | Core path safety, quarantine, receipt, and preference validation passes. |
| `swift test --package-path apps/macos/SpacePilotMac -c release` | SwiftPM tests pass on machines with full Xcode/XCTest. |
| `bash scripts/macos/build-app.sh` | Builds, validates, creates `SpacePilot.app`, and writes zip/checksum. |
| `bash scripts/macos/build-dmg.sh` | Creates `SpacePilot-macOS.dmg`, a versioned DMG, and checksums. |
| Open DMG | Disk image mounts and shows `SpacePilot.app` plus Applications shortcut. |
| Drag app from DMG to Applications | App copies successfully and launches from Applications. |
| Launch app bundle | App opens without terminal dependency. |
| Gatekeeper with ad-hoc bundle | Local builds may require user approval. |
| Developer ID signed bundle | Signature is valid. |
| Notarized bundle | macOS accepts the app without quarantine warnings. |

## Core Workflows

| Workflow | Expected Result |
| --- | --- |
| First-run note | Safety note appears once and can be dismissed. |
| Cleanup scan | User temp/log/cache candidates appear with risk, size, path. |
| Filter/search | Candidate list updates immediately. |
| Quarantine selected cleanup | Files move under `~/Library/Application Support/SpacePilot/Quarantine`. |
| Restore selected quarantine | Files restore when original path is clear. |
| Purge selected quarantine | Payload folder and manifest entry are removed. |
| Receipt creation | Cleanup receipt appears under `Receipts`. |
| Large-file scan | Personal files above threshold are listed without auto-selection. |
| Duplicate scan | Same-size files are hash-verified before display. |
| RAM Assist refresh | Memory pressure, available RAM, swap use, uptime, process count, top processes, and recommendations refresh without quitting apps. |
| RAM Assist OS handoff | Activity Monitor and Login Items buttons open expected macOS tools. |
| Settings save | Preferences persist across relaunch. |
| Activity log | Important actions and warnings are recorded. |

## Safety Cases

| Case | Expected Result |
| --- | --- |
| Candidate is cleanup root itself | Blocked. |
| Candidate is outside approved root | Blocked. |
| Candidate is sibling with same prefix | Blocked. |
| Symbolic link in cleanup root | Skipped. |
| Protected extension found | Skipped. |
| Restore path already exists | Restore skipped with warning. |
| File disappears after scan | Cleanup continues and reports warning. |
| File locked by app | Cleanup does not crash and reports warning. |
| `vm_stat`, `ps`, or `sysctl` output changes | RAM Assist parser handles expected local output or fails gracefully. |
| High swap or memory pressure | RAM Assist recommends reviewing top processes instead of running `purge`. |

## Distribution Evidence

Record for each Mac release candidate:

- Git commit.
- Swift version.
- macOS version/build.
- Artifact name.
- SHA-256 checksum.
- Signing identity.
- Notarization result.
- QA owner and date.
- Known issues.
