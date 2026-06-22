# Continuous Integration

SpacePilot uses GitHub Actions to validate Windows builds.

Workflow:

```text
.github/workflows/windows-ci.yml
```

## What CI Runs

On pushes to `main`, pull requests, and manual runs, CI:

1. Checks out the repository.
2. Installs the .NET 8 SDK.
3. Restores `SpacePilot.sln`.
4. Builds the solution in Release configuration.
5. Runs the automated test project.
6. Publishes the WPF app for `win-x64`.
7. Packages the publish output as a zip artifact.
8. Writes a SHA-256 checksum.
9. Uploads test results and the release package as workflow artifacts.

## Signing In CI

CI supports optional signing when these repository secrets are configured:

```text
SPACEPILOT_SIGNING_CERT_BASE64
SPACEPILOT_SIGNING_CERT_PASSWORD
```

`SPACEPILOT_SIGNING_CERT_BASE64` should be a base64-encoded `.pfx` file. When both secrets are present, the workflow imports the certificate into the current-user certificate store and `scripts\package-spacepilot.ps1` signs the published app files.

If secrets are missing, CI still builds, tests, and packages unsigned artifacts.

## Local Equivalent

Run the same core checks on Windows:

```powershell
dotnet restore .\SpacePilot.sln
dotnet build .\SpacePilot.sln -c Release
dotnet test .\tests\SpacePilot.Tests\SpacePilot.Tests.csproj -c Release
.\scripts\package-spacepilot.ps1 -Configuration Release -Runtime win-x64 -SkipSigning
```

## Artifacts

CI uploads:

- `test-results`
- `spacepilot-win-x64`

The package artifact contains:

- `SpacePilot-<version>-win-x64.zip`
- `SpacePilot-<version>-win-x64.zip.sha256`
