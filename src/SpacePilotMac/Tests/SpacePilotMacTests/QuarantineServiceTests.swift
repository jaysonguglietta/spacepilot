import Foundation
import XCTest
@testable import SpacePilotMac

final class QuarantineServiceTests: XCTestCase {
    func testQuarantinesAndRestoresAllowedFile() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        let approved = root.appendingPathComponent("approved", isDirectory: true)
        let quarantine = root.appendingPathComponent("quarantine", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: root) }

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
        XCTAssertTrue(result.warnings.isEmpty)
        let entry = try XCTUnwrap(result.entries.first)
        XCTAssertFalse(FileManager.default.fileExists(atPath: file.path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: entry.payloadPath))

        let warnings = service.restore(ids: [entry.id])
        XCTAssertTrue(warnings.isEmpty)
        XCTAssertTrue(FileManager.default.fileExists(atPath: file.path))
        XCTAssertTrue(service.entries().isEmpty)
    }

    func testSkipsFileOutsideApprovedRoot() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        let approved = root.appendingPathComponent("approved", isDirectory: true)
        let outside = root.appendingPathComponent("outside", isDirectory: true)
        let quarantine = root.appendingPathComponent("quarantine", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: root) }

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
        XCTAssertTrue(result.entries.isEmpty)
        XCTAssertEqual(result.warnings.count, 1)
        XCTAssertTrue(FileManager.default.fileExists(atPath: file.path))
    }
}
