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

## Step-By-Step: Install From A Compiled Release

When a release is published, download the Windows zip from:

```text
https://github.com/jaysonguglietta/spacepilot/releases
```

Copy and paste this in PowerShell after replacing `0.1.0` with the version you want:

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

Compare the hash printed by `Get-FileHash` with the hash in the `.sha256` file before launching. Unsigned builds may show Windows SmartScreen warnings.

## Step-By-Step: Run From Source

### 1. Open PowerShell

Open **PowerShell** from the Start menu.

### 2. Install prerequisites

```powershell
winget install --id Git.Git -e
winget install --id Microsoft.DotNet.SDK.8 -e
```

Close and reopen PowerShell, then verify:

```powershell
git --version
dotnet --version
```

### 3. Clone the repo

Use SSH:

```powershell
mkdir "$env:USERPROFILE\Source"
cd "$env:USERPROFILE\Source"
git clone git@github.com:jaysonguglietta/spacepilot.git
cd spacepilot
```

Or use HTTPS:

```powershell
mkdir "$env:USERPROFILE\Source"
cd "$env:USERPROFILE\Source"
git clone https://github.com/jaysonguglietta/spacepilot.git
cd spacepilot
```

### 4. Build and run

```powershell
dotnet restore .\apps\windows\SpacePilot.sln
dotnet build .\apps\windows\SpacePilot.sln -c Release
dotnet run --project .\apps\windows\src\SpacePilot\SpacePilot.csproj -c Release
```

## Step-By-Step: Test

```powershell
dotnet test .\apps\windows\tests\SpacePilot.Tests\SpacePilot.Tests.csproj -c Release
```

## Step-By-Step: Create A Runnable Folder

```powershell
dotnet publish .\apps\windows\src\SpacePilot\SpacePilot.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\publish\SpacePilot
.\artifacts\publish\SpacePilot\SpacePilot.exe
```

Self-contained build:

```powershell
dotnet publish .\apps\windows\src\SpacePilot\SpacePilot.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish\SpacePilot-self-contained
.\artifacts\publish\SpacePilot-self-contained\SpacePilot.exe
```

## Step-By-Step: Package

Create a release zip:

```powershell
.\scripts\windows\package-spacepilot.ps1 -Configuration Release -Runtime win-x64 -SkipSigning
```

Create an MSI when WiX is installed:

```powershell
.\scripts\windows\build-msi.ps1 -SkipSigning
```

## Step-By-Step: Update

```powershell
git pull
dotnet restore .\apps\windows\SpacePilot.sln
dotnet build .\apps\windows\SpacePilot.sln -c Release
dotnet run --project .\apps\windows\src\SpacePilot\SpacePilot.csproj -c Release
```

## Step-By-Step: Uninstall Or Reset

Remove cloned source:

```powershell
Remove-Item "$env:USERPROFILE\Source\spacepilot" -Recurse -Force
```

Optional local-data reset:

```powershell
Remove-Item "$env:LOCALAPPDATA\SpacePilot" -Recurse -Force
```

Do not remove local data if you may need to restore quarantined files.

## Local Data

Windows stores preferences, quarantine metadata, receipts, and WinGet exports under:

```text
%LOCALAPPDATA%\SpacePilot\
```

## Platform Notes

- The app targets WPF and `net8.0-windows10.0.19041.0`.
- Build and runtime validation should happen on Windows.
- Public release still needs signed installer artifacts and completed Windows QA evidence.
