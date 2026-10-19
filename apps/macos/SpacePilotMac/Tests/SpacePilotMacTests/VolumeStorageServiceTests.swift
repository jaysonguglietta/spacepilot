import Foundation
import XCTest
@testable import SpacePilotMac

final class VolumeStorageServiceTests: XCTestCase {
    func testDefaultCapacitySourceIsStartupVolume() {
        XCTAssertEqual(VolumeStorageService().volumeURL.path, "/")
    }

    func testSystemReclaimableEstimateIsIncludedInAvailableSpace() {
        let sample = VolumeStorageSnapshot(
            capacityBytes: 500_000,
            freeBytes: 28_000,
            availableForImportantUseBytes: 38_000,
            sampledAt: Date()
        )

        XCTAssertEqual(sample.potentiallyReclaimableByMacOSBytes, 10_000)
        XCTAssertEqual(sample.availableForImportantUseBytes, sample.freeBytes + sample.potentiallyReclaimableByMacOSBytes!)
    }

    func testUnavailableSystemEstimateDoesNotInventPurgeableSpace() {
        let sample = VolumeStorageSnapshot(
            capacityBytes: 500_000,
            freeBytes: 28_000,
            availableForImportantUseBytes: nil,
            sampledAt: Date()
        )

        XCTAssertNil(sample.potentiallyReclaimableByMacOSBytes)
    }
}
