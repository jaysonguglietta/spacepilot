# FAQ

## Is SpacePilot Production Ready?

The core app workflows and automated service checks exist. A new public release still requires configured Windows and Apple signing credentials, notarization, protected release settings, and recorded clean-device and accessibility QA. The release workflow is designed to refuse unsigned artifacts; existing releases need separate signature verification.

## Is There A One-Click Installer?

There are compiled GitHub Release downloads. Windows users can install from an installer ZIP containing an MSI, and macOS users can install from a DMG. Verify the signature and notarization status of the particular release before installation. See [Installation](INSTALLATION.md).

## Does SpacePilot Clean The Registry?

No. SpacePilot does not automatically clean, repair, or rewrite the registry.

## Does SpacePilot Delete My Documents?

No automatic cleanup targets broad personal folders such as Documents, Desktop, or Downloads. Large-file and duplicate scans can show personal files; they start unselected, require a final review, and only move selected files into quarantine. Duplicate cleanup requires a chosen keeper for every selected group.

## Why Use Quarantine?

Quarantine gives you a chance to restore files before permanent deletion. It is safer than immediate deletion, especially while testing cleanup rules.

## Why Did Disk Space Not Increase Immediately?

If quarantine is enabled, files are moved into SpacePilot quarantine. Purge quarantine from the Recovery view to permanently reclaim that space.

## Can SpacePilot Uninstall Apps?

No. On Windows, SpacePilot shows installed apps and opens Windows Apps & Features so the user can make uninstall decisions through Windows. The first macOS build does not include app uninstall workflows.

## Can SpacePilot Disable Startup Items?

No. On Windows, SpacePilot inventories startup entries and scheduled tasks, then routes changes through Windows controls. The first macOS build does not include startup-item management.

## Can SpacePilot Free Up RAM?

SpacePilot includes RAM Assist, which shows memory pressure, available and used RAM, swap or commit usage, uptime, top memory processes, and safe recommendations. It does not force-clear RAM or kill apps automatically because modern Windows and macOS use available RAM for cache and can become slower if memory is purged blindly.

If RAM pressure is high, use Task Manager on Windows or Activity Monitor on macOS to intentionally close or restart memory-heavy apps.

## Does SpacePilot Send Telemetry?

No telemetry, analytics, cloud sync, or crash reporting is currently implemented.

## Where Are Receipts Stored?

Cleanup receipts are stored under:

```text
%LOCALAPPDATA%\SpacePilot\Receipts\
```

On macOS:

```text
~/Library/Application Support/SpacePilot/Receipts/
```

Receipts can include file paths because they are meant to document cleanup actions.

## Should I Run As Administrator?

Standard user mode is preferred for routine review. On Windows, administrator rights may be needed for some system temp locations or restore-point requests. On macOS, the first build intentionally focuses on user-owned cleanup locations.
