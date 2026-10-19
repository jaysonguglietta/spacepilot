import Foundation

struct VolumeStorageSnapshot: Hashable, Sendable {
    let capacityBytes: Int64
    let freeBytes: Int64
    let availableForImportantUseBytes: Int64?
    let sampledAt: Date

    // This is a useful comparison with Finder's purgeable figure, but Apple does
    // not promise that the two capacity APIs reproduce Finder's exact accounting.
    var potentiallyReclaimableByMacOSBytes: Int64? {
        guard let availableForImportantUseBytes,
              availableForImportantUseBytes >= freeBytes else { return nil }
        return availableForImportantUseBytes - freeBytes
    }
}

enum VolumeStorageError: LocalizedError {
    case unavailable

    var errorDescription: String? {
        "Disk capacity is unavailable. Refresh to try again."
    }
}

struct VolumeStorageService: Sendable {
    let volumeURL: URL

    // The startup volume is what Finder reports for Macintosh HD. A user's
    // home directory can live on a different volume or network share.
    init(volumeURL: URL = URL(fileURLWithPath: "/", isDirectory: true)) {
        self.volumeURL = volumeURL
    }

    func snapshot() throws -> VolumeStorageSnapshot {
        let values = try volumeURL.resourceValues(forKeys: [
            .volumeTotalCapacityKey,
            .volumeAvailableCapacityKey,
            .volumeAvailableCapacityForImportantUsageKey
        ])
        guard let capacity = values.volumeTotalCapacity, capacity > 0,
              let free = values.volumeAvailableCapacity, free >= 0 else {
            throw VolumeStorageError.unavailable
        }

        let important = values.volumeAvailableCapacityForImportantUsage
        return VolumeStorageSnapshot(
            capacityBytes: Int64(capacity),
            freeBytes: Int64(free),
            availableForImportantUseBytes: important.flatMap { $0 >= Int64(free) ? $0 : nil },
            sampledAt: Date()
        )
    }
}
