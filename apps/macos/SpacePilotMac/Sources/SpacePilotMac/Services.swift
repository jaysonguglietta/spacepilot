import CryptoKit
import Foundation

enum MacAppPaths {
    static var applicationSupportRoot: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support", isDirectory: true)
        return base.appendingPathComponent("SpacePilot", isDirectory: true)
    }

    static var quarantineRoot: URL {
        applicationSupportRoot.appendingPathComponent("Quarantine", isDirectory: true)
    }

    static var receiptsRoot: URL {
        applicationSupportRoot.appendingPathComponent("Receipts", isDirectory: true)
    }

    static var preferencesURL: URL {
        applicationSupportRoot.appendingPathComponent("preferences-macos.json")
    }
}

enum MacPathSafety {
    static func normalize(_ path: String) -> String {
        let expanded = (path as NSString).expandingTildeInPath
        return URL(fileURLWithPath: expanded).standardized.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
    }

    static func isAllowedCleanupTarget(candidatePath: String, approvedRoot: String) -> Bool {
        guard !candidatePath.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              !approvedRoot.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        else {
            return false
        }

        let candidate = normalize(candidatePath)
        let root = normalize(approvedRoot)

        if candidate == root || candidate == "" || candidate == "/" {
            return false
        }

        return candidate.hasPrefix(root + "/")
    }
}

enum MacCleanupRuleCatalog {
    static func defaultRules(home: URL = FileManager.default.homeDirectoryForCurrentUser, temporaryDirectory: String = NSTemporaryDirectory()) -> [CleanupRule] {
        [
            CleanupRule(
                id: "user-temp",
                name: "User temporary files",
                description: "Temporary files from the current macOS user session that are at least one day old.",
                defaultSelected: true,
                risk: .low,
                minimumAgeDays: 1,
                locations: [
                    CleanupLocation(rootPath: temporaryDirectory, includeSubdirectories: true, includeFiles: true, includeDirectories: false)
                ]
            ),
            CleanupRule(
                id: "user-logs",
                name: "User logs",
                description: "Application and diagnostic logs in your user Library older than two weeks.",
                defaultSelected: true,
                risk: .low,
                minimumAgeDays: 14,
                locations: [
                    CleanupLocation(rootPath: home.appendingPathComponent("Library/Logs").path, includeSubdirectories: true, includeFiles: true, includeDirectories: false),
                    CleanupLocation(rootPath: home.appendingPathComponent("Library/Logs/DiagnosticReports").path, includeSubdirectories: true, includeFiles: true, includeDirectories: false)
                ]
            ),
            CleanupRule(
                id: "user-caches",
                name: "User app caches",
                description: "Recreatable cache files in ~/Library/Caches older than two weeks. Close related apps before cleaning.",
                defaultSelected: false,
                risk: .medium,
                minimumAgeDays: 14,
                locations: [
                    CleanupLocation(rootPath: home.appendingPathComponent("Library/Caches").path, includeSubdirectories: true, includeFiles: true, includeDirectories: false)
                ]
            ),
            CleanupRule(
                id: "xcode-derived-data",
                name: "Xcode derived data",
                description: "Xcode build intermediates older than one week. Xcode recreates these as needed.",
                defaultSelected: false,
                risk: .medium,
                minimumAgeDays: 7,
                locations: [
                    CleanupLocation(rootPath: home.appendingPathComponent("Library/Developer/Xcode/DerivedData").path, includeSubdirectories: true, includeFiles: true, includeDirectories: false)
                ]
            ),
            CleanupRule(
                id: "swiftpm-cache",
                name: "Swift Package cache",
                description: "Swift Package Manager cache entries older than one week.",
                defaultSelected: false,
                risk: .medium,
                minimumAgeDays: 7,
                locations: [
                    CleanupLocation(rootPath: home.appendingPathComponent("Library/Caches/org.swift.swiftpm").path, includeSubdirectories: true, includeFiles: true, includeDirectories: false)
                ]
            )
        ]
    }
}

struct ProtectionPolicy: Sendable {
    let protectedExtensions: [String]
    let protectedPaths: [String]

    func isProtected(_ path: String) -> Bool {
        let normalized = MacPathSafety.normalize(path)
        let ext = URL(fileURLWithPath: path).pathExtension
        if !ext.isEmpty && protectedExtensions.contains(where: { $0.lowercased() == ".\(ext.lowercased())" }) {
            return true
        }

        return protectedPaths.contains { protectedPath in
            let protected = MacPathSafety.normalize(protectedPath)
            return normalized == protected || normalized.hasPrefix(protected + "/")
        }
    }
}

final class MacCleanerService: @unchecked Sendable {
    private let fileManager: FileManager

    init(fileManager: FileManager = .default) {
        self.fileManager = fileManager
    }

    func scan(rules: [CleanupRule], policy: ProtectionPolicy, maxCandidates: Int = 5_000) -> CleanupScanResult {
        var result = CleanupScanResult()

        for rule in rules {
            for location in rule.locations {
                let rootPath = (location.rootPath as NSString).expandingTildeInPath
                var isDirectory: ObjCBool = false
                guard fileManager.fileExists(atPath: rootPath, isDirectory: &isDirectory), isDirectory.boolValue else {
                    continue
                }

                let rootURL = URL(fileURLWithPath: rootPath, isDirectory: true)
                let keys: Set<URLResourceKey> = [.isDirectoryKey, .isSymbolicLinkKey, .fileSizeKey, .totalFileAllocatedSizeKey, .contentModificationDateKey]
                let options: FileManager.DirectoryEnumerationOptions = location.includeSubdirectories
                    ? [.skipsPackageDescendants]
                    : [.skipsPackageDescendants, .skipsSubdirectoryDescendants]

                guard let enumerator = fileManager.enumerator(at: rootURL, includingPropertiesForKeys: Array(keys), options: options) else {
                    continue
                }

                for case let url as URL in enumerator {
                    if result.candidates.count >= maxCandidates {
                        result.warnings.append("Scan stopped after \(maxCandidates) candidates to keep the review usable.")
                        return result
                    }

                    do {
                        let values = try url.resourceValues(forKeys: keys)
                        if values.isSymbolicLink == true {
                            enumerator.skipDescendants()
                            continue
                        }

                        let isDirectoryValue = values.isDirectory == true
                        if isDirectoryValue && !location.includeDirectories {
                            continue
                        }

                        if !isDirectoryValue && !location.includeFiles {
                            continue
                        }

                        if policy.isProtected(url.path) {
                            continue
                        }

                        if !MacPathSafety.isAllowedCleanupTarget(candidatePath: url.path, approvedRoot: rootPath) {
                            result.warnings.append("Skipped unsafe target: \(url.path)")
                            continue
                        }

                        let modified = values.contentModificationDate
                        if let modified, Calendar.current.dateComponents([.day], from: modified, to: Date()).day ?? 0 < rule.minimumAgeDays {
                            continue
                        }

                        let size = Int64(values.totalFileAllocatedSize ?? values.fileSize ?? 0)
                        if size <= 0 {
                            continue
                        }

                        result.candidates.append(CleanupCandidate(
                            categoryId: rule.id,
                            categoryName: rule.name,
                            displayName: url.lastPathComponent,
                            path: url.path,
                            approvedRoot: rootPath,
                            kind: isDirectoryValue ? .directory : .file,
                            sizeBytes: size,
                            lastModified: modified,
                            risk: rule.risk
                        ))
                    } catch {
                        result.warnings.append("Could not inspect \(url.path): \(error.localizedDescription)")
                    }
                }
            }
        }

        result.candidates.sort { $0.sizeBytes > $1.sizeBytes }
        return result
    }
}

final class QuarantineService: @unchecked Sendable {
    private let root: URL
    private let manifestURL: URL
    private let fileManager: FileManager
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()

    init(root: URL = MacAppPaths.quarantineRoot, fileManager: FileManager = .default) {
        self.root = root
        self.manifestURL = root.appendingPathComponent("manifest.json")
        self.fileManager = fileManager
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        encoder.dateEncodingStrategy = .iso8601
        decoder.dateDecodingStrategy = .iso8601
    }

    func entries() -> [QuarantineEntry] {
        loadManifest()
            .filter { fileManager.fileExists(atPath: $0.payloadPath) }
            .sorted { $0.quarantinedAt > $1.quarantinedAt }
    }

    func quarantine(_ candidates: [CleanupCandidate]) -> (entries: [QuarantineEntry], warnings: [String]) {
        try? fileManager.createDirectory(at: root, withIntermediateDirectories: true)
        var manifest = loadManifest()
        var completed: [QuarantineEntry] = []
        var warnings: [String] = []

        for candidate in candidates {
            guard MacPathSafety.isAllowedCleanupTarget(candidatePath: candidate.path, approvedRoot: candidate.approvedRoot) else {
                warnings.append("Skipped unsafe target: \(candidate.path)")
                continue
            }

            let source = URL(fileURLWithPath: candidate.path)
            guard fileManager.fileExists(atPath: source.path) else {
                warnings.append("Skipped missing item: \(candidate.path)")
                continue
            }

            let id = "\(Self.timestamp())-\(UUID().uuidString)"
            let entryRoot = root.appendingPathComponent(id, isDirectory: true)
            let payload = entryRoot.appendingPathComponent("payload")

            do {
                try fileManager.createDirectory(at: entryRoot, withIntermediateDirectories: true)
                try fileManager.moveItem(at: source, to: payload)
                let entry = QuarantineEntry(
                    id: id,
                    originalPath: candidate.path,
                    payloadPath: payload.path,
                    displayName: candidate.displayName,
                    categoryName: candidate.categoryName,
                    kind: candidate.kind,
                    sizeBytes: candidate.sizeBytes,
                    quarantinedAt: Date(),
                    originalLastModified: candidate.lastModified
                )
                manifest.append(entry)
                completed.append(entry)
            } catch {
                warnings.append("Could not quarantine \(candidate.path): \(error.localizedDescription)")
            }
        }

        saveManifest(manifest)
        return (completed, warnings)
    }

    func restore(ids: Set<String>) -> [String] {
        var manifest = loadManifest()
        var warnings: [String] = []

        for entry in manifest.filter({ ids.contains($0.id) }) {
            let original = URL(fileURLWithPath: entry.originalPath)
            let payload = URL(fileURLWithPath: entry.payloadPath)
            guard !fileManager.fileExists(atPath: original.path) else {
                warnings.append("Skipped restore because the original path exists: \(entry.originalPath)")
                continue
            }

            do {
                try fileManager.createDirectory(at: original.deletingLastPathComponent(), withIntermediateDirectories: true)
                try fileManager.moveItem(at: payload, to: original)
                try? fileManager.removeItem(at: payload.deletingLastPathComponent())
                manifest.removeAll { $0.id == entry.id }
            } catch {
                warnings.append("Could not restore \(entry.originalPath): \(error.localizedDescription)")
            }
        }

        saveManifest(manifest)
        return warnings
    }

    func purge(ids: Set<String>) -> [String] {
        var manifest = loadManifest()
        var warnings: [String] = []

        for entry in manifest.filter({ ids.contains($0.id) }) {
            let entryFolder = URL(fileURLWithPath: entry.payloadPath).deletingLastPathComponent()
            do {
                if fileManager.fileExists(atPath: entryFolder.path) {
                    try fileManager.removeItem(at: entryFolder)
                }
                manifest.removeAll { $0.id == entry.id }
            } catch {
                warnings.append("Could not purge \(entry.displayName): \(error.localizedDescription)")
            }
        }

        saveManifest(manifest)
        return warnings
    }

    private func loadManifest() -> [QuarantineEntry] {
        guard let data = try? Data(contentsOf: manifestURL) else { return [] }
        return (try? decoder.decode([QuarantineEntry].self, from: data)) ?? []
    }

    private func saveManifest(_ entries: [QuarantineEntry]) {
        try? fileManager.createDirectory(at: root, withIntermediateDirectories: true)
        if let data = try? encoder.encode(entries) {
            try? data.write(to: manifestURL, options: .atomic)
        }
    }

    private static func timestamp() -> String {
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyyMMddHHmmssSSS"
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        return formatter.string(from: Date())
    }
}

final class ReceiptService: @unchecked Sendable {
    private let root: URL
    private let fileManager: FileManager
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()

    init(root: URL = MacAppPaths.receiptsRoot, fileManager: FileManager = .default) {
        self.root = root
        self.fileManager = fileManager
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        encoder.dateEncodingStrategy = .iso8601
        decoder.dateDecodingStrategy = .iso8601
    }

    func save(_ receipt: CleanupReceipt) {
        try? fileManager.createDirectory(at: root, withIntermediateDirectories: true)
        guard let data = try? encoder.encode(receipt) else { return }
        try? data.write(to: root.appendingPathComponent("\(receipt.id).json"), options: .atomic)
    }

    func recent(limit: Int = 25) -> [CleanupReceipt] {
        guard let urls = try? fileManager.contentsOfDirectory(at: root, includingPropertiesForKeys: nil) else {
            return []
        }

        return urls
            .filter { $0.pathExtension == "json" }
            .compactMap { url in
                guard let data = try? Data(contentsOf: url) else { return nil }
                return try? decoder.decode(CleanupReceipt.self, from: data)
            }
            .sorted { $0.timestamp > $1.timestamp }
            .prefix(limit)
            .map { $0 }
    }
}

final class PreferencesService: @unchecked Sendable {
    private let url: URL
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()

    init(url: URL = MacAppPaths.preferencesURL) {
        self.url = url
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
    }

    func load() -> MacPreferences {
        guard let data = try? Data(contentsOf: url),
              let preferences = try? decoder.decode(MacPreferences.self, from: data)
        else {
            return MacPreferences()
        }
        return preferences
    }

    func save(_ preferences: MacPreferences) {
        try? FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        guard let data = try? encoder.encode(preferences) else { return }
        try? data.write(to: url, options: .atomic)
    }
}

final class StorageAnalysisService: @unchecked Sendable {
    private let fileManager: FileManager

    init(fileManager: FileManager = .default) {
        self.fileManager = fileManager
    }

    func scanLargeFiles(minimumMB: Int, maxResults: Int = 300, policy: ProtectionPolicy) -> StorageScanResult<LargeFileInfo> {
        var result = StorageScanResult<LargeFileInfo>()
        let minimumBytes = Int64(max(1, minimumMB)) * 1_048_576

        for root in userStorageRoots() {
            guard let enumerator = fileManager.enumerator(
                at: root,
                includingPropertiesForKeys: [.isDirectoryKey, .isSymbolicLinkKey, .fileSizeKey, .totalFileAllocatedSizeKey, .contentModificationDateKey],
                options: [.skipsPackageDescendants]
            ) else {
                continue
            }

            for case let url as URL in enumerator {
                do {
                    let values = try url.resourceValues(forKeys: [.isDirectoryKey, .isSymbolicLinkKey, .fileSizeKey, .totalFileAllocatedSizeKey, .contentModificationDateKey])
                    if values.isSymbolicLink == true {
                        enumerator.skipDescendants()
                        continue
                    }
                    if values.isDirectory == true || policy.isProtected(url.path) {
                        continue
                    }

                    let size = Int64(values.totalFileAllocatedSize ?? values.fileSize ?? 0)
                    if size >= minimumBytes {
                        result.items.append(LargeFileInfo(
                            path: url.path,
                            name: url.lastPathComponent,
                            directory: url.deletingLastPathComponent().path,
                            sizeBytes: size,
                            modifiedAt: values.contentModificationDate,
                            recommendation: recommendation(for: url)
                        ))
                    }
                } catch {
                    result.warnings.append("Could not inspect \(url.path): \(error.localizedDescription)")
                }
            }
        }

        result.items.sort { $0.sizeBytes > $1.sizeBytes }
        if result.items.count > maxResults {
            result.items = Array(result.items.prefix(maxResults))
            result.warnings.append("Large-file scan was limited to the largest \(maxResults) files.")
        }

        return result
    }

    func scanDuplicates(minimumMB: Int, maxGroups: Int = 100, policy: ProtectionPolicy) -> StorageScanResult<DuplicateFileInfo> {
        var result = StorageScanResult<DuplicateFileInfo>()
        let minimumBytes = Int64(max(1, minimumMB)) * 1_048_576
        var filesBySize: [Int64: [URL]] = [:]

        for root in userStorageRoots() {
            guard let enumerator = fileManager.enumerator(
                at: root,
                includingPropertiesForKeys: [.isDirectoryKey, .isSymbolicLinkKey, .fileSizeKey, .totalFileAllocatedSizeKey, .contentModificationDateKey],
                options: [.skipsPackageDescendants]
            ) else {
                continue
            }

            for case let url as URL in enumerator {
                do {
                    let values = try url.resourceValues(forKeys: [.isDirectoryKey, .isSymbolicLinkKey, .fileSizeKey, .totalFileAllocatedSizeKey])
                    if values.isSymbolicLink == true {
                        enumerator.skipDescendants()
                        continue
                    }
                    if values.isDirectory == true || policy.isProtected(url.path) {
                        continue
                    }

                    let size = Int64(values.totalFileAllocatedSize ?? values.fileSize ?? 0)
                    if size >= minimumBytes {
                        filesBySize[size, default: []].append(url)
                    }
                } catch {
                    result.warnings.append("Could not inspect \(url.path): \(error.localizedDescription)")
                }
            }
        }

        var groups = 0
        for (size, urls) in filesBySize where urls.count > 1 {
            var byHash: [String: [URL]] = [:]
            for url in urls {
                if let hash = sha256(url: url) {
                    byHash[hash, default: []].append(url)
                }
            }

            for (hash, matches) in byHash where matches.count > 1 {
                groups += 1
                let sorted = matches.sorted { $0.path < $1.path }
                for (index, url) in sorted.enumerated() {
                    let modified = (try? url.resourceValues(forKeys: [.contentModificationDateKey]))?.contentModificationDate
                    result.items.append(DuplicateFileInfo(
                        groupId: "Duplicate set \(groups)",
                        path: url.path,
                        name: url.lastPathComponent,
                        directory: url.deletingLastPathComponent().path,
                        sizeBytes: size,
                        modifiedAt: modified,
                        sha256: hash,
                        isRecommendedForCleanup: index > 0
                    ))
                }
                if groups >= maxGroups {
                    result.warnings.append("Duplicate scan was limited to \(maxGroups) groups.")
                    return result
                }
            }
        }

        return result
    }

    private func userStorageRoots() -> [URL] {
        let home = FileManager.default.homeDirectoryForCurrentUser
        return ["Desktop", "Documents", "Downloads", "Movies", "Music", "Pictures"].map { home.appendingPathComponent($0, isDirectory: true) }
            .filter { fileManager.fileExists(atPath: $0.path) }
    }

    private func recommendation(for url: URL) -> String {
        switch url.pathExtension.lowercased() {
        case "dmg", "pkg", "zip":
            return "Installer/archive; verify it is no longer needed before quarantining."
        case "mov", "mp4", "m4v":
            return "Large media; move to external storage if you want local space back."
        default:
            return "Review before quarantining. SpacePilot never auto-selects personal files."
        }
    }

    private func sha256(url: URL) -> String? {
        guard let handle = try? FileHandle(forReadingFrom: url) else { return nil }
        defer { try? handle.close() }

        var hasher = SHA256()
        while autoreleasepool(invoking: {
            let data = handle.readData(ofLength: 1_048_576)
            if data.isEmpty {
                return false
            }
            hasher.update(data: data)
            return true
        }) {}

        return hasher.finalize().map { String(format: "%02x", $0) }.joined()
    }
}
