# Signing And Installer

SpacePilot has Windows portable ZIP and MSI packaging, plus macOS app-bundle and DMG packaging. Local CI artifacts may be unsigned or ad-hoc signed; the GitHub release workflow requires Authenticode, Developer ID, and notarization.

## Release Packages

GitHub Releases can publish compiled Windows and macOS packages with:

```bash
git tag v0.1.0
git push origin v0.1.0
```

The `Release Packages` workflow accepts a numeric `major.minor.patch` version with an optional prerelease suffix. It runs only for commits reachable from `main`; manual dispatch must use `main`. It attaches signed and notarized packages plus checksums, and refuses to overwrite an existing release. Without the release environment and credentials below, it fails before publishing.

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
$env:SPACEPILOT_SKIP_SIGNING = "true"
.\scripts\windows\package-installer.ps1
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
$env:SPACEPILOT_SKIP_SIGNING = "false"
.\scripts\windows\package-spacepilot.ps1 -Configuration Release -Runtime win-x64
.\scripts\windows\package-installer.ps1
```

The signing script signs `.exe`, `.dll`, and `.msi` files with Authenticode:

```powershell
.\scripts\windows\sign-spacepilot.ps1 -Path .\artifacts\publish\SpacePilot-win-x64 -CertificateThumbprint "<certificate thumbprint>"
```

## GitHub Release Environment

Create a GitHub Actions environment named `release` with required reviewers. Add these **environment secrets**, not general CI secrets:

```text
SPACEPILOT_SIGNING_CERT_BASE64
SPACEPILOT_SIGNING_CERT_PASSWORD
SPACEPILOT_MACOS_SIGNING_CERT_BASE64
SPACEPILOT_MACOS_SIGNING_CERT_PASSWORD
SPACEPILOT_MACOS_SIGNING_IDENTITY
SPACEPILOT_MACOS_TEAM_ID
SPACEPILOT_NOTARY_APPLE_ID
SPACEPILOT_NOTARY_PASSWORD
```

The Windows base64 value is the `.pfx` file; the macOS base64 value is the Developer ID Application `.p12` file. The notary password is an Apple app-specific password. The signing identity must match the imported certificate and the team ID must match its `TeamIdentifier`. The release workflow imports credentials only after build/test and validates signed files inside both Windows download archives. It notarizes the macOS app ZIP, staples the app, rebuilds the ZIP, notarizes/staples the DMG, and verifies both tickets. Pull-request CI never receives signing secrets.

GitHub repository settings must separately protect `main` and version tags, require passing Windows/macOS checks and review, restrict who can create release tags or dispatch the release workflow, and keep the `release` environment restricted. Workflow YAML alone cannot install these repository rules. Test one draft release on a clean Windows and Mac machine before public distribution.

## MSI Installer

The MSI script requires WiX Toolset CLI as `wix`. SpacePilot pins CI examples to WiX `6.0.2`; WiX 7 adds an EULA acceptance gate that should be handled intentionally by the project owner before upgrading.

```powershell
dotnet tool install --global wix --version 6.0.2
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

The release workflow uses `scripts/macos/build-app.sh` with hardened-runtime Developer ID signing and `scripts/macos/notarize-release.sh` for Apple notarization and stapling. The ZIP contains the stapled app; the DMG itself is stapled. Local `build-app.sh` still uses ad-hoc signing for development unless release signing is explicitly required. Verify Gatekeeper behavior on a clean Mac before publishing.

## Certificate Handling

- Do not commit certificates, passwords, or private keys.
- Prefer a hardware-backed or managed code-signing certificate for public releases.
- Rotate secrets if a certificate is exposed.
- Verify signatures on a clean Windows machine before release.
- Verify Developer ID signature and notarization on a clean Mac before release.
