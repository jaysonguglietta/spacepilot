# SpacePilot User Guide

SpacePilot helps reclaim disk space and review system maintenance areas. The app scans first and shows candidates for review. Personal-file quarantine always needs an explicit final review; routine-cleaner confirmation follows the platform's available safety settings.

## Recommended First Run

1. Open SpacePilot.
2. Review the first-run safety note.
3. Run **Scan** from the top bar.
4. Review the **Health** summary for cleanup estimate and safety status. Windows also shows a measured reclaim plan; macOS shows suggested steps.
5. Open **Cleaner** and inspect the candidate list before selecting anything for cleanup.
6. On Windows, keep quarantine enabled for routine cleanup. Personal-file actions and macOS cleanup always use quarantine; purge old quarantine entries separately from Recovery.

## Common Tasks

### Free Temporary And Cache Space

1. Click **Scan**.
2. Open **Cleaner**.
3. Review low-risk categories first.
4. Use category, risk, and search filters to narrow the list.
5. Select the items you want to clean.
6. Confirm cleanup.
7. Open **Recovery** if you need to restore anything.

### Reclaim Space From Large Files

1. Open **Storage**.
2. Set the large-file threshold in **Settings** if needed.
3. Run the large-file scan.
4. Review file names, paths, sizes, and recommendations.
5. Select only files you recognize and no longer need locally.
6. Quarantine selected files.
7. Purge quarantine later to permanently reclaim space.

### Review Duplicate Files

1. Open **Storage**.
2. Run the duplicate scan.
3. Review each hash-verified duplicate group and choose a keeper explicitly.
4. Select only copies you recognize as redundant. None should start selected.
5. Confirm that at least one verified copy remains outside the selected batch.
6. Quarantine selected duplicates.

### Clean Browser Cache

Windows build only in the current release:

1. Close browsers.
2. Open **Browsers**.
3. Refresh browser profiles.
4. Keep cache selected for routine cleanup.
5. Select supported cookies, history, or sessions only when you understand the sign-out and data-loss impact. Clear Firefox history in Firefox: its history database also contains bookmarks, so SpacePilot does not remove that database.
6. Run cleanup.

### Update Apps With WinGet

Windows build only:

1. Open **Software**.
2. Refresh WinGet updates.
3. Select packages to update.
4. Confirm the update action.

WinGet may contact configured package sources and may require package agreements.

### Review Startup Impact

Windows build only:

1. Open **Startup**.
2. Review registry, startup-folder, and scheduled-task entries.
3. Use the Windows controls linked from the app to disable or change startup behavior.

SpacePilot does not silently disable startup entries.

### Improve Responsiveness With RAM Assist

1. Open **Performance**.
2. Click **Refresh**.
3. Review memory used, available RAM, pressure, uptime, and process count.
4. Review **Top memory processes** for the largest apps.
5. Use **Task Manager** on Windows or **Activity Monitor** on macOS if you intentionally want to close an app.
6. Follow **Strong improvements** for startup, update, browser, storage, swap, or restart recommendations.

RAM Assist does not force-empty memory or kill processes. Windows and macOS intentionally use available RAM for cache; closing or restarting a real memory-heavy app is safer than fake RAM boosting.

### Use SpacePilot On macOS

1. Launch SpacePilot for macOS.
2. Review the first-run safety note.
3. Run **Scan** from the top bar.
4. Open **Cleaner** to review temp, log, cache, Xcode, and SwiftPM cleanup candidates.
5. Select only the items you want to move into quarantine.
6. Open **Storage** to scan large files or verified duplicates.
7. Open **Performance** to review RAM pressure, swap use, top memory apps, and login-item guidance.
8. Open **Recovery** to restore quarantined files or purge them after review.

The first macOS build omits Windows-only Software, Startup, restore-point, and browser-history/session workflows.

### Restore Or Permanently Purge Cleanup

1. Open **Recovery**.
2. Select quarantined items.
3. Choose restore if you need them back.
4. Choose purge when you are sure they are no longer needed.

Purging quarantine is the step that permanently reclaims the quarantined disk space.

## Cleaner

The Cleaner workflow scans approved temp, cache, report, log, and app-cache locations. Candidates show category, path, size, and risk; Windows also shows the modified date in its grid.

Use this workflow to:

- Filter by cleanup category on Windows.
- Filter by risk level.
- Search by name, path, or category.
- Select visible items, clear selection, or select individual items. Windows also provides select-all and category controls.
- Review the action wording and confirm when prompted; personal-file actions always require confirmation.

Low-risk items are safer defaults. Medium-risk items can include caches or logs that Windows or apps may recreate, but they should still be reviewed before cleanup.

## Storage Analyzer

The Storage view focuses on disk-space wins outside normal temp cleanup.

- **Map storage** on Windows summarizes top folders and file-type categories within the scanned personal roots.
- **Large files** finds large personal files above the configured threshold.
- **Duplicate files** groups files by size and verifies duplicates with SHA-256 hashes before presenting them.

Large-file and duplicate cleanup is explicit-item only. SpacePilot starts personal files unselected, requires a final review, and quarantines selected files even if routine Windows cleanup is configured for permanent deletion. Duplicate cleanup also requires a verified keeper in every selected group.

## Browsers

The Windows Browsers view discovers browser profiles and lets you choose supported data types to clean by profile.

Default behavior favors cache cleanup. Cookies, history, and session cleanup are opt-in because those can sign users out or remove browsing context.

Close browsers before cleaning for best results. Clear Firefox history with Firefox's own controls to preserve bookmarks.

## Software

The Software view helps audit installed apps and package updates.

- Search installed software inventory.
- Open Windows Apps & Features for uninstall decisions.
- Check WinGet package updates.
- Update selected WinGet packages.
- Export or import a WinGet package list for PC rebuilds.

SpacePilot does not silently uninstall apps.

## Startup

The Startup view reviews registry startup items, startup-folder entries, and scheduled tasks. It provides impact guidance and links users to Windows controls for changes.

SpacePilot does not silently disable startup entries.

## Performance

The Performance view contains RAM Assist.

Windows shows physical memory usage, available RAM, commit usage, uptime, process count, top processes by working set/private memory, and recommendations tied to memory pressure, startup load, WinGet updates, browser profiles, and restart cadence.

macOS shows physical memory usage, available RAM, swap usage, uptime, process count, top processes by resident memory, and recommendations tied to memory pressure, swap use, cleanup/storage pressure, restart cadence, and Login Items.

RAM Assist is advisory. It opens Task Manager, Resource Monitor, Power Settings, Activity Monitor, or Login Items so the user can make intentional changes through OS tools.

## Recovery

The Recovery view contains:

- Quarantine entries that can be restored or purged.
- Cleanup receipts that record what happened.
- On Windows, an export control for the latest receipt.

Quarantine improves undo safety. Disk space is fully reclaimed only after quarantine is purged.

## Settings

The Settings view contains cleanup preferences and Windows maintenance checks.

Windows preferences include:

- Confirm before cleanup.
- Remind before medium-risk cleanup to create a restore point.
- Use quarantine.
- Select medium-risk cleanup by default.
- Quarantine retention days.
- Large-file and duplicate thresholds.
- Protected paths and protected extensions.

The settings audit checks elevation, Storage Sense, temp folder access, browser cache access, restore-point command availability, and latest restore-point status.

On macOS, Settings focuses on supported cleanup thresholds and the protected-extension policy. The macOS app always confirms and quarantines cleanup; it does not include Windows maintenance checks or an automatic retention purge.

## Activity

The Activity view shows in-session scan, cleanup, warning, inventory, and maintenance events.

Use it as a quick operational trail while testing or reviewing a cleanup session.
