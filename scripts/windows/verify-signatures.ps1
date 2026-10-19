[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [Parameter(Mandatory = $true)]
    [string]$CertificateThumbprint
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    throw "An expected signing certificate thumbprint is required."
}

$resolvedPath = (Resolve-Path -LiteralPath $Path).Path
$targets = @(Get-ChildItem -LiteralPath $resolvedPath -Recurse -File |
    Where-Object { $_.Extension -in @(".exe", ".dll", ".msi") })

if ($targets.Count -eq 0) {
    throw "No signed executables, libraries, or installers were found at $resolvedPath."
}

$expectedThumbprint = $CertificateThumbprint -replace '\s', ''
foreach ($target in $targets) {
    $signature = Get-AuthenticodeSignature -LiteralPath $target.FullName
    if ($signature.Status -ne "Valid") {
        throw "Invalid Authenticode signature on $($target.FullName): $($signature.Status)"
    }
    if (-not $signature.SignerCertificate -or
        ($signature.SignerCertificate.Thumbprint -replace '\s', '') -ine $expectedThumbprint) {
        throw "Unexpected signing certificate on $($target.FullName)."
    }
    Write-Host "Verified $($target.FullName)"
}
