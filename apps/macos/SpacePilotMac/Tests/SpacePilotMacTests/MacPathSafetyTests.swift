import Foundation
import XCTest
@testable import SpacePilotMac

final class MacPathSafetyTests: XCTestCase {
    func testAllowsChildPathInsideApprovedRoot() {
        let root = "/tmp/spacepilot-root"
        let candidate = "/tmp/spacepilot-root/cache/item.tmp"

        XCTAssertTrue(MacPathSafety.isAllowedCleanupTarget(candidatePath: candidate, approvedRoot: root))
    }

    func testRejectsApprovedRootItself() {
        let root = "/tmp/spacepilot-root"

        XCTAssertFalse(MacPathSafety.isAllowedCleanupTarget(candidatePath: root, approvedRoot: root))
    }

    func testRejectsSiblingWithSamePrefix() {
        let root = "/tmp/spacepilot-root"
        let candidate = "/tmp/spacepilot-root-sibling/item.tmp"

        XCTAssertFalse(MacPathSafety.isAllowedCleanupTarget(candidatePath: candidate, approvedRoot: root))
    }
}
