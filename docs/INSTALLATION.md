# Installation

SpacePilot currently supports local source builds for Windows and macOS. A signed Windows installer and a notarized macOS release artifact have not been published yet, so the supported install path today is to clone the repository and build locally.

Use the section for your platform. Each command block is intended to be copied and pasted.

## Windows: Run From Source

### 1. Open PowerShell

Open **PowerShell** from the Start menu.

### 2. Install prerequisites

If Git or the .NET 8 SDK are missing, install them with WinGet:

```powershell
winget install --id Git.Git -e
winget install --id Microsoft.DotNet.SDK.8 -e
```

Close and reopen PowerShell, then verify:

```powershell
git --version
dotnet --version
```

### 3. Clone SpacePilot

SSH clone:

```powershell
mkdir "$env:USERPROFILE\Source"
cd "$env:USERPROFILE\Source"
git clone git@github.com:jaysonguglietta/spacepilot.git
cd spacepilot
```

If SSH is not configured, use HTTPS instead:

```powershell
mkdir "$env:USERPROFILE\Source"
cd "$env:USERPROFILE\Source"
git clone https://github.com/jaysonguglietta/spacepilot.git
cd spacepilot
```

### 4. Restore, build, and run

```powershell
dotnet restore .\apps\windows\SpacePilot.sln
dotnet build .\apps\windows\SpacePilot.sln -c Release
dotnet run --project .\apps\windows\src\SpacePilot\SpacePilot.csproj -c Release
```

SpacePilot should open after the final command.

## Windows: Create A Local App Folder

Use this when you want a runnable folder instead of launching from source every time.

### 1. Publish the app

From the repository root:

```powershell
dotnet publish .\apps\windows\src\SpacePilot\SpacePilot.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\publish\SpacePilot
```

### 2. Launch the published app

```powershell
.\artifacts\publish\SpacePilot\SpacePilot.exe
```

### 3. Optional: create a self-contained build

Use this if the target PC does not have the .NET Desktop Runtime installed:

```powershell
dotnet publish .\apps\windows\src\SpacePilot\SpacePilot.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish\SpacePilot-self-contained
.\artifacts\publish\SpacePilot-self-contained\SpacePilot.exe
```

## Windows: Create A Release Zip

From the repository root:

```powershell
.\scripts\windows\package-spacepilot.ps1 -Configuration Release -Runtime win-x64 -SkipSigning
```

Outputs:

```text
artifacts\packages\SpacePilot-0.1.0-win-x64.zip
artifacts\packages\SpacePilot-0.1.0-win-x64.zip.sha256
```

Unsigned artifacts may trigger Windows SmartScreen or unknown-publisher warnings.

## Windows: Update

From the repository root:

```powershell
git pull
dotnet restore .\apps\windows\SpacePilot.sln
dotnet build .\apps\windows\SpacePilot.sln -c Release
dotnet run --project .\apps\windows\src\SpacePilot\SpacePilot.csproj -c Release
```

If you use a published app folder, rerun the `dotnet publish` command from the Windows app-folder section.

## Windows: Uninstall Or Reset

### Remove the app files

Close SpacePilot, then delete the cloned repository or publish folder.

Example:

```powershell
Remove-Item "$env:USERPROFILE\Source\spacepilot" -Recurse -Force
```

### Optional: remove SpacePilot local data

Only do this if you do not need to restore anything from quarantine:

```powershell
Remove-Item "$env:LOCALAPPDATA\SpacePilot" -Recurse -Force
```

Do not delete `%LOCALAPPDATA%\SpacePilot\Quarantine\` if you may need to restore files.

## macOS: Run From Source

### 1. Open Terminal

Open **Terminal** from Applications or Spotlight.

### 2. Install Apple Command Line Tools

```bash
xcode-select --install
```

If Command Line Tools are already installed, macOS will say so.

### 3. Verify tools

```bash
git --version
swift --version
```

### 4. Clone SpacePilot

SSH clone:

```bash
mkdir -p "$HOME/Source"
cd "$HOME/Source"
git clone git@github.com:jaysonguglietta/spacepilot.git
cd spacepilot
```

If SSH is not configured, use HTTPS instead:

```bash
mkdir -p "$HOME/Source"
cd "$HOME/Source"
git clone https://github.com/jaysonguglietta/spacepilot.git
cd spacepilot
```

### 5. Build and run

```bash
swift build --package-path apps/macos/SpacePilotMac -c release
swift run --package-path apps/macos/SpacePilotMac -c release SpacePilotMac
```

SpacePilot should open after the final command.

## macOS: Validate And Package

### 1. Run core validation

This validates path safety, cleanup-rule boundaries, quarantine/restore behavior, receipt ordering, and preference persistence:

```bash
bash scripts/macos/validate-core.sh
```

### 2. Build the local app bundle

```bash
bash scripts/macos/build-app.sh
```

Outputs:

```text
artifacts/macos/SpacePilot.app
artifacts/macos/SpacePilot-macOS.zip
artifacts/macos/SpacePilot-macOS.zip.sha256
```

### 3. Launch the app bundle

```bash
open artifacts/macos/SpacePilot.app
```

### 4. Optional: install to your user Applications folder

This avoids needing administrator permissions:

```bash
mkdir -p "$HOME/Applications"
rm -rf "$HOME/Applications/SpacePilot.app"
cp -R artifacts/macos/SpacePilot.app "$HOME/Applications/SpacePilot.app"
open "$HOME/Applications/SpacePilot.app"
```

The local bundle is ad-hoc signed for testing. Public distribution still requires Developer ID signing and notarization.

## macOS: Update

From the repository root:

```bash
git pull
swift build --package-path apps/macos/SpacePilotMac -c release
bash scripts/macos/validate-core.sh
bash scripts/macos/build-app.sh
open artifacts/macos/SpacePilot.app
```

If you copied SpacePilot into `~/Applications`, copy the rebuilt app again:

```bash
rm -rf "$HOME/Applications/SpacePilot.app"
cp -R artifacts/macos/SpacePilot.app "$HOME/Applications/SpacePilot.app"
open "$HOME/Applications/SpacePilot.app"
```

## macOS: Uninstall Or Reset

### Remove the app files

Close SpacePilot, then delete the local app bundle and repository if desired:

```bash
rm -rf "$HOME/Applications/SpacePilot.app"
rm -rf "$HOME/Source/spacepilot"
```

### Optional: remove SpacePilot local data

Only do this if you do not need to restore anything from quarantine:

```bash
rm -rf "$HOME/Library/Application Support/SpacePilot"
```

Do not delete `~/Library/Application Support/SpacePilot/Quarantine/` if you may need to restore files.

## Data Locations

Windows:

```text
%LOCALAPPDATA%\SpacePilot\
```

macOS:

```text
~/Library/Application Support/SpacePilot/
```

See [Privacy](PRIVACY.md) for more detail.
