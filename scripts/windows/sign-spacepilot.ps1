[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [string]$CertificateThumbprint = $env:SPACEPILOT_SIGNING_CERT_THUMBPRINT,

    [string]$TimestampUrl = $(if ($env:SPACEPILOT_TIMESTAMP_URL) { $env:SPACEPILOT_TIMESTAMP_URL } else { "http://timestamp.digicert.com" })
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    throw "A signing certificate thumbprint is required. Set SPACEPILOT_SIGNING_CERT_THUMBPRINT or pass -CertificateThumbprint."
}

$resolvedPath = Resolve-Path $Path
$certificate = Get-ChildItem -Path Cert:\CurrentUser\My, Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -replace '\s', '' -ieq ($CertificateThumbprint -replace '\s', '') } |
    Select-Object -First 1

if (-not $certificate) {
    throw "Could not find signing certificate with thumbprint $CertificateThumbprint."
}

$targets = if (Test-Path $resolvedPath -PathType Container) {
    Get-ChildItem -Path $resolvedPath -Recurse -File -Include *.exe, *.dll, *.msi
} else {
    Get-Item $resolvedPath
}

if ($targets.Count -eq 0) {
    throw "No signable files were found at $resolvedPath."
}

foreach ($target in $targets) {
    Write-Host "Signing $($target.FullName)"
    $signature = Set-AuthenticodeSignature -FilePath $target.FullName -Certificate $certificate -TimestampServer $TimestampUrl
    if ($signature.Status -ne "Valid") {
        throw "Signing failed for $($target.FullName): $($signature.StatusMessage)"
    }
}
