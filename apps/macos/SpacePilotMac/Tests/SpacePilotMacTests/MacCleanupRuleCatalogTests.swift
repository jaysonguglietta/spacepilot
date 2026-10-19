import Foundation
import XCTest
@testable import SpacePilotMac

final class MacCleanupRuleCatalogTests: XCTestCase {
    func testRulesHaveUniqueIdsAndLocations() {
        let home = URL(fileURLWithPath: "/Users/tester", isDirectory: true)
        let rules = MacCleanupRuleCatalog.defaultRules(home: home, temporaryDirectory: "/tmp/tester")

        XCTAssertFalse(rules.isEmpty)
        XCTAssertEqual(Set(rules.map(\.id)).count, rules.count)
        XCTAssertTrue(rules.allSatisfy { !$0.name.isEmpty && !$0.locations.isEmpty })
    }

    func testRulesDoNotTargetBroadPersonalFolders() {
        let home = URL(fileURLWithPath: "/Users/tester", isDirectory: true)
        let rules = MacCleanupRuleCatalog.defaultRules(home: home, temporaryDirectory: "/tmp/tester")
        let blocked = ["/Desktop", "/Documents", "/Downloads", "/Pictures", "/Movies", "/Music"]

        for location in rules.flatMap(\.locations) {
            XCTAssertFalse(blocked.contains { location.rootPath.contains($0) })
        }
    }

    func testAdditionalCachesAreNarrowOptInRules() throws {
        let home = URL(fileURLWithPath: "/Users/tester", isDirectory: true)
        let rules = MacCleanupRuleCatalog.defaultRules(home: home, temporaryDirectory: "/tmp/tester")
        let expectedRoots = [
            "dot-cache": home.appendingPathComponent(".cache").path,
            "simulator-caches": home.appendingPathComponent("Library/Developer/CoreSimulator/Caches").path,
            "swiftpm-cache": home.appendingPathComponent("Library/Caches/org.swift.swiftpm").path,
            "user-caches": home.appendingPathComponent("Library/Caches").path,
            "xcode-derived-data": home.appendingPathComponent("Library/Developer/Xcode/DerivedData").path
        ]

        for (id, path) in expectedRoots {
            let rule = try XCTUnwrap(rules.first { $0.id == id })
            XCTAssertEqual(rule.locations.map(\.rootPath), [path], id)
            XCTAssertEqual(rule.risk, .medium, id)
            XCTAssertFalse(rule.defaultSelected, id)
            XCTAssertEqual(rule.minimumAgeDays, 3, id)
            XCTAssertTrue(rule.locations.allSatisfy { $0.includeFiles && $0.includeSubdirectories && !$0.includeDirectories }, id)
        }

        let scannedRoots = rules.flatMap(\.locations).map(\.rootPath)
        XCTAssertFalse(scannedRoots.contains { $0.contains("/CoreSimulator/Devices") })
        XCTAssertFalse(scannedRoots.contains { $0.contains("/MobileSync/Backup") })
    }

    func testSpecificSwiftPackageCacheTakesPrecedenceWithoutDoubleCounting() throws {
        let home = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        defer { try? FileManager.default.removeItem(at: home) }
        let swiftCache = home.appendingPathComponent("Library/Caches/org.swift.swiftpm", isDirectory: true)
        try FileManager.default.createDirectory(at: swiftCache, withIntermediateDirectories: true)
        let file = swiftCache.appendingPathComponent("artifact.bin")
        try Data(repeating: 1, count: 4_096).write(to: file)
        try FileManager.default.setAttributes([.modificationDate: Date().addingTimeInterval(-5 * 86_400)], ofItemAtPath: file.path)

        let rules = MacCleanupRuleCatalog.defaultRules(home: home, temporaryDirectory: home.appendingPathComponent("session-temp").path)
        let specializedIndex = try XCTUnwrap(rules.firstIndex { $0.id == "swiftpm-cache" })
        let genericIndex = try XCTUnwrap(rules.firstIndex { $0.id == "user-caches" })
        XCTAssertLessThan(specializedIndex, genericIndex)
        let scan = MacCleanerService().scan(rules: rules, policy: ProtectionPolicy(protectedExtensions: [], protectedPaths: []))
        XCTAssertEqual(scan.candidates.count, 1)
        XCTAssertEqual(scan.candidates.first?.path, (MacFileSafety.canonicalDirectoryPath(swiftCache.path) ?? swiftCache.path) + "/artifact.bin")
        XCTAssertEqual(scan.candidates.first?.categoryId, "swiftpm-cache")
    }

    func testCandidateLimitKeepsLargestFileAndAReviewItemFromEachCategory() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let first = root.appendingPathComponent("first", isDirectory: true)
        let second = root.appendingPathComponent("second", isDirectory: true)
        try FileManager.default.createDirectory(at: first, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: second, withIntermediateDirectories: true)
        for (index, size) in [8_192, 16_384, 32_768, 65_536].enumerated() {
            try Data(repeating: UInt8(index), count: size).write(to: first.appendingPathComponent("file-\(index).bin"))
        }
        let secondFile = second.appendingPathComponent("small.bin")
        try Data(repeating: 7, count: 4_096).write(to: secondFile)
        let rules = [
            CleanupRule(id: "first", name: "First", description: "First test cache", defaultSelected: false, risk: .medium, minimumAgeDays: 0, locations: [CleanupLocation(rootPath: first.path, includeSubdirectories: true, includeFiles: true, includeDirectories: false)]),
            CleanupRule(id: "second", name: "Second", description: "Second test cache", defaultSelected: false, risk: .medium, minimumAgeDays: 0, locations: [CleanupLocation(rootPath: second.path, includeSubdirectories: true, includeFiles: true, includeDirectories: false)])
        ]

        let scan = MacCleanerService().scan(rules: rules, policy: ProtectionPolicy(protectedExtensions: [], protectedPaths: []), maxCandidates: 2)
        XCTAssertEqual(scan.candidates.count, 2)
        XCTAssertEqual(Set(scan.candidates.map(\.categoryId)), Set(["first", "second"]))
        XCTAssertTrue(scan.candidates.contains { $0.path == (MacFileSafety.canonicalDirectoryPath(first.path) ?? first.path) + "/file-3.bin" })
        XCTAssertTrue(scan.candidates.contains { $0.path == (MacFileSafety.canonicalDirectoryPath(second.path) ?? second.path) + "/small.bin" })
        XCTAssertTrue(scan.warnings.contains { $0.contains("Showing 2 of 5") && $0.contains("partial") })
    }

    func testEntryBudgetVisitsOtherCategoryBeforeStopping() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let first = root.appendingPathComponent("first", isDirectory: true)
        let second = root.appendingPathComponent("second", isDirectory: true)
        for directory in [first, second] {
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            for index in 0..<3 {
                try Data(repeating: UInt8(index), count: 4_096).write(to: directory.appendingPathComponent("file-\(index).bin"))
            }
        }
        let rules = [first, second].enumerated().map { index, directory in
            CleanupRule(id: "category-\(index)", name: "Category \(index)", description: "Budget test cache", defaultSelected: false, risk: .medium, minimumAgeDays: 0, locations: [CleanupLocation(rootPath: directory.path, includeSubdirectories: true, includeFiles: true, includeDirectories: false)])
        }
        let limits = StorageScanLimits(maxInspectedEntries: 2, maxHashBytes: 0, maxElapsedSeconds: 90)
        let scan = MacCleanerService(limits: limits).scan(rules: rules, policy: ProtectionPolicy(protectedExtensions: [], protectedPaths: []), maxCandidates: 2)

        XCTAssertEqual(Set(scan.candidates.map(\.categoryId)), Set(["category-0", "category-1"]))
        XCTAssertTrue(scan.warnings.contains { $0.contains("entry inspection limit") && $0.contains("partial") })
    }

    func testSymlinkedCacheRootAndAncestorAreSkipped() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let home = root.appendingPathComponent("home", isDirectory: true)
        let outside = root.appendingPathComponent("outside", isDirectory: true)
        let outsideCaches = outside.appendingPathComponent("Caches", isDirectory: true)
        try FileManager.default.createDirectory(at: home, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: outsideCaches, withIntermediateDirectories: true)
        try Data(repeating: 1, count: 4_096).write(to: outside.appendingPathComponent("dot-cache.bin"))
        try Data(repeating: 2, count: 4_096).write(to: outsideCaches.appendingPathComponent("library-cache.bin"))
        try FileManager.default.createSymbolicLink(at: home.appendingPathComponent(".cache"), withDestinationURL: outside)
        try FileManager.default.createSymbolicLink(at: home.appendingPathComponent("Library"), withDestinationURL: outside)

        let rules = MacCleanupRuleCatalog.defaultRules(home: home).filter { ["dot-cache", "user-caches"].contains($0.id) }
        let scan = MacCleanerService().scan(rules: rules, policy: ProtectionPolicy(protectedExtensions: [], protectedPaths: []))
        XCTAssertTrue(scan.candidates.isEmpty)
        XCTAssertEqual(scan.warnings.filter { $0.contains("symbolic-link component") }.count, 2)
    }
}
