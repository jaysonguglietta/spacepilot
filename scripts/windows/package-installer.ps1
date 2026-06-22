[CmdletBinding()]
param(
    [string]$Configuration = $(if ($env:SPACEPILOT_CONFIGURATION) { $env:SPACEPILOT_CONFIGURATION } else { "Release" }),
    [string]$Runtime = $(if ($env:SPACEPILOT_RUNTIME) { $env:SPACEPILOT_RUNTIME } else { "win-x64" }),
    [string]$Version = $(if ($env:SPACEPILOT_VERSION) { $env:SPACEPILOT_VERSION } elseif ($env:GITHUB_REF_TYPE -eq "tag") { $env:GITHUB_REF_NAME.TrimStart("v") } else { "0.1.0" }),
    [switch]$SkipSigning,
    [string]$CertificateThumbprint = $env:SPACEPILOT_SIGNING_CERT_THUMBPRINT
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$installerDir = Join-Path $repoRoot "artifacts\installers"
$packageDir = Join-Path $repoRoot "artifacts\packages"
$workDir = Join-Path $repoRoot "artifacts\installer-package-work"
$msiName = "SpacePilot-$Version-$Runtime.msi"
$msiPath = Join-Path $installerDir $msiName
$zipName = "SpacePilot-$Version-$Runtime-installer.zip"
$zipPath = Join-Path $packageDir $zipName
$shouldSkipSigning = $SkipSigning -or $env:SPACEPILOT_SKIP_SIGNING -eq "true" -or [string]::IsNullOrWhiteSpace($CertificateThumbprint)

New-Item -ItemType Directory -Force -Path $installerDir, $packageDir | Out-Null

$buildArgs = @(
    "-Configuration", $Configuration,
    "-Runtime", $Runtime,
    "-Version", $Version
)

if ($shouldSkipSigning) {
    $buildArgs += "-SkipSigning"
} elseif (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $buildArgs += @("-CertificateThumbprint", $CertificateThumbprint)
}

& (Join-Path $PSScriptRoot "build-msi.ps1") @buildArgs

if (-not (Test-Path $msiPath)) {
    throw "MSI output was not found at $msiPath."
}

Remove-Item -Path $workDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$zipPath.sha256" -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $workDir | Out-Null

Copy-Item -Path $msiPath -Destination (Join-Path $workDir $msiName)
Copy-Item -Path "$msiPath.sha256" -Destination (Join-Path $workDir "$msiName.sha256")

@"
SpacePilot Windows Installer

1. Double-click $msiName.
2. Follow the installer prompts.
3. Launch SpacePilot from the Start menu.

Notes:
- Windows may show SmartScreen or unknown-publisher warnings until production signing is configured.
- The installer keeps user data under %LOCALAPPDATA%\SpacePilot when the app is uninstalled.
- Verify $msiName with $msiName.sha256 before installing when possible.
"@ | Set-Content -Path (Join-Path $workDir "INSTALL.txt") -Encoding ASCII

Compress-Archive -Path (Join-Path $workDir "*") -DestinationPath $zipPath -Force

$hash = Get-FileHash -Algorithm SHA256 -Path $zipPath
$hashLine = "$($hash.Hash)  $(Split-Path $zipPath -Leaf)"
Set-Content -Path "$zipPath.sha256" -Value $hashLine -Encoding ASCII

Remove-Item -Path $workDir -Recurse -Force

Write-Host "Installer package: $zipPath"
Write-Host "SHA256: $hashLine"
