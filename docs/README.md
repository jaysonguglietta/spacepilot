# SpacePilot Documentation

SpacePilot is a local-first Windows and macOS cleanup utility. These docs explain how the apps are intended to work, what safety boundaries they enforce, and how to build toward production releases.

## Documentation Index

- [SpacePilot For Windows](windows/README.md): WPF app scope, build, package, and Windows-specific notes.
- [SpacePilot For macOS](macos/README.md): Native SwiftUI Mac app scope, build, and safety notes.
- [User Guide](USER_GUIDE.md): How to use the main workflows.
- [Product Brief](PRODUCT_BRIEF.md): Target users, workflows, data models, assumptions, and done criteria.
- [Installation](INSTALLATION.md): Install, run, update, and uninstall SpacePilot.
- [Safety Model](SAFETY_MODEL.md): Cleanup rules, quarantine, protected paths, and risky-operation boundaries.
- [Architecture](ARCHITECTURE.md): Project structure, service responsibilities, and data flow.
- [Development Guide](DEVELOPMENT.md): Local setup, build commands, validation, and coding conventions.
- [Continuous Integration](CI.md): GitHub Actions build, test, package, and optional signing workflow.
- [Release Checklist](RELEASE_CHECKLIST.md): Release, signing, installer, and QA requirements.
- [Signing And Installer](SIGNING_AND_INSTALLER.md): Release zip, Authenticode signing, and MSI scaffolding.
- [Windows QA Matrix](windows/QA_MATRIX.md): Manual release test coverage for Windows 10/11.
- [macOS QA Matrix](macos/QA_MATRIX.md): Manual release test coverage for macOS.
- [Product Quality Checklist](QUALITY_CHECKLIST.md): Product, safety, interaction, accessibility, engineering, and verification bar.
- [Privacy](PRIVACY.md): What data stays local and what actions may call external tools.
- [Troubleshooting](TROUBLESHOOTING.md): Common build, cleanup, quarantine, WinGet, and reminder issues.
- [FAQ](FAQ.md): Direct answers to common product and safety questions.
- [Brand](BRAND.md): Name, icon, mark, and brand positioning.

## Repository Layout

```text
apps/windows/     Windows WPF app, tests, solution, and WiX packaging
apps/macos/       macOS SwiftUI app and app-bundle packaging
scripts/windows/  Windows package, signing, and MSI scripts
scripts/macos/    macOS validation and app-bundle scripts
scripts/shared/   Cross-platform brand asset generation
docs/windows/     Windows-specific docs and QA matrix
docs/macos/       macOS-specific docs and QA matrix
```

## Current Status

The repository contains a WPF desktop app targeting .NET 8 on Windows and a native SwiftUI companion app for macOS. It includes the main cleanup workflows, production-oriented safety features, Windows and macOS CI, automated service tests, local macOS core validation, release zip/app-bundle packaging, optional Windows signing hooks, MSI scaffolding, accessibility metadata improvements, and QA matrices for both platforms. A public release still needs signed Windows installer artifacts, Developer ID signed and notarized macOS artifacts, release signing secret/certificate management, an auto-update channel, third-party accessibility review, and completed QA evidence.
