[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = $(if ($env:GITHUB_REF_TYPE -eq "tag") { $env:GITHUB_REF_NAME.TrimStart("v") } else { "0.1.0" }),
    [switch]$SelfContained,
    [switch]$SkipBuild,
    [switch]$SkipSigning,
    [string]$CertificateThumbprint = $env:SPACEPILOT_SIGNING_CERT_THUMBPRINT
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishDir = Join-Path $repoRoot "artifacts\publish\SpacePilot-$Runtime"
$packageDir = Join-Path $repoRoot "artifacts\packages"
$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

New-Item -ItemType Directory -Force -Path $publishDir, $packageDir | Out-Null

if (-not $SkipBuild) {
    dotnet publish (Join-Path $repoRoot "src\SpacePilot\SpacePilot.csproj") `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained $selfContainedValue `
        -p:Version=$Version `
        -p:PublishSingleFile=false `
        -o $publishDir
}

if (-not (Test-Path (Join-Path $publishDir "SpacePilot.exe"))) {
    throw "Publish output is missing SpacePilot.exe at $publishDir."
}

if (-not $SkipSigning) {
    & (Join-Path $PSScriptRoot "sign-spacepilot.ps1") -Path $publishDir -CertificateThumbprint $CertificateThumbprint
} else {
    Write-Host "Skipping code signing."
}

$packageName = "SpacePilot-$Version-$Runtime"
if ($SelfContained) {
    $packageName += "-self-contained"
}

$zipPath = Join-Path $packageDir "$packageName.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
$hash = Get-FileHash -Algorithm SHA256 -Path $zipPath
$hashLine = "$($hash.Hash)  $(Split-Path $zipPath -Leaf)"
$hashPath = "$zipPath.sha256"
Set-Content -Path $hashPath -Value $hashLine -Encoding ASCII

Write-Host "Package: $zipPath"
Write-Host "SHA256: $hashLine"
