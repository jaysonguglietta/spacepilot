# Safety Model

SpacePilot is designed around transparent cleanup and conservative defaults. The app should never behave like a black-box registry cleaner or silent uninstaller.

## Core Principles

- Scan before cleanup.
- Show every cleanup candidate before action.
- Require explicit user approval.
- Prefer quarantine over immediate deletion.
- Avoid automatic registry changes.
- Avoid silent uninstalling or startup disabling.
- Never clean broad user-content roots such as Documents, Desktop, Downloads, or application data roots.

## Approved Cleanup Roots

Cleanup candidates must come from known cleanup rules. Rules define approved roots, search patterns, age thresholds, recursion behavior, and risk level.

Examples include:

- User temp files.
- Windows temp files.
- Windows Error Reporting archives and queues.
- Crash dumps.
- Delivery Optimization cache.
- Explorer thumbnail and icon cache.
- Browser caches.
- Teams, Slack, Discord, and Zoom caches/logs.
- Windows maintenance logs.
- Microsoft Store package temp folders.

## Path Guardrails

Before a candidate can be cleaned, `PathSafety` verifies that the candidate is inside its approved root and is not the root itself. This prevents accidental deletion of broad folders.

SpacePilot also skips reparse points to avoid following links into unexpected locations.

## Risk Levels

Low-risk items are generally temporary files, reports, and logs that are less likely to affect user state.

Medium-risk items may include caches, system temp folders, Windows Update cache, or diagnostic logs that can be useful during troubleshooting.

High-risk items are used for explicit personal-file workflows such as large-file and duplicate review. These require direct item selection.

## Quarantine

Quarantine moves selected files into `%LOCALAPPDATA%\SpacePilot\Quarantine\` and records a manifest entry with the original path, size, risk, category, and timestamp.

Users can restore quarantined files or purge quarantine to reclaim the disk space permanently.

## Protected Paths And Extensions

Users can add protected paths and protected file extensions. Protected entries are skipped even if another workflow discovers them.

Default protected extensions include common office documents, PDFs, and Keynote files.

## Restore Points

SpacePilot can request a Windows restore point before medium-risk cleanup. The app launches the Windows PowerShell checkpoint command and reports whether the request was started.

Restore points protect system settings, not every personal file. Quarantine is still the primary undo mechanism for cleaned files.

## Explicitly Out Of Scope

SpacePilot should not:

- Clean or repair the registry automatically.
- Delete personal user documents automatically.
- Disable startup entries automatically.
- Uninstall software automatically.
- Upload cleanup telemetry without explicit opt-in.
- Follow junctions or symbolic links during cleanup scanning.
