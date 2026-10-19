import Darwin
import Foundation

private let permissiveValidationPolicy = ProtectionPolicy(protectedExtensions: [], protectedPaths: [])

enum ValidationFailure: Error, CustomStringConvertible {
    case failed(String)

    var description: String {
        switch self {
        case .failed(let message):
            return message
        }
    }
}

func expect(_ condition: @autoclosure () -> Bool, _ message: String) throws {
    if !condition() {
        throw ValidationFailure.failed(message)
    }
}

func validatePathSafety() throws {
    let root = "/tmp/spacepilot-root"

    try expect(
        MacPathSafety.isAllowedCleanupTarget(candidatePath: "/tmp/spacepilot-root/cache/item.tmp", approvedRoot: root),
        "Child path inside approved root should be allowed."
    )
    try expect(
        !MacPathSafety.isAllowedCleanupTarget(candidatePath: root, approvedRoot: root),
        "Approved root itself should not be a cleanup target."
    )
    try expect(
        !MacPathSafety.isAllowedCleanupTarget(candidatePath: "/tmp/spacepilot-root-sibling/item.tmp", approvedRoot: root),
        "Sibling paths with matching prefixes should be rejected."
    )
}

func validateCleanupRules() throws {
    let home = URL(fileURLWithPath: "/Users/tester", isDirectory: true)
    let rules = MacCleanupRuleCatalog.defaultRules(home: home, temporaryDirectory: "/tmp/tester")
    let blocked = ["/Desktop", "/Documents", "/Downloads", "/Pictures", "/Movies", "/Music"]

    try expect(!rules.isEmpty, "Cleanup rules should not be empty.")
    try expect(Set(rules.map(\.id)).count == rules.count, "Cleanup rule IDs should be unique.")
    try expect(rules.allSatisfy { !$0.name.isEmpty && !$0.locations.isEmpty }, "Cleanup rules should have names and locations.")

    for location in rules.flatMap(\.locations) {
        try expect(
            !blocked.contains { location.rootPath.contains($0) },
            "Cleanup rules should not directly target broad personal folders."
        )
    }
}

func validateCleanupScanBudget() throws {
    let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
    defer { try? FileManager.default.removeItem(at: root) }
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
    for index in 0..<3 {
        try Data([UInt8(index)]).write(to: root.appendingPathComponent("ignored-\(index).tmp"))
    }
    let rule = CleanupRule(id: "budget", name: "Budget", description: "Budget validation", defaultSelected: false, risk: .low, minimumAgeDays: 0, locations: [CleanupLocation(rootPath: root.path, includeSubdirectories: true, includeFiles: false, includeDirectories: false)])
    let entryLimited = MacCleanerService(limits: StorageScanLimits(maxInspectedEntries: 1, maxHashBytes: 0, maxElapsedSeconds: 90))
    let partial = entryLimited.scan(rules: [rule], policy: permissiveValidationPolicy)
    try expect(partial.candidates.isEmpty && partial.warnings.contains(where: { $0.contains("entry inspection limit") && $0.contains("partial") }), "Cleanup must stop at its entry budget even when no files qualify.")
    let timeLimited = MacCleanerService(limits: StorageScanLimits(maxInspectedEntries: 100, maxHashBytes: 0, maxElapsedSeconds: 0))
    try expect(timeLimited.scan(rules: [rule], policy: permissiveValidationPolicy).warnings.contains(where: { $0.contains("time limit") && $0.contains("partial") }), "Cleanup time budget must report incomplete results.")
}

func validateCleanupCoverageAndRootSafety() throws {
    let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
    defer { try? FileManager.default.removeItem(at: root) }
    let first = root.appendingPathComponent("first", isDirectory: true)
    let second = root.appendingPathComponent("second", isDirectory: true)
    for directory in [first, second] {
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
    }
    for (index, size) in [8_192, 16_384, 32_768, 65_536].enumerated() {
        try Data(repeating: UInt8(index), count: size).write(to: first.appendingPathComponent("file-\(index).bin"))
    }
    let secondFile = second.appendingPathComponent("small.bin")
    try Data(repeating: 7, count: 4_096).write(to: secondFile)
    let rules = [first, second].enumerated().map { index, directory in
        CleanupRule(id: "category-\(index)", name: "Category \(index)", description: "Validation cache", defaultSelected: false, risk: .medium, minimumAgeDays: 0, locations: [CleanupLocation(rootPath: directory.path, includeSubdirectories: true, includeFiles: true, includeDirectories: false)])
    }

    let scan = MacCleanerService().scan(rules: rules, policy: permissiveValidationPolicy, maxCandidates: 2)
    try expect(Set(scan.candidates.map(\.categoryId)) == Set(["category-0", "category-1"]), "Candidate cap must reserve review space for both categories.")
    try expect(scan.candidates.contains(where: { $0.path == (MacFileSafety.canonicalDirectoryPath(first.path) ?? first.path) + "/file-3.bin" }), "Cleanup scan must retain the largest file rather than the first encountered.")
    try expect(scan.candidates.contains(where: { $0.path == (MacFileSafety.canonicalDirectoryPath(second.path) ?? second.path) + "/small.bin" }), "Smaller categories must retain a reviewable candidate.")
    try expect(scan.warnings.contains(where: { $0.contains("Showing 2 of 5") && $0.contains("partial") }), "Candidate cap must report omitted eligible files.")

    let limited = MacCleanerService(limits: StorageScanLimits(maxInspectedEntries: 2, maxHashBytes: 0, maxElapsedSeconds: 90)).scan(rules: rules, policy: permissiveValidationPolicy, maxCandidates: 2)
    try expect(Set(limited.candidates.map(\.categoryId)) == Set(["category-0", "category-1"]), "Entry budget must visit each category before stopping.")
    try expect(limited.warnings.contains(where: { $0.contains("entry inspection limit") && $0.contains("partial") }), "Entry cap must report partial coverage.")

    let ageRoot = root.appendingPathComponent("age", isDirectory: true)
    try FileManager.default.createDirectory(at: ageRoot, withIntermediateDirectories: true)
    let ageFile = ageRoot.appendingPathComponent("cache.bin")
    try Data(repeating: 4, count: 8_192).write(to: ageFile)
    try FileManager.default.setAttributes([.modificationDate: Date().addingTimeInterval(-5 * 86_400)], ofItemAtPath: ageFile.path)
    let ageRule = CleanupRule(id: "age", name: "Age", description: "Anchored metadata validation", defaultSelected: false, risk: .medium, minimumAgeDays: 3, locations: [CleanupLocation(rootPath: ageRoot.path, includeSubdirectories: true, includeFiles: true, includeDirectories: false)])
    let oldScan = MacCleanerService().scan(rules: [ageRule], policy: permissiveValidationPolicy)
    try expect(oldScan.candidates.count == 1, "An aged cache file should be eligible.")
    let ageRootFD = MacFileSafety.openApprovedRootWithoutFollowingLinks(ageRoot.path)
    guard let ageRootFD else { throw ValidationFailure.failed("Expected to open the age-test root.") }
    defer { Darwin.close(ageRootFD) }
    try Data(repeating: 5, count: 4_096).write(to: ageFile, options: .atomic)
    guard let replacementMetadata = MacFileSafety.metadata(relativePath: "cache.bin", beneathRootFD: ageRootFD) else {
        throw ValidationFailure.failed("Expected descriptor-relative metadata for the replacement file.")
    }
    let replacementDate = Date(timeIntervalSince1970: TimeInterval(replacementMetadata.st_mtimespec.tv_sec)
        + TimeInterval(replacementMetadata.st_mtimespec.tv_nsec) / 1_000_000_000)
    try expect(replacementDate > Date().addingTimeInterval(-86_400), "Descriptor-relative metadata must report the fresh replacement, not the old file's age.")
    let freshScan = MacCleanerService().scan(rules: [ageRule], policy: permissiveValidationPolicy)
    try expect(freshScan.candidates.isEmpty, "A fresh replacement at an old cache path must not be eligible.")

    let home = root.appendingPathComponent("home", isDirectory: true)
    let outside = root.appendingPathComponent("outside", isDirectory: true)
    let outsideCaches = outside.appendingPathComponent("Caches", isDirectory: true)
    try FileManager.default.createDirectory(at: home, withIntermediateDirectories: true)
    try FileManager.default.createDirectory(at: outsideCaches, withIntermediateDirectories: true)
    try Data(repeating: 1, count: 4_096).write(to: outside.appendingPathComponent("dot-cache.bin"))
    try Data(repeating: 2, count: 4_096).write(to: outsideCaches.appendingPathComponent("library-cache.bin"))
    try FileManager.default.createSymbolicLink(at: home.appendingPathComponent(".cache"), withDestinationURL: outside)
    try FileManager.default.createSymbolicLink(at: home.appendingPathComponent("Library"), withDestinationURL: outside)
    let cacheRules = MacCleanupRuleCatalog.defaultRules(home: home).filter { ["dot-cache", "user-caches"].contains($0.id) }
    let unsafe = MacCleanerService().scan(rules: cacheRules, policy: permissiveValidationPolicy)
    try expect(unsafe.candidates.isEmpty, "Cleanup must not traverse symlinked cache roots or ancestors.")
    try expect(unsafe.warnings.filter { $0.contains("symbolic-link component") }.count == 2, "Skipped symlinked roots must be visible to the user.")

    let approved = root.appendingPathComponent("approved", isDirectory: true)
    let movedRoot = root.appendingPathComponent("approved-renamed", isDirectory: true)
    try FileManager.default.createDirectory(at: approved, withIntermediateDirectories: true)
    let originalFile = approved.appendingPathComponent("cache.bin")
    try Data(repeating: 3, count: 4_096).write(to: originalFile)
    let approvedRule = CleanupRule(id: "approved", name: "Approved", description: "Root swap validation", defaultSelected: false, risk: .medium, minimumAgeDays: 0, locations: [CleanupLocation(rootPath: approved.path, includeSubdirectories: true, includeFiles: true, includeDirectories: false)])
    let scanned = MacCleanerService().scan(rules: [approvedRule], policy: permissiveValidationPolicy)
    guard let pinnedCandidate = scanned.candidates.first else { throw ValidationFailure.failed("Expected a scanned candidate for root-swap validation.") }
    try expect(pinnedCandidate.approvedRootIdentity != nil, "Scanned candidates must record the approved root identity.")
    try FileManager.default.moveItem(at: approved, to: movedRoot)
    try FileManager.default.createDirectory(at: approved, withIntermediateDirectories: false)
    let replacementFile = approved.appendingPathComponent("cache.bin")
    try FileManager.default.linkItem(at: movedRoot.appendingPathComponent("cache.bin"), to: replacementFile)
    let quarantined = QuarantineService(root: root.appendingPathComponent("quarantine", isDirectory: true))
        .quarantine([pinnedCandidate], policy: permissiveValidationPolicy)
    try expect(quarantined.entries.isEmpty && !quarantined.warnings.isEmpty, "Replacing an approved root must be rejected even when the target is the same hard-linked file.")
    try expect(FileManager.default.fileExists(atPath: replacementFile.path), "Root-swap rejection must leave the replacement file untouched.")

    let pinnedRootFD = MacFileSafety.openApprovedRootWithoutFollowingLinks(movedRoot.path)
    guard let pinnedRootFD else { throw ValidationFailure.failed("Expected to open the original root without following links.") }
    defer { Darwin.close(pinnedRootFD) }
    guard let originalIdentity = MacFileSafety.identity(relativePath: "cache.bin", beneathRootFD: pinnedRootFD) else {
        throw ValidationFailure.failed("Expected to inspect the original cache file relative to its pinned root.")
    }
    try FileManager.default.removeItem(at: approved)
    try FileManager.default.createSymbolicLink(at: approved, withDestinationURL: outside)
    try expect(MacFileSafety.identity(relativePath: "cache.bin", beneathRootFD: pinnedRootFD) == originalIdentity, "Descriptor-relative inspection must stay on the original root after a path swap.")
    try expect(MacFileSafety.openApprovedRootWithoutFollowingLinks(approved.path) == nil, "A replaced symlink root must not reopen as an approved root.")
}

func validateQuarantineRoundTrip() throws {
    let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
    defer { try? FileManager.default.removeItem(at: root) }

    let approved = root.appendingPathComponent("approved", isDirectory: true)
    let quarantine = root.appendingPathComponent("quarantine", isDirectory: true)
    try FileManager.default.createDirectory(at: approved, withIntermediateDirectories: true)

    let file = approved.appendingPathComponent("cache.tmp")
    try "temporary data".write(to: file, atomically: true, encoding: .utf8)

    let candidate = CleanupCandidate(
        categoryId: "test",
        categoryName: "Test",
        displayName: "cache.tmp",
        path: file.path,
        approvedRoot: approved.path,
        kind: .file,
        sizeBytes: 14,
        lastModified: Date(),
        risk: .low,
        identity: MacFileSafety.identity(at: file.path)
    )

    let service = QuarantineService(root: quarantine)
    try expect(MacFileSafety.canonicalDirectoryPath(approved.path) != nil, "Could not resolve approved root")
    let result = service.quarantine([candidate], policy: permissiveValidationPolicy)
    try expect(result.warnings.isEmpty, "Quarantine should not warn for an allowed file: \(result.warnings)")
    try expect(result.entries.count == 1, "Quarantine should create one entry.")

    let entry = result.entries[0]
    try expect(!FileManager.default.fileExists(atPath: file.path), "Original file should move into quarantine.")
    try expect(FileManager.default.fileExists(atPath: entry.payloadPath), "Quarantine payload should exist.")

    let warnings = service.restore(ids: [entry.id])
    try expect(warnings.isEmpty, "Restore should not warn for a valid entry.")
    try expect(FileManager.default.fileExists(atPath: file.path), "Restored file should exist at the original path.")
    try expect(service.entries().isEmpty, "Quarantine manifest should be empty after restore.")
}

func validateRejectedQuarantineTarget() throws {
    let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
    defer { try? FileManager.default.removeItem(at: root) }

    let approved = root.appendingPathComponent("approved", isDirectory: true)
    let outside = root.appendingPathComponent("outside", isDirectory: true)
    let quarantine = root.appendingPathComponent("quarantine", isDirectory: true)
    try FileManager.default.createDirectory(at: approved, withIntermediateDirectories: true)
    try FileManager.default.createDirectory(at: outside, withIntermediateDirectories: true)

    let file = outside.appendingPathComponent("cache.tmp")
    try "keep me".write(to: file, atomically: true, encoding: .utf8)

    let candidate = CleanupCandidate(
        categoryId: "test",
        categoryName: "Test",
        displayName: "cache.tmp",
        path: file.path,
        approvedRoot: approved.path,
        kind: .file,
        sizeBytes: 7,
        lastModified: Date(),
        risk: .low,
        identity: MacFileSafety.identity(at: file.path)
    )

    let result = QuarantineService(root: quarantine).quarantine([candidate], policy: permissiveValidationPolicy)
    try expect(result.entries.isEmpty, "Quarantine should reject files outside the approved root.")
    try expect(result.warnings.count == 1, "Rejected target should produce one warning.")
    try expect(FileManager.default.fileExists(atPath: file.path), "Rejected file should remain in place.")
}

func validateReceiptsAndPreferences() throws {
    let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
    defer { try? FileManager.default.removeItem(at: root) }

    let receiptService = ReceiptService(root: root.appendingPathComponent("receipts", isDirectory: true))
    receiptService.save(CleanupReceipt(id: "older", timestamp: Date().addingTimeInterval(-60), mode: "Test", requestedCount: 1, completedCount: 1, requestedBytes: 10, completedBytes: 10, items: [], warnings: []))
    receiptService.save(CleanupReceipt(id: "newer", timestamp: Date(), mode: "Test", requestedCount: 1, completedCount: 1, requestedBytes: 20, completedBytes: 20, items: [], warnings: []))
    let legacyJSON = """
    {"id":"legacy","timestamp":"2020-01-01T00:00:00Z","mode":"macOS cleanup","requestedCount":1,"completedCount":1,"requestedBytes":10,"completedBytes":10}
    """
    try Data(legacyJSON.utf8).write(to: root.appendingPathComponent("receipts/legacy.json"))
    try expect(receiptService.recent().map(\.id) == ["newer", "older", "legacy"], "Receipts should preserve legacy entries and return newest first.")
    try expect(receiptService.recent().last?.items.isEmpty == true && receiptService.recent().last?.warnings.isEmpty == true, "Legacy receipts should default missing detail and warnings to empty arrays.")
    try expect(receiptService.diagnostics().isEmpty, "Legacy receipts should not be flagged as unreadable.")
    let receiptsRoot = root.appendingPathComponent("receipts", isDirectory: true)
    let oversized = receiptsRoot.appendingPathComponent("oversized.json")
    try Data(repeating: 65, count: 128 * 1_024 + 1).write(to: oversized)
    try expect(receiptService.diagnostics().contains(where: { $0.contains("oversized.json") && $0.contains("size limit") }), "Oversized receipt files must be skipped with a visible diagnostic.")
    try FileManager.default.removeItem(at: oversized)
    for index in 0..<101 {
        try Data("{}".utf8).write(to: receiptsRoot.appendingPathComponent("flood-\(index).json"))
    }
    try expect(receiptService.diagnostics().contains(where: { $0.contains("newest 100 receipt files") }), "Receipt loading must cap decoded files and report omitted records.")

    let preferenceService = PreferencesService(url: root.appendingPathComponent("preferences.json"))
    var preferences = MacPreferences()
    preferences.largeFileMinimumMB = 512
    preferences.protectedExtensions = [".safe"]
    preferenceService.save(preferences)

    let loaded = preferenceService.load()
    try expect(loaded.largeFileMinimumMB == 512, "Preferences should round-trip numeric settings.")
    try expect(loaded.protectedExtensions == [".safe"], "Preferences should round-trip protected extensions.")
}

func validatePerformanceAssist() throws {
    try expect(
        PerformanceAssistService.classifyMemoryPressure(45) == "Good",
        "Healthy memory pressure should be classified as Good."
    )
    try expect(
        PerformanceAssistService.classifyMemoryPressure(90) == "Critical",
        "Very high memory pressure should be classified as Critical."
    )

    let vmStat = """
    Mach Virtual Memory Statistics: (page size of 4096 bytes)
    Pages free:                               1000.
    Pages active:                             4000.
    Pages inactive:                           2000.
    Pages speculative:                         500.
    """
    let memory = PerformanceAssistService.parseVMStat(vmStat, physicalMemory: 34_359_738_368)
    try expect(memory.availableBytes == 14_336_000, "vm_stat parser should include free, inactive, and speculative pages.")
    let extremeVMStat = """
    Mach Virtual Memory Statistics: (page size of 4096 bytes)
    Pages free: 9223372036854775807.
    Pages inactive: 9223372036854775807.
    Pages speculative: 9223372036854775807.
    """
    let extremeMemory = PerformanceAssistService.parseVMStat(extremeVMStat, physicalMemory: 34_359_738_368)
    try expect(extremeMemory.availableBytes <= extremeMemory.totalBytes && extremeMemory.usagePercent >= 0, "Extreme vm_stat page counts must not overflow or exceed physical memory.")

    let processes = PerformanceAssistService.parseProcessList("  101 1048576 /Applications/Xcode.app/Contents/MacOS/Xcode")
    try expect(processes.first?.name == "Xcode", "Process parser should expose the app name from the command path.")
    try expect(processes.first?.residentMemoryBytes == 1_073_741_824, "Process parser should convert RSS KB to bytes.")
    try expect(PerformanceAssistService.parseProcessList("  101 9223372036854775807 /Applications/Invalid.app").isEmpty, "Out-of-range process RSS must be rejected without overflow.")
    try expect(PerformanceAssistService.run("/bin/echo", arguments: ["ready"]).trimmingCharacters(in: .whitespacesAndNewlines) == "ready", "Bounded helper runner should return normal output.")
    let timeoutStart = ProcessInfo.processInfo.systemUptime
    try expect(PerformanceAssistService.run("/bin/sleep", arguments: ["2"], timeoutSeconds: 0.1).isEmpty, "A timed-out helper must yield unavailable output.")
    try expect(ProcessInfo.processInfo.systemUptime - timeoutStart < 1.8, "A timed-out helper must not keep RAM Assist busy indefinitely.")
    let outputStart = ProcessInfo.processInfo.systemUptime
    try expect(PerformanceAssistService.run("/bin/sh", arguments: ["-c", "while :; do printf x; done"], timeoutSeconds: 2, maxOutputBytes: 1_024).isEmpty, "Excess helper output must be rejected.")
    try expect(ProcessInfo.processInfo.systemUptime - outputStart < 2, "Output truncation must stop the helper promptly.")
}

func validateScanWarningLimit() throws {
    var result = StorageScanResult<Int>()
    for index in 0..<1_000 {
        result.addWarning("Could not inspect /sample/\(index)")
    }
    result.addWarning("Scan stopped at the resource limit; results are partial.", priority: true)
    try expect(result.warnings.count == ScanWarningBuffer.detailedLimit + 2, "Scan warning storage must remain bounded while retaining a summary and limit warning.")
    try expect(result.warnings.contains(where: { $0.contains("968 additional scan warnings") }), "Suppressed warning count must be visible to the user.")
    try expect(result.warnings.contains(where: { $0.contains("results are partial") }), "A scan limit warning must remain visible after per-file warnings are capped.")
}

func validateQuarantineRecoveryAndBoundaries() throws {
    let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
    defer { try? FileManager.default.removeItem(at: root) }
    let approved = root.appendingPathComponent("approved", isDirectory: true)
    let outside = root.appendingPathComponent("outside", isDirectory: true)
    let quarantine = root.appendingPathComponent("quarantine", isDirectory: true)
    try FileManager.default.createDirectory(at: approved, withIntermediateDirectories: true)
    try FileManager.default.createDirectory(at: outside, withIntermediateDirectories: true)
    try FileManager.default.createDirectory(at: quarantine, withIntermediateDirectories: true)
    let service = QuarantineService(root: quarantine)

    let outsideFile = outside.appendingPathComponent("keep.txt")
    try "keep".write(to: outsideFile, atomically: true, encoding: .utf8)
    let forged = QuarantineEntry(id: "forged", originalPath: outsideFile.path, payloadPath: outsideFile.path, displayName: "keep.txt", categoryName: "Test", kind: .file, sizeBytes: 4, quarantinedAt: Date(), originalLastModified: nil)
    let encoder = JSONEncoder()
    encoder.dateEncodingStrategy = .iso8601
    try encoder.encode([forged]).write(to: quarantine.appendingPathComponent("manifest.json"))
    try expect(!service.diagnostics().isEmpty, "Invalid manifest should produce recovery diagnostics.")
    _ = service.purge(ids: ["forged"])
    try expect(FileManager.default.fileExists(atPath: outsideFile.path), "Forged manifest must not delete an external file.")

    let file = approved.appendingPathComponent("cache.tmp")
    try "temporary".write(to: file, atomically: true, encoding: .utf8)
    let candidate = CleanupCandidate(categoryId: "test", categoryName: "Test", displayName: "cache.tmp", path: file.path, approvedRoot: approved.path, kind: .file, sizeBytes: 9, lastModified: Date(), risk: .low, identity: MacFileSafety.identity(at: file.path))
    try FileManager.default.removeItem(at: quarantine.appendingPathComponent("manifest.json"))
    try FileManager.default.createDirectory(at: quarantine.appendingPathComponent("manifest.json"), withIntermediateDirectories: false)
    let quarantined = service.quarantine([candidate], policy: permissiveValidationPolicy)
    try expect(quarantined.entries.count == 1, "A manifest write failure should not lose a moved item's recovery record.")
    try expect(!quarantined.warnings.isEmpty, "A manifest write failure must be reported.")
    try expect(service.entries().count == 1, "Per-entry sidecar should recover a moved item after a manifest write failure.")

    let conflict = approved.appendingPathComponent("cache.tmp")
    try "new file".write(to: conflict, atomically: true, encoding: .utf8)
    let entryID = quarantined.entries[0].id
    let conflictWarnings = service.restore(ids: [entryID])
    try expect(!conflictWarnings.isEmpty, "Restore to an occupied original path should warn.")
    let conflictContents = try String(contentsOf: conflict, encoding: .utf8)
    try expect(conflictContents == "new file", "Restore must not overwrite the new file.")
    let alternate = root.appendingPathComponent("alternate", isDirectory: true)
    try FileManager.default.createDirectory(at: alternate, withIntermediateDirectories: true)
    _ = service.restore(ids: [entryID], destinationDirectory: alternate)
    try expect(FileManager.default.fileExists(atPath: alternate.appendingPathComponent("cache.tmp").path), "Restore to another folder should recover the quarantined file.")

    try FileManager.default.removeItem(at: quarantine.appendingPathComponent("manifest.json"))
    let disposable = approved.appendingPathComponent("disposable.tmp")
    try "disposable".write(to: disposable, atomically: true, encoding: .utf8)
    let disposableCandidate = CleanupCandidate(categoryId: "test", categoryName: "Test", displayName: "disposable.tmp", path: disposable.path, approvedRoot: approved.path, kind: .file, sizeBytes: 10, lastModified: Date(), risk: .low, identity: MacFileSafety.identity(at: disposable.path))
    let disposableResult = service.quarantine([disposableCandidate], policy: permissiveValidationPolicy)
    try expect(disposableResult.entries.count == 1, "File should quarantine before explicit purge.")
    let purgeWarnings = service.purge(ids: [disposableResult.entries[0].id])
    try expect(purgeWarnings.isEmpty, "Valid file purge should complete without warnings: \(purgeWarnings)")
    try expect(!FileManager.default.fileExists(atPath: disposableResult.entries[0].payloadPath), "Purge should remove only the selected quarantine payload.")
    try expect(FileManager.default.fileExists(atPath: outsideFile.path), "Purge must leave unrelated files untouched.")

    let stale = approved.appendingPathComponent("stale.tmp")
    try "before".write(to: stale, atomically: true, encoding: .utf8)
    let staleCandidate = CleanupCandidate(categoryId: "test", categoryName: "Test", displayName: "stale.tmp", path: stale.path, approvedRoot: approved.path, kind: .file, sizeBytes: 6, lastModified: Date(), risk: .high, identity: MacFileSafety.identity(at: stale.path))
    let foldersBefore = Set(try FileManager.default.contentsOfDirectory(atPath: quarantine.path))
    try "changed after scan".write(to: stale, atomically: true, encoding: .utf8)
    let staleResult = service.quarantine([staleCandidate], policy: permissiveValidationPolicy)
    let foldersAfter = Set(try FileManager.default.contentsOfDirectory(atPath: quarantine.path))
    try expect(staleResult.entries.isEmpty && !staleResult.warnings.isEmpty, "Changed file must not be quarantined.")
    try expect(foldersAfter == foldersBefore, "Rejected move must not leave an orphan recovery sidecar or folder.")
    try expect(FileManager.default.fileExists(atPath: stale.path), "Rejected file must remain in place.")

    let medium = approved.appendingPathComponent("medium.tmp")
    try "before".write(to: medium, atomically: true, encoding: .utf8)
    let mediumCandidate = CleanupCandidate(categoryId: "test", categoryName: "Test", displayName: "medium.tmp", path: medium.path, approvedRoot: approved.path, kind: .file, sizeBytes: 6, lastModified: Date(), risk: .medium, identity: MacFileSafety.identity(at: medium.path))
    let mediumHandle = try FileHandle(forWritingTo: medium)
    try mediumHandle.write(contentsOf: Data("after!".utf8))
    try mediumHandle.close()
    try FileManager.default.setAttributes([.modificationDate: Date().addingTimeInterval(3_600)], ofItemAtPath: medium.path)
    let mediumResult = service.quarantine([mediumCandidate], policy: permissiveValidationPolicy)
    try expect(mediumResult.entries.isEmpty && !mediumResult.warnings.isEmpty && FileManager.default.fileExists(atPath: medium.path), "A medium-risk file changed after scanning must not be quarantined, even when its inode and size are unchanged.")

    let linked = approved.appendingPathComponent("linked", isDirectory: true)
    try FileManager.default.createSymbolicLink(at: linked, withDestinationURL: outside)
    let linkedCandidate = CleanupCandidate(categoryId: "test", categoryName: "Test", displayName: "keep.txt", path: linked.appendingPathComponent("keep.txt").path, approvedRoot: approved.path, kind: .file, sizeBytes: 4, lastModified: Date(), risk: .low, identity: MacFileSafety.identity(at: linked.appendingPathComponent("keep.txt").path))
    let linkedResult = service.quarantine([linkedCandidate], policy: permissiveValidationPolicy)
    try expect(linkedResult.entries.isEmpty, "A path through a replaced ancestor link must be rejected.")
    try expect(FileManager.default.fileExists(atPath: outsideFile.path), "Linked outside file should remain untouched.")

    let newlyProtected = approved.appendingPathComponent("newly-protected.tmp")
    try "preserve".write(to: newlyProtected, atomically: true, encoding: .utf8)
    let protectedCandidate = CleanupCandidate(categoryId: "test", categoryName: "Test", displayName: "newly-protected.tmp", path: newlyProtected.path, approvedRoot: approved.path, kind: .file, sizeBytes: 8, lastModified: Date(), risk: .low, identity: MacFileSafety.identity(at: newlyProtected.path))
    let protectedResult = service.quarantine([protectedCandidate], policy: ProtectionPolicy(protectedExtensions: [".tmp"], protectedPaths: []))
    try expect(protectedResult.entries.isEmpty && !protectedResult.warnings.isEmpty && FileManager.default.fileExists(atPath: newlyProtected.path), "Current extension protection must be rechecked before mutation.")

    let folder = approved.appendingPathComponent("collection", isDirectory: true)
    try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
    let nestedProtected = folder.appendingPathComponent("keep.key")
    try "preserve".write(to: nestedProtected, atomically: true, encoding: .utf8)
    let folderCandidate = CleanupCandidate(categoryId: "test", categoryName: "Test", displayName: "collection", path: folder.path, approvedRoot: approved.path, kind: .directory, sizeBytes: 8, lastModified: Date(), risk: .low, identity: MacFileSafety.identity(at: folder.path))
    let protectedDescendant = service.quarantine([folderCandidate], policy: ProtectionPolicy(protectedExtensions: [".key"], protectedPaths: []))
    let protectedPath = service.quarantine([folderCandidate], policy: ProtectionPolicy(protectedExtensions: [], protectedPaths: [nestedProtected.path]))
    try expect(protectedDescendant.entries.isEmpty && protectedPath.entries.isEmpty && FileManager.default.fileExists(atPath: nestedProtected.path), "Directory mutation must respect protected descendants by extension and path.")
}

func validateDuplicateKeeperAndOverlap() throws {
    let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
    defer { try? FileManager.default.removeItem(at: root) }
    let nested = root.appendingPathComponent("nested", isDirectory: true)
    try FileManager.default.createDirectory(at: nested, withIntermediateDirectories: true)
    let first = root.appendingPathComponent("first.bin")
    let second = nested.appendingPathComponent("second.bin")
    let contents = Data(repeating: 42, count: 1_048_576)
    try contents.write(to: first)
    try contents.write(to: second)
    let policy = ProtectionPolicy(protectedExtensions: [], protectedPaths: [])
    let storage = StorageAnalysisService(roots: [root, nested])
    let large = storage.scanLargeFiles(minimumMB: 1, policy: policy)
    try expect(large.items.count == 2, "Overlapping storage roots should not count a file twice.")
    let duplicate = storage.scanDuplicates(minimumMB: 1, policy: policy)
    try expect(duplicate.items.count == 2, "Overlapping roots should produce one two-file duplicate group.")
    let selection = Set([duplicate.items[1].id])
    let withoutKeeper = storage.validateDuplicateSelection(all: duplicate.items, selectedIDs: selection, keeperIDs: [:], policy: policy)
    try expect(withoutKeeper.candidates.isEmpty, "Duplicate cleanup requires an explicit keeper.")
    let keeper = duplicate.items[0]
    let validated = storage.validateDuplicateSelection(all: duplicate.items, selectedIDs: selection, keeperIDs: [keeper.groupId: keeper.id], policy: policy)
    try expect(validated.candidates.count == 1, "Unchanged selected duplicate with verified keeper should be actionable.")
    let entryLimited = StorageAnalysisService(roots: [root], limits: StorageScanLimits(maxInspectedEntries: 1, maxHashBytes: 8 * 1_073_741_824, maxElapsedSeconds: 90))
    let partialLarge = entryLimited.scanLargeFiles(minimumMB: Int.max, policy: policy)
    try expect(partialLarge.warnings.contains(where: { $0.contains("partial") && $0.contains("entry inspection limit") }), "Entry budget must report a partial large-file scan, including extreme thresholds.")

    let hashLimited = StorageAnalysisService(roots: [root], limits: StorageScanLimits(maxInspectedEntries: 100, maxHashBytes: 1_048_576, maxElapsedSeconds: 90))
    let partialDuplicate = hashLimited.scanDuplicates(minimumMB: 1, policy: policy)
    try expect(partialDuplicate.items.isEmpty, "Incomplete hashing must not emit an unverified duplicate group.")
    try expect(partialDuplicate.warnings.contains(where: { $0.contains("partial") && $0.contains("hashing limit") }), "Hash budget must report partial duplicate results.")
    let verificationLimited = hashLimited.validateDuplicateSelection(all: duplicate.items, selectedIDs: selection, keeperIDs: [keeper.groupId: keeper.id], policy: policy)
    try expect(verificationLimited.candidates.isEmpty && verificationLimited.warnings.contains(where: { $0.contains("partial") }), "Action-time duplicate verification must respect the hash budget.")

    let selectedPath = URL(fileURLWithPath: duplicate.items[1].path)
    try Data(repeating: 17, count: 1_048_576).write(to: selectedPath)
    let stale = storage.validateDuplicateSelection(all: duplicate.items, selectedIDs: selection, keeperIDs: [keeper.groupId: keeper.id], policy: policy)
    try expect(stale.candidates.isEmpty, "Changed duplicate content must be rejected before quarantine.")

    let timeLimited = StorageAnalysisService(roots: [root], limits: StorageScanLimits(maxInspectedEntries: 100, maxHashBytes: 8 * 1_073_741_824, maxElapsedSeconds: 0))
    try expect(timeLimited.scanLargeFiles(minimumMB: 1, policy: policy).warnings.contains(where: { $0.contains("time limit") && $0.contains("partial") }), "Elapsed-time limit must report partial results.")

    let cancellation = ScanCancellation()
    cancellation.cancel()
    try expect(storage.scanLargeFiles(minimumMB: 1, policy: policy, cancellation: cancellation).items.isEmpty && cancellation.inspectedCount == 0, "Cancelled scan must stop before inspecting files.")
}

@main
enum CoreValidation {
    static func main() throws {
        try validatePathSafety()
        try validateCleanupRules()
        try validateCleanupScanBudget()
        try validateCleanupCoverageAndRootSafety()
        try validateQuarantineRoundTrip()
        try validateRejectedQuarantineTarget()
        try validateReceiptsAndPreferences()
        try validatePerformanceAssist()
        try validateScanWarningLimit()
        try validateQuarantineRecoveryAndBoundaries()
        try validateDuplicateKeeperAndOverlap()

        print("SpacePilot macOS core validation passed.")
    }
}
