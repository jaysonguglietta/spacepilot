# FAQ

## Is SpacePilot Production Ready?

The core app workflows, safety model, Windows CI, automated service tests, release zip packaging, optional signing hooks, MSI scaffolding, accessibility metadata improvements, and QA matrix are implemented. Public distribution still needs signed installer artifacts, release signing secrets/certificate management, an auto-update channel, third-party accessibility review, and completed Windows 10/11 QA evidence.

## Is There A One-Click Installer?

Not yet. The current install path is to build from source on Windows. See [Installation](INSTALLATION.md).

## Does SpacePilot Clean The Registry?

No. SpacePilot does not automatically clean, repair, or rewrite the registry.

## Does SpacePilot Delete My Documents?

No automatic workflow targets broad personal folders such as Documents, Desktop, or Downloads. Large-file and duplicate workflows can show personal files, but cleanup requires explicit item selection and uses the quarantine pipeline.

## Why Use Quarantine?

Quarantine gives you a chance to restore files before permanent deletion. It is safer than immediate deletion, especially while testing cleanup rules.

## Why Did Disk Space Not Increase Immediately?

If quarantine is enabled, files are moved into SpacePilot quarantine. Purge quarantine from the Recovery view to permanently reclaim that space.

## Can SpacePilot Uninstall Apps?

No. SpacePilot shows installed apps and opens Windows Apps & Features so the user can make uninstall decisions through Windows.

## Can SpacePilot Disable Startup Items?

No. SpacePilot inventories startup entries and scheduled tasks, then routes changes through Windows controls.

## Does SpacePilot Send Telemetry?

No telemetry, analytics, cloud sync, or crash reporting is currently implemented.

## Where Are Receipts Stored?

Cleanup receipts are stored under:

```text
%LOCALAPPDATA%\SpacePilot\Receipts\
```

Receipts can include file paths because they are meant to document cleanup actions.

## Should I Run As Administrator?

Standard user mode is preferred for routine review. Administrator rights may be needed for some system temp locations or restore-point requests.
