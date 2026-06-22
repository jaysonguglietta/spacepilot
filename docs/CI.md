# Continuous Integration

SpacePilot uses GitHub Actions to validate Windows and macOS builds.

Workflows:

```text
.github/workflows/windows-ci.yml
.github/workflows/macos-ci.yml
```

## Windows CI

On pushes to `main`, pull requests, and manual runs, Windows CI:

1. Checks out the repository.
2. Installs the .NET 8 SDK.
3. Restores `apps/windows/SpacePilot.sln`.
4. Builds the solution in Release configuration.
5. Runs the automated test project.
6. Publishes the WPF app for `win-x64`.
7. Packages the publish output as a zip artifact.
8. Writes a SHA-256 checksum.
9. Uploads test results and the release package as workflow artifacts.

## macOS CI

On pushes to `main`, pull requests, and manual runs, macOS CI:

1. Checks out the repository.
2. Shows the Swift toolchain version.
3. Builds the Swift Package release executable.
4. Runs `scripts/macos/validate-core.sh` for path safety, cleanup-rule, quarantine, receipt, and preference checks.
5. Runs SwiftPM tests when the runner has XCTest available.
6. Builds `SpacePilot.app` with `scripts/macos/build-app.sh`.
7. Ad-hoc signs the local app bundle when `codesign` is available.
8. Packages `SpacePilot-macOS.zip`.
9. Writes a SHA-256 checksum.
10. Uploads the app bundle, zip, and checksum as workflow artifacts.

## Signing In CI

Windows CI supports optional Authenticode signing when these repository secrets are configured:

```text
SPACEPILOT_SIGNING_CERT_BASE64
SPACEPILOT_SIGNING_CERT_PASSWORD
```

`SPACEPILOT_SIGNING_CERT_BASE64` should be a base64-encoded `.pfx` file. When both secrets are present, the workflow imports the certificate into the current-user certificate store and `scripts\windows\package-spacepilot.ps1` signs the published app files.

If secrets are missing, CI still builds, tests, and packages unsigned artifacts.

The macOS workflow currently creates an ad-hoc signed local bundle. Public macOS distribution still needs Developer ID signing, notarization, and release-time verification.

## Local Equivalent

Run the same core checks on Windows:

```powershell
dotnet restore .\apps\windows\SpacePilot.sln
dotnet build .\apps\windows\SpacePilot.sln -c Release
dotnet test .\apps\windows\tests\SpacePilot.Tests\SpacePilot.Tests.csproj -c Release
.\scripts\windows\package-spacepilot.ps1 -Configuration Release -Runtime win-x64 -SkipSigning
```

Run the same core checks on macOS:

```bash
swift build --package-path apps/macos/SpacePilotMac -c release
bash scripts/macos/validate-core.sh
swift test --package-path apps/macos/SpacePilotMac -c release
bash scripts/macos/build-app.sh
```

If the local Apple Command Line Tools installation does not include XCTest, `swift test` may fail with `no such module 'XCTest'`. In that case, use full Xcode for SwiftPM tests and keep `scripts/macos/validate-core.sh` as the local core validation gate.

## Artifacts

Windows CI uploads:

- `test-results`
- `spacepilot-win-x64`

The package artifact contains:

- `SpacePilot-<version>-win-x64.zip`
- `SpacePilot-<version>-win-x64.zip.sha256`

macOS CI uploads:

- `spacepilot-macos`

The package artifact contains:

- `SpacePilot.app`
- `SpacePilot-macOS.zip`
- `SpacePilot-macOS.zip.sha256`
