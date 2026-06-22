# SpacePilot Documentation

SpacePilot is a local-first Windows cleanup and optimization utility. These docs explain how the app is intended to work, what safety boundaries it enforces, and how to build toward a production release.

## Documentation Index

- [User Guide](USER_GUIDE.md): How to use the main workflows.
- [Safety Model](SAFETY_MODEL.md): Cleanup rules, quarantine, protected paths, and risky-operation boundaries.
- [Architecture](ARCHITECTURE.md): Project structure, service responsibilities, and data flow.
- [Development Guide](DEVELOPMENT.md): Local setup, build commands, validation, and coding conventions.
- [Release Checklist](RELEASE_CHECKLIST.md): Windows release, signing, installer, and QA requirements.
- [Privacy](PRIVACY.md): What data stays local and what actions may call external tools.
- [Brand](BRAND.md): Name, icon, mark, and brand positioning.

## Current Status

The repository contains a WPF desktop app targeting .NET 8 on Windows. It includes the main product workflows and production-oriented safety features, but still needs a Windows build pipeline, installer, signing, auto-update channel, accessibility review, and broad Windows 10/11 QA before public distribution.
