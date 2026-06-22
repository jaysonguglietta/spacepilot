# Release Checklist

SpacePilot has the core app workflows in place. A public production release still needs packaging, signing, QA, and support infrastructure.

## Build

- Confirm app version and release notes.
- Build on Windows with the .NET 8 SDK.
- Produce Release artifacts.
- Verify the executable uses the SpacePilot icon and product metadata.
- Confirm `%LOCALAPPDATA%\SpacePilot\` is used for app data.

## Installer

- Choose MSIX, MSI, or another Windows installer format.
- Add branded installer artwork.
- Include license and privacy text.
- Verify install, repair, upgrade, and uninstall flows.
- Verify app shortcuts and Start menu entries.

## Signing

- Obtain a code signing certificate.
- Sign the executable and installer.
- Verify signatures on a clean Windows machine.
- Document certificate renewal and storage process.

## QA

- Test on supported Windows 10 and Windows 11 versions.
- Test standard user and administrator launch paths.
- Test systems with Storage Sense enabled and disabled.
- Test with common browsers installed and not installed.
- Test WinGet missing, installed, and partially failing.
- Test restore-point command availability and failure paths.
- Test cleanup while target apps are open.
- Test quarantine restore and purge after restart.

## Safety Review

- Review all cleanup roots.
- Confirm root folders cannot be deleted directly.
- Confirm reparse points are skipped.
- Confirm protected paths and extensions are honored.
- Confirm medium-risk and high-risk operations require explicit selection.
- Confirm no registry cleaning exists.

## Accessibility

- Keyboard-only navigation.
- Focus states.
- Screen reader names for controls.
- Color contrast.
- Text wrapping and scaling.

## Privacy And Diagnostics

- Keep local-only behavior by default.
- Add crash reporting only with explicit opt-in.
- Document any external commands or network activity.
- Provide a way to export diagnostic information without exposing personal files.

## Release Operations

- Create GitHub release notes.
- Attach signed installer artifacts.
- Publish checksums.
- Tag the release.
- Keep a rollback path for installer and auto-update issues.
