[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = $(if ($env:GITHUB_REF_TYPE -eq "tag") { $env:GITHUB_REF_NAME.TrimStart("v") } else { "0.1.0" }),
    [string]$Manufacturer = "SpacePilot",
    [string]$UpgradeCode = "7D461C33-9C4A-4F39-A78B-B0DC573C0E69",
    [switch]$SkipBuild,
    [switch]$SkipSigning,
    [string]$CertificateThumbprint = $env:SPACEPILOT_SIGNING_CERT_THUMBPRINT
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    throw "WiX Toolset CLI was not found. Install it with 'dotnet tool install --global wix' or restore a repo-local tool before running this script."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$publishDir = Join-Path $repoRoot "artifacts\publish\SpacePilot-$Runtime"
$msiDir = Join-Path $repoRoot "artifacts\installers"
$workDir = Join-Path $repoRoot "artifacts\wix"
New-Item -ItemType Directory -Force -Path $publishDir, $msiDir, $workDir | Out-Null

if (-not $SkipBuild) {
    dotnet publish (Join-Path $repoRoot "apps\windows\src\SpacePilot\SpacePilot.csproj") `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained false `
        -p:Version=$Version `
        -p:PublishSingleFile=false `
        -o $publishDir
}

if (-not (Test-Path (Join-Path $publishDir "SpacePilot.exe"))) {
    throw "Publish output is missing SpacePilot.exe at $publishDir."
}

if (-not $SkipSigning) {
    & (Join-Path $PSScriptRoot "sign-spacepilot.ps1") -Path $publishDir -CertificateThumbprint $CertificateThumbprint
}

function ConvertTo-WixId([string]$value) {
    $clean = ($value -replace '[^A-Za-z0-9_]', '_')
    if ($clean -match '^[0-9]') {
        return "id_$clean"
    }
    return $clean
}

$files = Get-ChildItem -Path $publishDir -Recurse -File | Sort-Object FullName
$componentRows = New-Object System.Collections.Generic.List[string]
$componentRefs = New-Object System.Collections.Generic.List[string]
$index = 0
foreach ($file in $files) {
    $index++
    $relative = [IO.Path]::GetRelativePath($publishDir, $file.FullName)
    $componentId = ConvertTo-WixId "cmp_${index}_$relative"
    $fileId = ConvertTo-WixId "fil_${index}_$relative"
    $source = [System.Security.SecurityElement]::Escape($file.FullName)
    $componentRows.Add("        <Component Id=`"$componentId`" Guid=`"*`"><File Id=`"$fileId`" Source=`"$source`" KeyPath=`"yes`" /></Component>")
    $componentRefs.Add("      <ComponentRef Id=`"$componentId`" />")
}

$components = $componentRows -join [Environment]::NewLine
$refs = $componentRefs -join [Environment]::NewLine
$wxsPath = Join-Path $workDir "SpacePilot.Generated.wxs"
$msiPath = Join-Path $msiDir "SpacePilot-$Version-$Runtime.msi"

@"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="SpacePilot" Manufacturer="$Manufacturer" Version="$Version" UpgradeCode="$UpgradeCode" Scope="perMachine">
    <MajorUpgrade DowngradeErrorMessage="A newer version of SpacePilot is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="SpacePilot" />
    </StandardDirectory>

    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="SpacePilot" />
    </StandardDirectory>

    <DirectoryRef Id="INSTALLFOLDER">
$components
    </DirectoryRef>

    <DirectoryRef Id="ApplicationProgramsFolder">
      <Component Id="ApplicationShortcut" Guid="*">
        <Shortcut Id="StartMenuShortcut" Name="SpacePilot" Target="[INSTALLFOLDER]SpacePilot.exe" WorkingDirectory="INSTALLFOLDER" />
        <RemoveFolder Id="ApplicationProgramsFolder" On="uninstall" />
        <RegistryValue Root="HKCU" Key="Software\SpacePilot" Name="installed" Type="integer" Value="1" KeyPath="yes" />
      </Component>
    </DirectoryRef>

    <Feature Id="MainFeature" Title="SpacePilot" Level="1">
$refs
      <ComponentRef Id="ApplicationShortcut" />
    </Feature>
  </Package>
</Wix>
"@ | Set-Content -Path $wxsPath -Encoding UTF8

wix build $wxsPath -arch x64 -out $msiPath

if (-not $SkipSigning) {
    & (Join-Path $PSScriptRoot "sign-spacepilot.ps1") -Path $msiPath -CertificateThumbprint $CertificateThumbprint
}

$hash = Get-FileHash -Algorithm SHA256 -Path $msiPath
Set-Content -Path "$msiPath.sha256" -Value "$($hash.Hash)  $(Split-Path $msiPath -Leaf)" -Encoding ASCII
Write-Host "MSI: $msiPath"
