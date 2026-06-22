# Installation

SpacePilot currently supports source builds for Windows and macOS. A signed Windows installer and a notarized macOS release artifact have not been published yet, so the supported path today is to build locally from source.

## Requirements

Windows:

- Windows 10 or Windows 11.
- .NET 8 SDK for building from source.
- Git for cloning the repository.
- Optional: WinGet for package update, export, and import workflows.
- Optional: Administrator rights for some Windows temp locations and restore-point requests.

macOS:

- macOS 26.5.1 (25F80) is the primary requested target.
- Swift Package Manager from Xcode or Apple Command Line Tools.
- Git for cloning the repository.
- Optional: full Xcode for `swift test` and future notarized release work.

## Run Windows From Source

Open PowerShell on Windows and run:

```powershell
git clone git@github.com:jaysonguglietta/spacepilot.git
cd spacepilot
dotnet restore .\apps\windows\SpacePilot.sln
dotnet build .\apps\windows\SpacePilot.sln -c Release
dotnet run --project .\apps\windows\src\SpacePilot\SpacePilot.csproj -c Release
```

This starts SpacePilot for Windows from the source tree.

## Create A Windows App Folder

To create a local runnable build folder:

```powershell
dotnet publish .\apps\windows\src\SpacePilot\SpacePilot.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\publish\SpacePilot
```

Then launch:

```powershell
.\artifacts\publish\SpacePilot\SpacePilot.exe
```

If the target computer does not have the matching .NET desktop runtime, create a self-contained build:

```powershell
dotnet publish .\apps\windows\src\SpacePilot\SpacePilot.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish\SpacePilot-self-contained
```

## Future Release Flow

The repository now includes Windows MSI scaffolding and macOS app-bundle packaging, but signed public installers have not been published yet. After release signing is configured, the intended end-user flow is:

1. Open the GitHub Releases page.
2. Download the latest signed Windows installer or notarized macOS artifact.
3. Run the Windows installer or drag the macOS app to Applications.
4. Launch SpacePilot from the Start menu or Applications.
5. Review the first-run safety note before scanning.

Until signed release artifacts exist, prefer the source build paths above.

## Install From A Release Zip

When CI or a release produces `SpacePilot-<version>-win-x64.zip`:

1. Download the zip and matching `.sha256` file.
2. Verify the checksum.
3. Extract the zip to a trusted folder.
4. Run `SpacePilot.exe`.

Unsigned artifacts may trigger Windows SmartScreen or unknown-publisher warnings.

## Run macOS From Source

From Terminal on macOS:

```bash
git clone git@github.com:jaysonguglietta/spacepilot.git
cd spacepilot
swift run --package-path apps/macos/SpacePilotMac -c release SpacePilotMac
```

This launches the native SwiftUI macOS app.

## Validate macOS Core Logic

The repository includes a local validation runner that does not require XCTest:

```bash
bash scripts/macos/validate-core.sh
```

It validates path safety, cleanup-rule boundaries, quarantine/restore behavior, receipt ordering, and preference persistence.

## Build A macOS App Bundle

```bash
bash scripts/macos/build-app.sh
```

Outputs:

```text
artifacts/macos/SpacePilot.app
artifacts/macos/SpacePilot-macOS.zip
artifacts/macos/SpacePilot-macOS.zip.sha256
```

The local app bundle is ad-hoc signed when `codesign` is available. Public distribution still requires Developer ID signing and notarization.

## Update

For a source install:

```powershell
git pull
dotnet build .\apps\windows\SpacePilot.sln -c Release
dotnet run --project .\apps\windows\src\SpacePilot\SpacePilot.csproj -c Release
```

For a published local folder, rerun the `dotnet publish` command and replace the previous publish output.

For a macOS source build:

```bash
git pull
bash scripts/macos/build-app.sh
```

## Uninstall

If you ran SpacePilot for Windows from source or a local publish folder:

1. Close SpacePilot.
2. Delete the cloned repository or publish folder.
3. Optional: delete local app data at `%LOCALAPPDATA%\SpacePilot\`.

Do not delete `%LOCALAPPDATA%\SpacePilot\Quarantine\` if you may need to restore files from quarantine.

If you ran SpacePilot for macOS:

1. Close SpacePilot.
2. Delete the local `SpacePilot.app`, release zip, or cloned repository.
3. Optional: delete local app data at `~/Library/Application Support/SpacePilot/`.

Do not delete `~/Library/Application Support/SpacePilot/Quarantine/` if you may need to restore files from quarantine.

## Data Location

Windows stores preferences, quarantine metadata, receipts, and exports under:

```text
%LOCALAPPDATA%\SpacePilot\
```

macOS stores preferences, quarantine metadata, and receipts under:

```text
~/Library/Application Support/SpacePilot/
```

See [Privacy](PRIVACY.md) for more detail.
