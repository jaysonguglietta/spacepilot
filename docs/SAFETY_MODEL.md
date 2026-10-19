# Safety Model

SpacePilot is designed around transparent cleanup and conservative defaults. The app should never behave like a black-box registry cleaner or silent uninstaller.

## Core Principles

- Scan before cleanup.
- Show every cleanup candidate before action.
- Require explicit user approval.
- Prefer quarantine over immediate deletion.
- Avoid automatic registry changes.
- Avoid silent uninstalling or startup disabling.
- Avoid fake RAM boosting, force-emptying memory, or automatic process termination.
- Never clean broad user-content roots such as Documents, Desktop, Downloads, or application data roots.

## Approved Cleanup Roots

Cleanup candidates must come from known cleanup rules. Rules define approved roots, search patterns, age thresholds, recursion behavior, and risk level.

Examples include:

- User temp files.
- macOS user temporary files.
- macOS user logs.
- macOS user app caches.
- Xcode DerivedData.
- Swift Package Manager cache.
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

Before a candidate can be cleaned, path safety checks require it to be inside its approved root and refuse the root itself. The mutation service also rechecks a candidate after the scan. Windows whole-directory cleanup is held back until protected descendants and concurrent changes can be handled safely.

SpacePilot skips Windows reparse points and macOS symbolic links. The macOS file move opens and checks directory components without following links; a changed scanned file must be reviewed again.

## Risk Levels

Low-risk items are generally temporary files, reports, and logs that are less likely to affect user state.

Medium-risk items may include caches, system temp folders, Windows Update cache, or diagnostic logs that can be useful during troubleshooting.

High-risk items are used for explicit personal-file workflows such as large-file and duplicate review. These start unselected, require direct item selection and a final confirmation, and always use quarantine. Duplicate groups require a chosen keeper and a fresh verification before any copy moves.

## Quarantine

On Windows, quarantine moves selected files into `%LOCALAPPDATA%\SpacePilot\Quarantine\`.

On macOS, quarantine moves selected files into `~/Library/Application Support/SpacePilot/Quarantine/`.

Both implementations record a manifest entry with the original path, size, category, and timestamp. The reviewed candidate carries its risk level before cleanup.

Users can restore quarantined files or purge quarantine to reclaim the disk space permanently.

## Protected Paths And Extensions

Windows users can add protected paths and file extensions. Protected entries are skipped even if another workflow discovers them. macOS uses its protected-extension and path policy, but the first Mac settings screen does not expose a path editor.

Default protected extensions include common office documents, PDFs, and Keynote files.

## Restore Points

SpacePilot for Windows can request a restore point before medium-risk cleanup. The app launches the Windows PowerShell checkpoint command and reports whether the request was started.

Restore points protect system settings, not every personal file. Quarantine is still the primary undo mechanism for cleaned files.

The first macOS build does not modify system settings or create system snapshots.

## RAM Assist Safety

RAM Assist is read-only and advisory. It can show memory pressure, available RAM, swap or commit usage, uptime, and top memory processes. It may open Task Manager, Resource Monitor, Power Settings, Activity Monitor, or Login Items.

SpacePilot does not:

- Force-empty standby memory.
- Run macOS `purge`.
- Kill or quit processes automatically.
- Promise that free RAM alone improves performance.

Modern operating systems use available RAM for useful cache. SpacePilot recommends safer actions such as restarting a stuck app, trimming startup/login items, updating apps, cleaning disk cache, or restarting after long uptime.

## Explicitly Out Of Scope

SpacePilot should not:

- Clean or repair the registry automatically.
- Delete personal user documents automatically.
- Force-purge RAM or terminate processes automatically.
- Disable startup entries automatically.
- Uninstall software automatically.
- Upload cleanup telemetry without explicit opt-in.
- Follow junctions or symbolic links during cleanup scanning.
