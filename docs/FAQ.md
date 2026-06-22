# FAQ

## Is SpacePilot Production Ready?

The core app workflows, safety model, Windows and macOS CI, automated service tests, macOS core validation, release ZIP/MSI/DMG packaging, optional Windows signing hooks, accessibility metadata improvements, and QA matrices are implemented. Public distribution still needs signed Windows installer artifacts, Developer ID signed and notarized macOS artifacts, release signing secrets/certificate management, an auto-update channel, third-party accessibility review, and completed QA evidence.

## Is There A One-Click Installer?

There are compiled GitHub Release downloads. Windows users can install from an installer ZIP that contains an MSI, and macOS users can install from a DMG. Fully signed Windows installers and notarized macOS releases are still future production hardening items. See [Installation](INSTALLATION.md).

## Does SpacePilot Clean The Registry?

No. SpacePilot does not automatically clean, repair, or rewrite the registry.

## Does SpacePilot Delete My Documents?

No automatic workflow targets broad personal folders such as Documents, Desktop, or Downloads. Large-file and duplicate workflows can show personal files, but cleanup requires explicit item selection and uses the quarantine pipeline.

## Why Use Quarantine?

Quarantine gives you a chance to restore files before permanent deletion. It is safer than immediate deletion, especially while testing cleanup rules.

## Why Did Disk Space Not Increase Immediately?

If quarantine is enabled, files are moved into SpacePilot quarantine. Purge quarantine from the Recovery view to permanently reclaim that space.

## Can SpacePilot Uninstall Apps?

No. On Windows, SpacePilot shows installed apps and opens Windows Apps & Features so the user can make uninstall decisions through Windows. The first macOS build does not include app uninstall workflows.

## Can SpacePilot Disable Startup Items?

No. On Windows, SpacePilot inventories startup entries and scheduled tasks, then routes changes through Windows controls. The first macOS build does not include startup-item management.

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
