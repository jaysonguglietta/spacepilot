# Release Checklist

SpacePilot has the core app workflows in place. A public production release still needs packaging, signing, QA, and support infrastructure.

## Build

- Confirm app version and release notes.
- Confirm GitHub Actions is passing on `main`.
- Build on Windows with the .NET 8 SDK.
- Build on macOS with Swift Package Manager.
- Run automated tests and macOS core validation.
- Produce Windows and macOS release artifacts.
- Verify the executable uses the SpacePilot icon and product metadata.
- Confirm `%LOCALAPPDATA%\SpacePilot\` is used for app data.
- Confirm `~/Library/Application Support/SpacePilot/` is used for macOS app data.

## Installer

- Choose MSIX, MSI, or another Windows installer format.
- Build the MSI with `scripts\windows\build-msi.ps1` when WiX is available.
- Build the macOS DMG with `scripts/macos/build-dmg.sh`.
- Add branded installer artwork.
- Include license and privacy text.
- Verify install, repair, upgrade, and uninstall flows.
- Verify app shortcuts and Start menu entries.
- Verify macOS Applications install, launch, quarantine warning, and removal flows.

## Signing

- Obtain a code signing certificate.
- Sign the executable and installer.
- Configure `SPACEPILOT_SIGNING_CERT_BASE64` and `SPACEPILOT_SIGNING_CERT_PASSWORD` only in trusted GitHub release environments.
- Verify signatures on a clean Windows machine.
- Obtain Apple Developer ID signing credentials.
- Sign and notarize macOS artifacts.
- Verify notarization and Gatekeeper behavior on a clean Mac.
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
- Complete [Windows QA Matrix](windows/QA_MATRIX.md).
- Test on macOS 26.5.1 (25F80).
- Test macOS source run, app bundle launch, quarantine, restore, purge, large files, duplicates, and settings persistence.
- Complete [macOS QA Matrix](macos/QA_MATRIX.md).

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
- Screen reader names for navigation, primary actions, data grids, and progress.
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
- Push a version tag such as `v0.1.0` or run **Actions > Release Packages**.
- Verify release assets include Windows and macOS compiled packages plus `.sha256` checksums.
- Verify the macOS DMG opens and supports drag-and-drop install to Applications.
- Attach signed installer artifacts.
- Attach notarized macOS artifacts.
- Publish checksums.
- Tag the release.
- Keep a rollback path for installer and auto-update issues.
