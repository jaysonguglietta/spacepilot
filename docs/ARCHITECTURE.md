# Architecture

SpacePilot is a WPF desktop application targeting `.NET 8` with Windows-specific APIs behind service boundaries.

## Project Layout

```text
SpacePilot.sln
src/SpacePilot/
  App.xaml
  MainWindow.xaml
  Models/
  Services/
  ViewModels/
  Converters/
  Utilities/
  Assets/
scripts/
docs/
```

## UI Layer

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

## Service Layer

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

## Data Flow

Most workflows follow the same pattern:

1. A command in `MainViewModel` starts a busy operation.
2. A service scans or performs the requested action.
3. Results are converted into observable view models.
4. Summaries, command states, and activity log entries are refreshed.
5. If files are changed, quarantine and receipt services record recoverability and audit details.

## Local Data

SpacePilot stores user data under:

```text
%LOCALAPPDATA%\SpacePilot\
```

Important subpaths:

- `preferences.json`
- `Quarantine\manifest.json`
- `Receipts\*.json`
- `Winget\packages-*.json`

## Platform Boundaries

The project targets WPF and Windows-specific APIs. Build and runtime validation should happen on Windows, even though simple text, XML, and script validation can run elsewhere.
