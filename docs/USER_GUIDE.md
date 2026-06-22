# SpacePilot User Guide

SpacePilot helps reclaim disk space and review system maintenance areas without making silent destructive changes. The app scans first, shows candidates, and asks for explicit approval before cleanup.

## Recommended First Run

1. Open SpacePilot.
2. Review the first-run safety note.
3. Run **Scan** from the top bar.
4. Review the **Health** summary for cleanup estimate, reclaim plan, and safety status.
5. Open **Cleaner** and inspect the candidate list before selecting anything for cleanup.
6. Keep quarantine enabled unless you are intentionally purging old quarantine data.

## Cleaner

The Cleaner workflow scans approved temp, cache, report, log, and app-cache locations. Each candidate includes category, path, size, age, and risk.

Use this workflow to:

- Filter by cleanup category.
- Filter by risk level.
- Search by name, path, or category.
- Select all, select visible, clear selection, or select individual items.
- Confirm cleanup before files are moved to quarantine or deleted.

Low-risk items are safer defaults. Medium-risk items can include caches or logs that Windows or apps may recreate, but they should still be reviewed before cleanup.

## Storage Analyzer

The Storage view focuses on disk-space wins outside normal temp cleanup.

- **Map storage** summarizes top folders and file-type categories.
- **Large files** finds large personal files above the configured threshold.
- **Duplicate files** groups files by size and verifies duplicates with SHA-256 hashes before presenting them.

Large-file and duplicate cleanup is explicit-item only. SpacePilot does not automatically choose personal documents, media, or downloads for removal.

## Browsers

The Browsers view discovers browser profiles and lets you choose what to clean by profile.

Default behavior favors cache cleanup. Cookies, history, and session cleanup are opt-in because those can sign users out or remove browsing context.

Close browsers before cleaning for best results.

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

## Recovery

The Recovery view contains:

- Quarantine entries that can be restored or purged.
- Cleanup receipts that record what happened.
- Export controls for the latest receipt.

Quarantine improves undo safety. Disk space is fully reclaimed only after quarantine is purged.

## Settings

The Settings view contains cleanup preferences and Windows maintenance checks.

Important preferences:

- Confirm before cleanup.
- Remind before medium-risk cleanup to create a restore point.
- Use quarantine.
- Select medium-risk cleanup by default.
- Quarantine retention days.
- Large-file and duplicate thresholds.
- Protected paths and protected extensions.

The settings audit checks elevation, Storage Sense, temp folder access, browser cache access, restore-point command availability, and latest restore-point status.

## Activity

The Activity view shows in-session scan, cleanup, warning, inventory, and maintenance events.

Use it as a quick operational trail while testing or reviewing a cleanup session.
