# Signing And Installer

SpacePilot now has release packaging, optional code signing, and MSI installer scaffolding.

## Release Zip

Create a framework-dependent release package:

```powershell
.\scripts\package-spacepilot.ps1 -Configuration Release -Runtime win-x64 -SkipSigning
```

Create a self-contained release package:

```powershell
.\scripts\package-spacepilot.ps1 -Configuration Release -Runtime win-x64 -SelfContained -SkipSigning
```

Outputs:

```text
artifacts\packages\SpacePilot-<version>-win-x64.zip
artifacts\packages\SpacePilot-<version>-win-x64.zip.sha256
```

## Code Signing

SpacePilot does not commit private certificates. Import a code signing certificate into `Cert:\CurrentUser\My` or `Cert:\LocalMachine\My`, then run:

```powershell
$env:SPACEPILOT_SIGNING_CERT_THUMBPRINT = "<certificate thumbprint>"
.\scripts\package-spacepilot.ps1 -Configuration Release -Runtime win-x64
```

The signing script signs `.exe`, `.dll`, and `.msi` files with Authenticode:

```powershell
.\scripts\sign-spacepilot.ps1 -Path .\artifacts\publish\SpacePilot-win-x64 -CertificateThumbprint "<certificate thumbprint>"
```

## GitHub Actions Signing Secrets

For CI signing, add these repository secrets:

```text
SPACEPILOT_SIGNING_CERT_BASE64
SPACEPILOT_SIGNING_CERT_PASSWORD
```

`SPACEPILOT_SIGNING_CERT_BASE64` must contain the base64 text of the `.pfx` file.

## MSI Installer

The MSI script requires WiX Toolset CLI as `wix`:

```powershell
dotnet tool install --global wix
```

Build an unsigned MSI:

```powershell
.\scripts\build-msi.ps1 -SkipSigning
```

Build and sign an MSI:

```powershell
$env:SPACEPILOT_SIGNING_CERT_THUMBPRINT = "<certificate thumbprint>"
.\scripts\build-msi.ps1
```

Outputs:

```text
artifacts\installers\SpacePilot-<version>-win-x64.msi
artifacts\installers\SpacePilot-<version>-win-x64.msi.sha256
```

## Certificate Handling

- Do not commit certificates, passwords, or private keys.
- Prefer a hardware-backed or managed code-signing certificate for public releases.
- Rotate secrets if a certificate is exposed.
- Verify signatures on a clean Windows machine before release.
