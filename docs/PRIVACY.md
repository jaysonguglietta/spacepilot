# Privacy

SpacePilot is designed as a local-first utility. Cleanup scans, preferences, quarantine manifests, receipts, and activity logs are stored locally.

## Local Data

Windows writes user data under:

```text
%LOCALAPPDATA%\SpacePilot\
```

macOS writes user data under:

```text
~/Library/Application Support/SpacePilot/
```

Stored data can include:

- Preferences.
- Cleanup category selections.
- Protected paths and extensions.
- Quarantine metadata.
- Cleanup receipts.
- Exported WinGet package lists.

Receipts and manifests can include local file paths because they are needed for restore and audit workflows.

RAM Assist samples process names, process IDs, executable paths when the OS allows them, memory counters, uptime, swap or commit usage, and process counts. This data is shown in the current app session and may be summarized in the in-session activity log, but it is not uploaded.

## External Activity

Most workflows are local. Some Windows actions call platform tools:

- WinGet update, export, and import commands may contact configured package sources.
- Windows restore-point checks and requests use PowerShell.
- Windows settings buttons open local Windows settings panels or tools.
- RAM Assist buttons open local tools such as Task Manager, Resource Monitor, Power Settings, Activity Monitor, or Login Items.

The first macOS build does not call package managers, browser sync services, or cloud APIs. RAM Assist uses local macOS tools such as `vm_stat`, `ps`, and `sysctl`.

## Telemetry

The current app does not implement telemetry, analytics, cloud sync, or crash reporting.

If diagnostics are added later, they should be opt-in, documented clearly, and avoid collecting personal file contents.

## User Control

Users can remove local SpacePilot data by uninstalling the app and deleting:

```text
%LOCALAPPDATA%\SpacePilot\
~/Library/Application Support/SpacePilot/
```

Deleting this folder removes preferences, receipts, and quarantine metadata. Do not delete quarantine data if you may need to restore cleaned files.

## Uninstall Notes

For Windows MSI installs, uninstall SpacePilot through Windows Apps & Features. For source or local publish builds, uninstalling means closing SpacePilot and deleting the repository, publish folder, or local macOS app bundle. Uninstalling removes installed app files while leaving user data removal as an explicit user choice.
