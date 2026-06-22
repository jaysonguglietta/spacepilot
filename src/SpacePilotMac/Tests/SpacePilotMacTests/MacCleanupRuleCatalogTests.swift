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
}
