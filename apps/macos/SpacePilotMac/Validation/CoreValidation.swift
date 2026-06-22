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
        risk: .low
    )

    let service = QuarantineService(root: quarantine)
    let result = service.quarantine([candidate])
    try expect(result.warnings.isEmpty, "Quarantine should not warn for an allowed file.")
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
        risk: .low
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

@main
enum CoreValidation {
    static func main() throws {
        try validatePathSafety()
        try validateCleanupRules()
        try validateQuarantineRoundTrip()
        try validateRejectedQuarantineTarget()
        try validateReceiptsAndPreferences()

        print("SpacePilot macOS core validation passed.")
    }
}
