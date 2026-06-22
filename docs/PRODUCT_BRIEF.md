# Product Brief

## 1. Target Users

SpacePilot is for Windows and macOS users who want to reclaim disk space, understand local cleanup opportunities, and review system maintenance without trusting a black-box cleanup tool.

Primary users:

- Everyday Windows and macOS users with low disk space.
- Power users who want transparent cleanup controls.
- IT helpers setting up or refreshing a personal PC.
- Windows users migrating to a new PC who need app inventory and WinGet export/import help.
- Mac users who want safe cleanup of user caches, logs, temp files, large files, and duplicates.

Secondary users:

- Developers and maintainers reviewing cleanup safety.
- Future release engineers packaging and signing the app.

## 2. Core Problem

Windows and macOS accumulate temporary files, application caches, logs, duplicates, and large forgotten files. Windows also has startup entries and outdated packages. Many cleanup tools solve this with opaque automation, aggressive defaults, or risky system claims.

SpacePilot solves the problem by making cleanup visible, reviewable, recoverable, and local-first.

## 3. Primary User Workflows

### Scan And Review Cleanup

1. User opens SpacePilot.
2. User reviews the first-run safety note.
3. User starts a scan.
4. SpacePilot lists cleanup candidates with path, category, risk, size, and age.
5. User filters, searches, and selects items.
6. User confirms cleanup.
7. SpacePilot quarantines or deletes according to preferences and writes activity/receipt data.

### Reclaim Personal Storage

1. User opens Storage.
2. User maps storage usage.
3. User scans large files or verified duplicates.
4. User reviews each file explicitly.
5. User quarantines selected files.
6. User restores or purges from Recovery later.

### Clean Browser Data Safely

1. User closes browsers.
2. User opens Browsers.
3. SpacePilot discovers profiles.
4. Cache cleanup is selected by default.
5. Cookies, history, and sessions require opt-in.
6. User runs cleanup and reviews warnings if files are locked.

### Maintain Software

1. User opens Software.
2. User searches installed app inventory.
3. User opens Windows Apps & Features for uninstall decisions.
4. User checks WinGet updates.
5. User updates selected packages or exports/imports a package list.

### Review Startup Impact

1. User opens Startup.
2. SpacePilot inventories registry startup items, startup-folder entries, and scheduled tasks.
3. User reviews impact guidance.
4. User changes startup behavior through Windows controls.

### Recover Or Audit

1. User opens Recovery.
2. User reviews quarantine entries and cleanup receipts.
3. User restores selected files, purges quarantine, or exports a receipt.

### Run On macOS

1. User launches the SwiftUI macOS app.
2. User scans approved user-owned cleanup roots.
3. User reviews temp, log, cache, Xcode, or SwiftPM candidates.
4. User quarantines selected items.
5. User restores or purges from Recovery.

## 4. Main Screens

- **Health**: summary of cleanup estimate, reclaim plan, inventory, and safety state.
- **Cleaner**: searchable/filterable cleanup candidate review.
- **Storage**: storage map, large files, verified duplicate files.
- **Browsers**: browser profile discovery and opt-in browser data cleanup.
- **Software**: installed app review and WinGet maintenance.
- **Startup**: startup registry/folder/task inventory and guidance.
- **Recovery**: quarantine restore/purge and receipt export.
- **Settings**: preferences, protected paths/extensions, restore-point and Windows maintenance checks.
- **Activity**: in-session log of important actions and warnings.

The macOS app uses the same top-level sections except Windows-only Software, Startup, and browser-history/session controls are omitted in the first Mac build.

## 5. Key Data Models

- `CleanupRule`: category, risk, age threshold, enabled state, cleanup locations.
- `CleanupLocation`: root path, pattern, recursion, age and directory handling.
- `CleanupCandidate`: candidate path, size, category, risk, timestamps, selected state.
- `CleanupReceipt`: cleanup run metadata, selected items, bytes affected, restore-point status.
- `QuarantineEntry`: original path, quarantine path, size, category, risk, timestamp.
- `UserPreferences`: confirmation, quarantine, restore-point reminder, thresholds, protected entries.
- `ProtectedPathEntry`: path and reason.
- `LargeFileInfo`: file metadata and recommendation.
- `DuplicateFileInfo`: duplicate group, hash, path, size.
- `InstalledAppInfo`: display name, publisher, version, install date, uninstall metadata.
- `WingetPackageInfo`: package id, installed version, available version, source.
- `StartupEntry`: source, command/path, publisher or task metadata, impact guidance.
- `SettingsCheck`: maintenance check name, status, details.
- `ActivityLogEntry`: timestamp, level, message.

## 6. Important Edge Cases

- Files are locked or removed between scan and cleanup.
- Cleanup paths resolve outside approved roots.
- Cleanup root folder itself is selected accidentally.
- Reparse points could point into unexpected locations.
- User selects personal large files or duplicates without recognizing them.
- Browser data cleanup could sign the user out or remove session state.
- Quarantine uses disk space until purged.
- Restore-point creation can fail due to Windows policy, permissions, throttling, or disabled System Protection.
- WinGet may be missing, blocked, offline, or require agreements.
- Non-Windows environments cannot run WPF or Windows maintenance APIs.
- Mac cleanup must avoid broad user folders and symbolic links.
- Mac distribution requires Developer ID signing and notarization before public release.
- Protected paths/extensions must override all cleanup workflows.
- Standard user mode may lack permission for system temp/log paths.

## 7. Assumptions

- The app is local-first and does not require a cloud backend.
- A source build is acceptable until a signed installer is created.
- Users value transparency and reversibility over one-click aggressive cleanup.
- WPF/.NET 8 is an appropriate Windows desktop target.
- SwiftUI/Swift Package Manager is an appropriate first macOS companion target.
- Windows controls remain the safest place for uninstall and startup-disable decisions.
- Quarantine is the default cleanup path for safer recovery.

## 8. Done For This Version

This version is done when:

- Users can install/run from source on Windows using documented commands.
- Users can scan, review, filter, select, and clean approved cleanup candidates.
- Cleanup actions are confirmed and recoverable through quarantine by default.
- Personal-file workflows require explicit selection.
- Browser cookies, history, and sessions are opt-in.
- Software, startup, settings, recovery, and activity workflows are real and not dead navigation.
- Docs explain installation, use, safety, privacy, troubleshooting, architecture, development, release, and brand.
- The codebase has a clear service/view-model structure for future production work.
- macOS users can build, validate, test when XCTest is available, and package the SwiftUI companion app locally.

Not done for public release:

- Public signed installer artifact.
- Public signed and notarized macOS artifact.
- Auto-update channel.
- Crash reporting with opt-in.
- Third-party accessibility audit.
- Completed Windows 10/11 QA evidence.
