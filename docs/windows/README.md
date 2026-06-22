# SpacePilot For Windows

SpacePilot for Windows is the WPF/.NET desktop app. It focuses on safe disk cleanup, storage review, browser cache cleanup, software inventory, WinGet maintenance, startup review, quarantine, receipts, and Windows maintenance checks.

## App Location

```text
apps/windows/
```

Key paths:

- `apps/windows/SpacePilot.sln`
- `apps/windows/src/SpacePilot/SpacePilot.csproj`
- `apps/windows/tests/SpacePilot.Tests/SpacePilot.Tests.csproj`
- `apps/windows/packaging/wix/`

## Run From Source

From the repository root on Windows:

```powershell
dotnet restore .\apps\windows\SpacePilot.sln
dotnet build .\apps\windows\SpacePilot.sln -c Release
dotnet run --project .\apps\windows\src\SpacePilot\SpacePilot.csproj -c Release
```

## Test

```powershell
dotnet test .\apps\windows\tests\SpacePilot.Tests\SpacePilot.Tests.csproj -c Release
```

## Package

Create a release zip:

```powershell
.\scripts\windows\package-spacepilot.ps1 -Configuration Release -Runtime win-x64 -SkipSigning
```

Create an MSI when WiX is installed:

```powershell
.\scripts\windows\build-msi.ps1 -SkipSigning
```

## Local Data

Windows stores preferences, quarantine metadata, receipts, and WinGet exports under:

```text
%LOCALAPPDATA%\SpacePilot\
```

## Platform Notes

- The app targets WPF and `net8.0-windows10.0.19041.0`.
- Build and runtime validation should happen on Windows.
- Public release still needs signed installer artifacts and completed Windows QA evidence.
