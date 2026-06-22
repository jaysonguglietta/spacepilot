# SpacePilot For macOS

SpacePilot for macOS is a native SwiftUI companion app for the Windows version. It keeps the same product philosophy: scan first, review every candidate, quarantine by default, and avoid broad or opaque cleanup.

## Target

- macOS 26.5.1 (25F80), as requested.
- Built as a Swift Package with a SwiftUI executable.
- Minimum package platform is macOS 14 so the app can run on current and future macOS releases.

## App Location

```text
apps/macos/
```

Key paths:

- `apps/macos/SpacePilotMac/Package.swift`
- `apps/macos/SpacePilotMac/Sources/SpacePilotMac/`
- `apps/macos/SpacePilotMac/Tests/SpacePilotMacTests/`
- `apps/macos/SpacePilotMac/Validation/`
- `apps/macos/packaging/`

## What The Mac Version Does

- Scans user-owned macOS cleanup roots.
- Reviews candidates before action.
- Moves selected items to SpacePilot quarantine.
- Restores or purges quarantine.
- Saves local JSON cleanup receipts.
- Scans personal folders for large files.
- Finds duplicate files by size plus SHA-256 hash.
- Keeps activity logs in memory for the current session.
- Stores preferences locally.

## Mac-Specific Cleanup Rules

Default cleanup rules include:

- User temporary files from the current session temp directory.
- User logs in `~/Library/Logs`.
- User app caches in `~/Library/Caches`.
- Xcode DerivedData in `~/Library/Developer/Xcode/DerivedData`.
- Swift Package Manager cache in `~/Library/Caches/org.swift.swiftpm`.

Medium-risk cache rules are opt-in by default.

## Safety Boundaries

SpacePilot for macOS does not:

- Delete broad personal folders automatically.
- Clean system folders outside approved user-owned roots.
- Follow symbolic links.
- Modify system settings.
- Uninstall apps.
- Touch browser cookies, history, or sessions in this first Mac build.
- Upload telemetry.

Large-file and duplicate workflows can show personal files, but quarantine requires explicit selection.

## Local Data

SpacePilot for macOS stores data under:

```text
~/Library/Application Support/SpacePilot/
```

Important paths:

- `preferences-macos.json`
- `Quarantine/manifest.json`
- `Receipts/*.json`

## Step-By-Step: Run From Source

### 1. Open Terminal

Open **Terminal** from Applications or Spotlight.

### 2. Install Apple Command Line Tools

```bash
xcode-select --install
```

If Command Line Tools are already installed, macOS will say so.

### 3. Verify tools

```bash
git --version
swift --version
```

### 4. Clone the repo

Use SSH:

```bash
mkdir -p "$HOME/Source"
cd "$HOME/Source"
git clone git@github.com:jaysonguglietta/spacepilot.git
cd spacepilot
```

Or use HTTPS:

```bash
mkdir -p "$HOME/Source"
cd "$HOME/Source"
git clone https://github.com/jaysonguglietta/spacepilot.git
cd spacepilot
```

### 5. Build and run

```bash
swift build --package-path apps/macos/SpacePilotMac -c release
swift run --package-path apps/macos/SpacePilotMac -c release SpacePilotMac
```

## Step-By-Step: Build And Validate

```bash
swift build --package-path apps/macos/SpacePilotMac -c release
bash scripts/macos/validate-core.sh
```

The validation script compiles the non-UI macOS cleanup services and verifies:

- Approved-root path safety.
- Rejection of cleanup roots and sibling-prefix paths.
- Cleanup-rule boundaries.
- Quarantine and restore.
- Receipt ordering.
- Preference persistence.

When full Xcode/XCTest is available, also run:

```bash
swift test --package-path apps/macos/SpacePilotMac -c release
```

## Step-By-Step: Build A Local App Bundle

```bash
bash scripts/macos/build-app.sh
```

Outputs:

```text
artifacts/macos/SpacePilot.app
artifacts/macos/SpacePilot-macOS.zip
artifacts/macos/SpacePilot-macOS.zip.sha256
```

The local bundle is ad-hoc signed when `codesign` is available. Public distribution still needs Developer ID signing and notarization.

Launch the local app bundle:

```bash
open artifacts/macos/SpacePilot.app
```

Optional user Applications install:

```bash
mkdir -p "$HOME/Applications"
rm -rf "$HOME/Applications/SpacePilot.app"
cp -R artifacts/macos/SpacePilot.app "$HOME/Applications/SpacePilot.app"
open "$HOME/Applications/SpacePilot.app"
```

## Step-By-Step: Update

```bash
git pull
swift build --package-path apps/macos/SpacePilotMac -c release
bash scripts/macos/validate-core.sh
bash scripts/macos/build-app.sh
open artifacts/macos/SpacePilot.app
```

If you copied SpacePilot into `~/Applications`, copy the rebuilt app again:

```bash
rm -rf "$HOME/Applications/SpacePilot.app"
cp -R artifacts/macos/SpacePilot.app "$HOME/Applications/SpacePilot.app"
open "$HOME/Applications/SpacePilot.app"
```

## Step-By-Step: Uninstall Or Reset

Remove app and cloned source:

```bash
rm -rf "$HOME/Applications/SpacePilot.app"
rm -rf "$HOME/Source/spacepilot"
```

Optional local-data reset:

```bash
rm -rf "$HOME/Library/Application Support/SpacePilot"
```

Do not remove local data if you may need to restore quarantined files.

## Local Verification

The current local workspace verified:

- `swift build --package-path apps/macos/SpacePilotMac -c release`
- `bash scripts/macos/validate-core.sh`
- `bash scripts/macos/build-app.sh`

`swift test` requires XCTest. If Apple Command Line Tools are installed without XCTest, use full Xcode for SwiftPM tests.

## Current Limitations

- The Mac app is source-build/package-script based, not an Xcode project.
- Full Xcode is not installed in the current workspace, so `xcodebuild` project validation is unavailable locally.
- The app bundle is ad-hoc signed for local testing only.
- Public distribution still needs Developer ID signing, notarization, a `.dmg` or `.pkg`, and a completed macOS QA pass.
