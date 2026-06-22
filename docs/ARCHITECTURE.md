# Architecture

SpacePilot has two native desktop implementations that share the same product philosophy:

- A Windows WPF application targeting `.NET 8`.
- A macOS SwiftUI companion app built with Swift Package Manager.

Both implementations keep platform APIs behind service boundaries and route file-changing behavior through explicit safety checks, quarantine, and receipt records.

## Project Layout

```text
apps/windows/SpacePilot.sln
apps/windows/src/SpacePilot/
  App.xaml
  MainWindow.xaml
  Models/
  Services/
  ViewModels/
  Converters/
  Utilities/
  Assets/
apps/macos/SpacePilotMac/
  Package.swift
  Sources/SpacePilotMac/
  Tests/SpacePilotMacTests/
  Validation/
scripts/
docs/
```

## Windows UI Layer

`MainWindow.xaml` defines the main shell and all top-level product sections:

- Health
- Cleaner
- Storage
- Browsers
- Software
- Startup
- Recovery
- Settings
- Activity

`MainViewModel` coordinates commands, collections, summaries, preferences, and status messages. Smaller view models wrap individual cleanup candidates, browser profiles, WinGet packages, storage items, duplicate files, quarantine entries, and protected paths.

## Windows Service Layer

Services keep platform and workflow logic out of the XAML.

- `CleanerService`: scans cleanup rules and creates cleanup candidates.
- `CleanupRuleCatalog`: defines built-in cleanup categories and approved roots.
- `PathSafety`: enforces cleanup target boundaries.
- `ProtectionPolicyService`: applies protected paths and extensions.
- `QuarantineService`: moves, restores, manifests, and purges quarantined files.
- `CleanupReceiptService`: writes and reads cleanup receipt JSON.
- `StorageAnalysisService`: maps storage, finds large files, and hashes duplicates.
- `BrowserProfileService`: discovers browser profiles and cleanup targets.
- `SoftwareInventoryService`: reads installed app inventory.
- `WingetService`: wraps WinGet upgrade, export, and import commands.
- `StartupService`: reads startup registry, startup folder, and scheduled-task entries.
- `SettingsAuditService`: checks Windows maintenance state.
- `SystemRestoreService`: requests and reads Windows restore-point status.
- `ScheduledScanService`: manages the weekly scan reminder task.
- `PreferencesService`: persists user preferences.
- `ReclaimPlanService`: builds recommended reclaim actions.

## macOS UI Layer

`SpacePilotMacApp` hosts a SwiftUI `NavigationSplitView` shell with these sections:

- Health
- Cleaner
- Storage
- Recovery
- Settings
- Activity

`MacAppState` owns observable state, commands, selections, preferences, activity messages, and summaries. SwiftUI views render the review and recovery workflows while leaving file operations in services.

## macOS Service Layer

The macOS service files live under `apps/macos/SpacePilotMac/Sources/SpacePilotMac/`:

- `MacCleanupRuleCatalog`: defines user-owned temp, log, cache, Xcode, and SwiftPM cleanup rules.
- `MacPathSafety`: prevents cleanup root deletion, sibling-prefix traversal, and paths outside approved roots.
- `MacCleanerService`: scans approved cleanup roots and skips symbolic links.
- `QuarantineService`: moves, restores, manifests, and purges quarantined files.
- `ReceiptService`: writes cleanup receipt JSON.
- `StorageAnalysisService`: finds large files and SHA-256 verified duplicates in personal folders.
- `PreferencesService`: persists macOS cleanup preferences.

## Data Flow

Most workflows follow the same pattern:

1. A command in `MainViewModel` starts a busy operation.
2. A service scans or performs the requested action.
3. Results are converted into observable view models.
4. Summaries, command states, and activity log entries are refreshed.
5. If files are changed, quarantine and receipt services record recoverability and audit details.

## Local Data

Windows stores user data under:

```text
%LOCALAPPDATA%\SpacePilot\
```

Important subpaths:

- `preferences.json`
- `Quarantine\manifest.json`
- `Receipts\*.json`
- `Winget\packages-*.json`

macOS stores user data under:

```text
~/Library/Application Support/SpacePilot/
```

Important subpaths:

- `preferences-macos.json`
- `Quarantine/manifest.json`
- `Receipts/*.json`

## Platform Boundaries

Windows WPF build and runtime validation should happen on Windows. macOS SwiftUI build and runtime validation should happen on macOS. Shared product rules should remain explicit in platform-specific service layers rather than hidden in broad automation.
