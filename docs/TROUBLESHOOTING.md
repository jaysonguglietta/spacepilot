# Troubleshooting

## SpacePilot Does Not Build On Windows

Confirm the .NET SDK is installed:

```powershell
dotnet --info
```

Then restore and build:

```powershell
dotnet restore .\apps\windows\SpacePilot.sln
dotnet build .\apps\windows\SpacePilot.sln -c Release
```

The Windows app targets WPF, so build and runtime validation should happen on Windows.

## SpacePilot Does Not Build On macOS

Confirm Swift is available:

```bash
swift --version
```

Then build and validate:

```bash
swift build --package-path apps/macos/SpacePilotMac -c release
bash scripts/macos/validate-core.sh
```

If `swift test` fails with `no such module 'XCTest'`, install or select full Xcode. The local validation script does not require XCTest.

If SwiftPM reports `sandbox-exec: sandbox_apply: Operation not permitted` inside a restricted automation environment, run the same command in a normal Terminal session.

## Some Cleanup Items Fail

Common causes:

- The file is still in use by Windows or an app.
- The app needs administrator rights for that location.
- The item disappeared between scan and cleanup.
- A protected path or extension blocked cleanup.

Close related apps, rerun the scan, and review the Activity view for warnings.

## Browser Cleanup Misses Files

Close the browser before cleaning. Browsers often keep cache, cookies, history, and session files locked while running.

Cookies, history, and sessions are opt-in because they can sign users out or remove browsing context.

## Quarantine Did Not Free As Much Space As Expected

Quarantine moves files into SpacePilot's local quarantine folder. This improves undo safety, but disk space is fully reclaimed only after quarantine is purged.

Open **Recovery**, review quarantined items, then purge selected items or purge all when you are sure you do not need to restore them.

## Restore Point Request Fails

Restore-point requests depend on Windows System Protection and PowerShell availability. They may fail if:

- System Protection is disabled.
- The app is running without needed permissions.
- PowerShell is blocked by policy.
- Windows throttles restore-point creation.

Quarantine is still the main file-level recovery mechanism.

## WinGet Actions Do Not Work

Confirm WinGet is available:

```powershell
winget --version
```

If WinGet is missing or managed by organization policy, package update, export, and import actions may not work.

## Weekly Reminder Does Not Appear

The weekly reminder uses Windows Task Scheduler. If enabling fails, check:

- The app is running on Windows.
- Task Scheduler service is enabled.
- Security policy allows creating user-level scheduled tasks.

The task name is:

```text
SpacePilot Weekly Scan Reminder
```

## I Want To Reset SpacePilot

Close the app and delete:

```text
%LOCALAPPDATA%\SpacePilot\
~/Library/Application Support/SpacePilot/
```

This resets preferences, receipts, exports, and quarantine metadata. Do not reset this folder if you may need to restore quarantined files.
