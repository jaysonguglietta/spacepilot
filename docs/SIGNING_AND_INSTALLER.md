# Signing And Installer

SpacePilot now has Windows release packaging, optional Authenticode signing, MSI installer scaffolding, and macOS app-bundle packaging.

## Release Zip

Create a framework-dependent release package:

```powershell
.\scripts\windows\package-spacepilot.ps1 -Configuration Release -Runtime win-x64 -SkipSigning
```

Create a self-contained release package:

```powershell
.\scripts\windows\package-spacepilot.ps1 -Configuration Release -Runtime win-x64 -SelfContained -SkipSigning
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
.\scripts\windows\package-spacepilot.ps1 -Configuration Release -Runtime win-x64
```

The signing script signs `.exe`, `.dll`, and `.msi` files with Authenticode:

```powershell
.\scripts\windows\sign-spacepilot.ps1 -Path .\artifacts\publish\SpacePilot-win-x64 -CertificateThumbprint "<certificate thumbprint>"
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
.\scripts\windows\build-msi.ps1 -SkipSigning
```

Build and sign an MSI:

```powershell
$env:SPACEPILOT_SIGNING_CERT_THUMBPRINT = "<certificate thumbprint>"
.\scripts\windows\build-msi.ps1
```

Outputs:

```text
artifacts\installers\SpacePilot-<version>-win-x64.msi
artifacts\installers\SpacePilot-<version>-win-x64.msi.sha256
```

## macOS App Bundle

Create a local macOS bundle and zip:

```bash
bash scripts/macos/build-app.sh
```

Outputs:

```text
artifacts/macos/SpacePilot.app
artifacts/macos/SpacePilot-macOS.zip
artifacts/macos/SpacePilot-macOS.zip.sha256
```

The script ad-hoc signs the app when `codesign` is available. Ad-hoc signing is for local testing only.

## macOS Developer ID Signing And Notarization

Public macOS distribution still needs:

- Apple Developer Program membership.
- Developer ID Application certificate.
- Hardened runtime signing configuration.
- Notarization through Apple notary service.
- Stapling the notarization ticket to the distributed app or installer.
- Verification on a clean Mac with Gatekeeper enabled.

Future release automation should add signed `.dmg` or `.pkg` output once credentials and distribution format are chosen.

## Certificate Handling

- Do not commit certificates, passwords, or private keys.
- Prefer a hardware-backed or managed code-signing certificate for public releases.
- Rotate secrets if a certificate is exposed.
- Verify signatures on a clean Windows machine before release.
- Verify Developer ID signature and notarization on a clean Mac before release.
