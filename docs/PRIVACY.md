# Privacy

SpacePilot is designed as a local-first utility. Cleanup scans, preferences, quarantine manifests, receipts, and activity logs are stored locally.

## Local Data

SpacePilot writes user data under:

```text
%LOCALAPPDATA%\SpacePilot\
```

Stored data can include:

- Preferences.
- Cleanup category selections.
- Protected paths and extensions.
- Quarantine metadata.
- Cleanup receipts.
- Exported WinGet package lists.

Receipts and manifests can include local file paths because they are needed for restore and audit workflows.

## External Activity

Most workflows are local. Some actions call Windows tools:

- WinGet update, export, and import commands may contact configured package sources.
- Windows restore-point checks and requests use PowerShell.
- Windows settings buttons open local Windows settings panels or tools.

## Telemetry

The current app does not implement telemetry, analytics, cloud sync, or crash reporting.

If diagnostics are added later, they should be opt-in, documented clearly, and avoid collecting personal file contents.

## User Control

Users can remove local SpacePilot data by uninstalling the app and deleting:

```text
%LOCALAPPDATA%\SpacePilot\
```

Deleting this folder removes preferences, receipts, and quarantine metadata. Do not delete quarantine data if you may need to restore cleaned files.

## Uninstall Notes

For current source or local publish builds, uninstalling means closing SpacePilot and deleting the repository or publish folder. Future installer builds should remove installed app files through Windows Apps & Features while leaving user data removal as an explicit user choice.
