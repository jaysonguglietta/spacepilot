# SpacePilot

SpacePilot is a safety-first Windows desktop cleanup utility inspired by CCleaner, but designed around transparency instead of black-box automation. It scans known cleanup locations, shows every candidate before deletion, and keeps software/startup management routed through Windows controls.

## Product Scope

This version is a local-only WPF desktop app for Windows users who want to reclaim disk space and understand what is installed or launching at startup without risking registry damage or silent app changes.

## Documentation

Detailed docs live in [`docs/`](docs/README.md):

- [User Guide](docs/USER_GUIDE.md)
- [Installation](docs/INSTALLATION.md)
- [Safety Model](docs/SAFETY_MODEL.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Development Guide](docs/DEVELOPMENT.md)
- [Release Checklist](docs/RELEASE_CHECKLIST.md)
- [Privacy](docs/PRIVACY.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [FAQ](docs/FAQ.md)
- [Brand](docs/BRAND.md)

## Install / Run

There is no signed public installer yet. To run SpacePilot today, build it from source on Windows:

```powershell
git clone git@github.com:jaysonguglietta/spacepilot.git
cd spacepilot
dotnet restore .\SpacePilot.sln
dotnet build .\SpacePilot.sln -c Release
dotnet run --project .\src\SpacePilot\SpacePilot.csproj -c Release
```

For a local runnable folder:

```powershell
dotnet publish .\src\SpacePilot\SpacePilot.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\publish\SpacePilot
.\artifacts\publish\SpacePilot\SpacePilot.exe
```

See [Installation](docs/INSTALLATION.md) for update, uninstall, and future installer guidance.

## Quick Use

1. Launch SpacePilot.
2. Review the first-run safety note.
3. Click **Scan**.
4. Review **Health** for cleanup estimate and recommended reclaim plan.
5. Open **Cleaner** to filter and select cleanup candidates.
6. Keep quarantine enabled for safer cleanup.
7. Use **Recovery** to restore quarantined files or purge quarantine when you are ready to reclaim the space permanently.

## Core Workflows

- First-run safety note explains what the app will and will not change.
- Scan known temp/cache locations and build an item-level cleanup review.
- Search cleanup candidates by name, category, or path.
- Filter cleanup candidates by category and risk level.
- Select all, select visible, clear selection, or toggle individual cleanup items.
- Confirm deletion and optionally request a restore point before medium-risk cleanup.
- Move cleanup items into quarantine by default, then restore or purge them later.
- Export JSON cleanup receipts with before/after free space and restore-point status.
- Review a recommended reclaim plan that groups low-risk cleanup, quarantine purge, duplicates, large files, and startup efficiency.
- Map storage by top folders and file-type categories.
- Find large files in user storage folders and quarantine only the files the user selects.
- Find verified duplicate files using size grouping plus SHA-256 hashing.
- Review installed software with live search, then open Windows Apps & Features for removal.
- Check WinGet package updates, update selected packages, export an app list, or import an app list for PC rebuilds.
- Discover browser profiles and select cache, cookies, history, or session cleanup per profile.
- Review registry, startup-folder, and scheduled-task startup entries with impact guidance.
- Open Windows Storage settings, Disk Cleanup, and Recycle Bin from the Settings workflow.
- Add protected paths and protected file extensions that cleanup workflows must skip.
- Enable or disable a weekly Windows Task Scheduler reminder to run the app.
- Audit Windows maintenance settings such as elevation, Storage Sense, temp folder health, browser cache access, restore-point command availability, and latest restore-point status.
- Persist cleanup category selections and safety preferences in local app data.
- Track scan, cleanup, warning, and inventory events in an in-session activity log.

## Safety Boundaries

- No automatic registry cleaning.
- No silent uninstalling or startup disabling.
- No blind deletion of user documents, downloads, desktop files, or application data roots.
- No deletion outside approved cleanup roots.
- No deletion of cleanup root folders themselves.
- Reparse points are skipped to avoid traversing links into unexpected locations.
- Personal large-file and duplicate cleanup uses explicit item selection and the same quarantine/receipt pipeline.
- Quarantine improves undo safety, but disk space is only fully reclaimed after the user purges quarantine.
- Browser cookies, history, and sessions are opt-in per profile.
- WinGet import can install applications and requires explicit user confirmation.
- Protected paths and extensions are skipped even when selected elsewhere.

## Build On Windows

Install the .NET 8 SDK on Windows, then run:

```powershell
dotnet build .\SpacePilot.sln
dotnet run --project .\src\SpacePilot\SpacePilot.csproj
```

The project targets WPF, so it must be built on Windows.

## Data Location

User preferences, quarantine data, and cleanup receipts are stored under:

```text
%LOCALAPPDATA%\SpacePilot\
```

Key files:

- `preferences.json` stores user preferences.
- `Quarantine\manifest.json` tracks recoverable quarantined items.
- `Receipts\*.json` stores cleanup receipts.
- `Winget\packages-*.json` stores exported app lists.

## Brand Assets

The app uses a shield-and-sweep logo mark that signals safe cleanup, reclaimed space, and verified maintenance.

- `src\SpacePilot\Assets\AppLogoMark.svg` is the editable source mark.
- `src\SpacePilot\Assets\AppIcon.ico` is the generated multi-size Windows application icon.
- `scripts\generate-brand-assets.mjs` regenerates the `.ico` from the same design language.

## Production Release Checklist

The app-level production features are implemented in this repository. A public release still needs:

1. Branded installer artwork and setup flow.
2. Code signing certificate and signed MSIX/MSI artifacts.
3. CI build and release pipeline on Windows.
4. Auto-update channel.
5. Crash reporting and diagnostics with explicit opt-in.
6. Windows 10/11 test matrix.
7. Full accessibility and localization pass.
8. MSIX/MSI auto-update channel and release signing verification.

## Safety Notes

Cleanup tools can cause damage if they delete the wrong thing. The engine keeps every candidate tied to an approved cleanup root and refuses to delete root folders directly. Any future cleanup rule should be reviewed with the same caution.
