import Foundation

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
    let result = service.quarantine([candidate])
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

    let result = QuarantineService(root: quarantine).quarantine([candidate])
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
    try expect(receiptService.recent().map(\.id) == ["newer", "older"], "Receipts should return newest entries first.")

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
    let quarantined = service.quarantine([candidate])
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
    let disposableResult = service.quarantine([disposableCandidate])
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
    let staleResult = service.quarantine([staleCandidate])
    let foldersAfter = Set(try FileManager.default.contentsOfDirectory(atPath: quarantine.path))
    try expect(staleResult.entries.isEmpty && !staleResult.warnings.isEmpty, "Changed file must not be quarantined.")
    try expect(foldersAfter == foldersBefore, "Rejected move must not leave an orphan recovery sidecar or folder.")
    try expect(FileManager.default.fileExists(atPath: stale.path), "Rejected file must remain in place.")

    let linked = approved.appendingPathComponent("linked", isDirectory: true)
    try FileManager.default.createSymbolicLink(at: linked, withDestinationURL: outside)
    let linkedCandidate = CleanupCandidate(categoryId: "test", categoryName: "Test", displayName: "keep.txt", path: linked.appendingPathComponent("keep.txt").path, approvedRoot: approved.path, kind: .file, sizeBytes: 4, lastModified: Date(), risk: .low, identity: MacFileSafety.identity(at: linked.appendingPathComponent("keep.txt").path))
    let linkedResult = service.quarantine([linkedCandidate])
    try expect(linkedResult.entries.isEmpty, "A path through a replaced ancestor link must be rejected.")
    try expect(FileManager.default.fileExists(atPath: outsideFile.path), "Linked outside file should remain untouched.")
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
