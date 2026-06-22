# Signing And Installer

SpacePilot now has Windows portable ZIP packaging, Windows MSI installer ZIP packaging, optional Authenticode signing, macOS app-bundle packaging, and macOS DMG packaging.

## Release Packages

GitHub Releases can publish compiled Windows and macOS packages with:

```bash
git tag v0.1.0
git push origin v0.1.0
```

The `Release Packages` workflow attaches the compiled ZIP, installer ZIP, and DMG files plus checksums to the release.

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

Create an installer ZIP containing the MSI, MSI checksum, and install note:

```powershell
.\scripts\windows\package-installer.ps1 -Configuration Release -Runtime win-x64 -SkipSigning
```

Outputs:

```text
artifacts\packages\SpacePilot-<version>-win-x64-installer.zip
artifacts\packages\SpacePilot-<version>-win-x64-installer.zip.sha256
```

## Code Signing

SpacePilot does not commit private certificates. Import a code signing certificate into `Cert:\CurrentUser\My` or `Cert:\LocalMachine\My`, then run:

```powershell
$env:SPACEPILOT_SIGNING_CERT_THUMBPRINT = "<certificate thumbprint>"
.\scripts\windows\package-spacepilot.ps1 -Configuration Release -Runtime win-x64
.\scripts\windows\package-installer.ps1 -Configuration Release -Runtime win-x64 -SkipBuild
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

The release installer ZIP wraps those MSI outputs into:

```text
artifacts\packages\SpacePilot-<version>-win-x64-installer.zip
artifacts\packages\SpacePilot-<version>-win-x64-installer.zip.sha256
```

## macOS App Bundle And DMG

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

Create a local macOS DMG:

```bash
bash scripts/macos/build-dmg.sh
```

Outputs:

```text
artifacts/macos/SpacePilot-macOS.dmg
artifacts/macos/SpacePilot-macOS.dmg.sha256
artifacts/macos/SpacePilot-0.1.0-macOS.dmg
artifacts/macos/SpacePilot-0.1.0-macOS.dmg.sha256
```

Use `VERSION="0.2.0" bash scripts/macos/build-dmg.sh` to produce a versioned DMG for another release number.

The app bundle is ad-hoc signed when `codesign` is available. Ad-hoc signing is for local testing only.

## macOS Developer ID Signing And Notarization

Public macOS distribution still needs:

- Apple Developer Program membership.
- Developer ID Application certificate.
- Hardened runtime signing configuration.
- Notarization through Apple notary service.
- Stapling the notarization ticket to the distributed app or installer.
- Verification on a clean Mac with Gatekeeper enabled.

## Certificate Handling

- Do not commit certificates, passwords, or private keys.
- Prefer a hardware-backed or managed code-signing certificate for public releases.
- Rotate secrets if a certificate is exposed.
- Verify signatures on a clean Windows machine before release.
- Verify Developer ID signature and notarization on a clean Mac before release.
