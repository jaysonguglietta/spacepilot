# Installation

SpacePilot can be distributed as compiled Windows and macOS release packages from GitHub Releases. Source builds are still documented for developers and testers.

Use the section for your platform. Each command block is intended to be copied and pasted.

## Download A Compiled Package

When a release is published, users can download packages from:

```text
https://github.com/jaysonguglietta/spacepilot/releases
```

Release assets use this naming pattern:

```text
SpacePilot-<version>-win-x64.zip
SpacePilot-<version>-win-x64.zip.sha256
SpacePilot-<version>-macOS.zip
SpacePilot-<version>-macOS.zip.sha256
```

Until production signing is configured, Windows packages may show unknown-publisher warnings and macOS packages may show Gatekeeper warnings.

## Windows: Install From A Compiled Release

### Option A: Download in your browser

1. Open `https://github.com/jaysonguglietta/spacepilot/releases`.
2. Open the latest release.
3. Download `SpacePilot-<version>-win-x64.zip`.
4. Download `SpacePilot-<version>-win-x64.zip.sha256`.
5. Extract the zip.
6. Run `SpacePilot.exe`.

### Option B: Copy and paste in PowerShell

Replace `0.1.0` with the release version you want:

```powershell
$Version = "0.1.0"
$DownloadFolder = "$env:USERPROFILE\Downloads"
$ZipName = "SpacePilot-$Version-win-x64.zip"
$ChecksumName = "$ZipName.sha256"
$ReleaseBase = "https://github.com/jaysonguglietta/spacepilot/releases/download/v$Version"

Invoke-WebRequest -Uri "$ReleaseBase/$ZipName" -OutFile "$DownloadFolder\$ZipName"
Invoke-WebRequest -Uri "$ReleaseBase/$ChecksumName" -OutFile "$DownloadFolder\$ChecksumName"

cd $DownloadFolder
Get-FileHash -Algorithm SHA256 ".\$ZipName"
Get-Content ".\$ChecksumName"

$InstallFolder = "$env:LOCALAPPDATA\Programs\SpacePilot"
Remove-Item $InstallFolder -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $InstallFolder | Out-Null
Expand-Archive ".\$ZipName" -DestinationPath $InstallFolder -Force
& "$InstallFolder\SpacePilot.exe"
```

Compare the hash printed by `Get-FileHash` with the hash in the `.sha256` file before launching.

## macOS: Install From A Compiled Release

### Option A: Download in your browser

1. Open `https://github.com/jaysonguglietta/spacepilot/releases`.
2. Open the latest release.
3. Download `SpacePilot-<version>-macOS.zip`.
4. Download `SpacePilot-<version>-macOS.zip.sha256`.
5. Double-click the zip to extract `SpacePilot.app`.
6. Move `SpacePilot.app` to Applications or `~/Applications`.
7. Open SpacePilot.

### Option B: Copy and paste in Terminal

Replace `0.1.0` with the release version you want:

```bash
VERSION="0.1.0"
DOWNLOAD_DIR="$HOME/Downloads"
ZIP_NAME="SpacePilot-$VERSION-macOS.zip"
CHECKSUM_NAME="$ZIP_NAME.sha256"
RELEASE_BASE="https://github.com/jaysonguglietta/spacepilot/releases/download/v$VERSION"

cd "$DOWNLOAD_DIR"
curl -L -o "$ZIP_NAME" "$RELEASE_BASE/$ZIP_NAME"
curl -L -o "$CHECKSUM_NAME" "$RELEASE_BASE/$CHECKSUM_NAME"
shasum -a 256 -c "$CHECKSUM_NAME"

unzip -o "$ZIP_NAME"
mkdir -p "$HOME/Applications"
rm -rf "$HOME/Applications/SpacePilot.app"
cp -R SpacePilot.app "$HOME/Applications/SpacePilot.app"
open "$HOME/Applications/SpacePilot.app"
```

If macOS blocks the app because the package is not notarized yet, open **System Settings > Privacy & Security** and review the Gatekeeper prompt.

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
