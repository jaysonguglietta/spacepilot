# Installation

SpacePilot is currently a source-available Windows desktop app. A signed public installer has not been published yet, so the supported install path today is to build and run it from source on Windows.

## Requirements

- Windows 10 or Windows 11.
- .NET 8 SDK for building from source.
- Git for cloning the repository.
- Optional: WinGet for package update, export, and import workflows.
- Optional: Administrator rights for some Windows temp locations and restore-point requests.

## Install From Source

Open PowerShell on Windows and run:

```powershell
git clone git@github.com:jaysonguglietta/spacepilot.git
cd spacepilot
dotnet restore .\SpacePilot.sln
dotnet build .\SpacePilot.sln -c Release
dotnet run --project .\src\SpacePilot\SpacePilot.csproj -c Release
```

This starts SpacePilot from the source tree.

## Create A Local App Folder

To create a local runnable build folder:

```powershell
dotnet publish .\src\SpacePilot\SpacePilot.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\publish\SpacePilot
```

Then launch:

```powershell
.\artifacts\publish\SpacePilot\SpacePilot.exe
```

If the target computer does not have the matching .NET desktop runtime, create a self-contained build:

```powershell
dotnet publish .\src\SpacePilot\SpacePilot.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish\SpacePilot-self-contained
```

## Future Installer Flow

After release packaging is added, the intended end-user flow is:

1. Open the GitHub Releases page.
2. Download the latest signed SpacePilot installer.
3. Run the installer.
4. Launch SpacePilot from the Start menu.
5. Review the first-run safety note before scanning.

Until a signed installer exists, prefer the source build path above.

## Update

For a source install:

```powershell
git pull
dotnet build .\SpacePilot.sln -c Release
dotnet run --project .\src\SpacePilot\SpacePilot.csproj -c Release
```

For a published local folder, rerun the `dotnet publish` command and replace the previous publish output.

## Uninstall

If you ran SpacePilot from source or a local publish folder:

1. Close SpacePilot.
2. Delete the cloned repository or publish folder.
3. Optional: delete local app data at `%LOCALAPPDATA%\SpacePilot\`.

Do not delete `%LOCALAPPDATA%\SpacePilot\Quarantine\` if you may need to restore files from quarantine.

## Data Location

SpacePilot stores preferences, quarantine metadata, receipts, and exports under:

```text
%LOCALAPPDATA%\SpacePilot\
```

See [Privacy](PRIVACY.md) for more detail.
