import Foundation

enum MacSection: String, CaseIterable, Identifiable, Sendable {
    case health = "Health"
    case cleaner = "Cleaner"
    case storage = "Storage"
    case recovery = "Recovery"
    case settings = "Settings"
    case activity = "Activity"

    var id: String { rawValue }
}

enum RiskLevel: String, Codable, CaseIterable, Identifiable, Sendable {
    case low = "Low"
    case medium = "Medium"
    case high = "High"

    var id: String { rawValue }
}

enum CleanupTargetKind: String, Codable, Sendable {
    case file
    case directory
}

struct CleanupRule: Identifiable, Codable, Hashable, Sendable {
    let id: String
    let name: String
    let description: String
    let defaultSelected: Bool
    let risk: RiskLevel
    let minimumAgeDays: Int
    let locations: [CleanupLocation]
}

struct CleanupLocation: Codable, Hashable, Sendable {
    let rootPath: String
    let includeSubdirectories: Bool
    let includeFiles: Bool
    let includeDirectories: Bool
}

struct CleanupCandidate: Identifiable, Codable, Hashable, Sendable {
    let id: UUID
    let categoryId: String
    let categoryName: String
    let displayName: String
    let path: String
    let approvedRoot: String
    let kind: CleanupTargetKind
    let sizeBytes: Int64
    let lastModified: Date?
    let risk: RiskLevel

    init(
        id: UUID = UUID(),
        categoryId: String,
        categoryName: String,
        displayName: String,
        path: String,
        approvedRoot: String,
        kind: CleanupTargetKind,
        sizeBytes: Int64,
        lastModified: Date?,
        risk: RiskLevel
    ) {
        self.id = id
        self.categoryId = categoryId
        self.categoryName = categoryName
        self.displayName = displayName
        self.path = path
        self.approvedRoot = approvedRoot
        self.kind = kind
        self.sizeBytes = sizeBytes
        self.lastModified = lastModified
        self.risk = risk
    }
}

struct CleanupScanResult: Sendable {
    var candidates: [CleanupCandidate] = []
    var warnings: [String] = []

    var totalBytes: Int64 {
        candidates.reduce(0) { $0 + $1.sizeBytes }
    }
}

struct QuarantineEntry: Identifiable, Codable, Hashable, Sendable {
    let id: String
    let originalPath: String
    let payloadPath: String
    let displayName: String
    let categoryName: String
    let kind: CleanupTargetKind
    let sizeBytes: Int64
    let quarantinedAt: Date
    let originalLastModified: Date?
}

struct CleanupReceipt: Identifiable, Codable, Hashable, Sendable {
    let id: String
    let timestamp: Date
    let mode: String
    let requestedCount: Int
    let completedCount: Int
    let requestedBytes: Int64
    let completedBytes: Int64
    let items: [CleanupReceiptItem]
    let warnings: [String]
}

struct CleanupReceiptItem: Codable, Hashable, Sendable {
    let path: String
    let categoryName: String
    let action: String
    let sizeBytes: Int64
    let quarantineId: String?
    let message: String?
}

struct LargeFileInfo: Identifiable, Hashable, Sendable {
    let id = UUID()
    let path: String
    let name: String
    let directory: String
    let sizeBytes: Int64
    let modifiedAt: Date?
    let recommendation: String
}

struct DuplicateFileInfo: Identifiable, Hashable, Sendable {
    let id = UUID()
    let groupId: String
    let path: String
    let name: String
    let directory: String
    let sizeBytes: Int64
    let modifiedAt: Date?
    let sha256: String
    let isRecommendedForCleanup: Bool
}

struct StorageScanResult<T: Sendable>: Sendable {
    var items: [T] = []
    var warnings: [String] = []
}

struct ActivityLogEntry: Identifiable, Hashable, Sendable {
    let id = UUID()
    let timestamp: Date
    let level: String
    let message: String
}

struct MacPreferences: Codable, Equatable, Sendable {
    var isFirstRun = true
    var useQuarantine = true
    var confirmBeforeCleanup = true
    var largeFileMinimumMB = 250
    var duplicateMinimumMB = 25
    var quarantineRetentionDays = 14
    var protectedExtensions: [String] = [".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".key", ".pages", ".numbers"]
    var protectedPaths: [String] = []
}
