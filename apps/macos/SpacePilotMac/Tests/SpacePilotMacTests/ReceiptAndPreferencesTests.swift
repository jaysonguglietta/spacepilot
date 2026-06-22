import Foundation
import XCTest
@testable import SpacePilotMac

final class ReceiptAndPreferencesTests: XCTestCase {
    func testReceiptServiceReturnsNewestFirst() {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        defer { try? FileManager.default.removeItem(at: root) }

        let service = ReceiptService(root: root)
        service.save(CleanupReceipt(id: "older", timestamp: Date().addingTimeInterval(-60), mode: "Test", requestedCount: 1, completedCount: 1, requestedBytes: 10, completedBytes: 10, items: [], warnings: []))
        service.save(CleanupReceipt(id: "newer", timestamp: Date(), mode: "Test", requestedCount: 1, completedCount: 1, requestedBytes: 20, completedBytes: 20, items: [], warnings: []))

        XCTAssertEqual(service.recent().map(\.id), ["newer", "older"])
    }

    func testPreferencesRoundTrip() {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        defer { try? FileManager.default.removeItem(at: root) }

        let url = root.appendingPathComponent("preferences.json")
        let service = PreferencesService(url: url)
        var preferences = MacPreferences()
        preferences.largeFileMinimumMB = 512
        preferences.protectedExtensions = [".safe"]
        service.save(preferences)

        let loaded = service.load()
        XCTAssertEqual(loaded.largeFileMinimumMB, 512)
        XCTAssertEqual(loaded.protectedExtensions, [".safe"])
    }
}
