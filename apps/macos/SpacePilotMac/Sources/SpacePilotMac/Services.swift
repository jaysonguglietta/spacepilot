import CryptoKit
import Darwin
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

enum MacFileSafety {
    static func canonicalDirectoryPath(_ path: String) -> String? {
        guard let pointer = path.withCString({ Darwin.realpath($0, nil) }) else { return nil }
        defer { free(pointer) }
        return String(cString: pointer)
    }

    static func identity(at path: String) -> FileIdentity? {
        var metadata = stat()
        guard lstat(path, &metadata) == 0 else { return nil }
        return identity(from: metadata)
    }

    static func identity(from metadata: stat) -> FileIdentity? {
        let mode = metadata.st_mode & mode_t(S_IFMT)
        let kind: CleanupTargetKind
        switch mode {
        case mode_t(S_IFREG): kind = .file
        case mode_t(S_IFDIR): kind = .directory
        default: return nil
        }
        return FileIdentity(
            device: UInt64(metadata.st_dev),
            inode: UInt64(metadata.st_ino),
            size: metadata.st_size,
            modifiedSeconds: Int64(metadata.st_mtimespec.tv_sec),
            modifiedNanoseconds: Int64(metadata.st_mtimespec.tv_nsec),
            kind: kind
        )
    }

    // Each directory is opened relative to a verified directory descriptor. A replaced
    // ancestor or symbolic link cannot redirect a later rename to another tree.
    static func openDirectoryWithoutFollowingLinks(_ url: URL) -> Int32? {
        let path = url.standardized.path
        guard path.hasPrefix("/") else { return nil }
        var descriptor = Darwin.open("/", O_RDONLY | O_DIRECTORY | O_CLOEXEC)
        guard descriptor >= 0 else { return nil }
        for component in path.split(separator: "/") {
            let next = String(component).withCString {
                Darwin.openat(descriptor, $0, O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC)
            }
            Darwin.close(descriptor)
            guard next >= 0 else { return nil }
            descriptor = next
        }
        return descriptor
    }

    static func isSameObject(_ current: FileIdentity, as expected: FileIdentity, includeContents: Bool) -> Bool {
        guard current.device == expected.device,
              current.inode == expected.inode,
              current.kind == expected.kind
        else { return false }
        return !includeContents || (
            current.size == expected.size
            && current.modifiedSeconds == expected.modifiedSeconds
            && current.modifiedNanoseconds == expected.modifiedNanoseconds
        )
    }
}

final class ScanCancellation: @unchecked Sendable {
    private let lock = NSLock()
    private var stopped = false
    private var count = 0

    var isCancelled: Bool {
        lock.lock()
        defer { lock.unlock() }
        return stopped
    }

    var inspectedCount: Int {
        lock.lock()
        defer { lock.unlock() }
        return count
    }

    func inspect() {
        lock.lock()
        count += 1
        lock.unlock()
    }

    func cancel() {
        lock.lock()
        stopped = true
        lock.unlock()
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
    private let limits: StorageScanLimits

    init(fileManager: FileManager = .default, limits: StorageScanLimits = .standard) {
        self.fileManager = fileManager
        self.limits = limits
    }

    func scan(rules: [CleanupRule], policy: ProtectionPolicy, maxCandidates: Int = 5_000, cancellation: ScanCancellation? = nil) -> CleanupScanResult {
        var result = CleanupScanResult()
        var seenPaths = Set<String>()
        var seenObjects = Set<String>()
        var budget = StorageWorkBudget(limits: limits)

        for rule in rules {
            if cancellation?.isCancelled == true { return result }
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

                guard let enumerator = fileManager.enumerator(at: rootURL, includingPropertiesForKeys: Array(keys), options: options, errorHandler: { url, error in
                    result.addWarning("Could not enumerate \(url.path): \(error.localizedDescription)")
                    return true
                }) else {
                    result.addWarning("Could not enumerate cleanup root: \(rootPath)")
                    continue
                }

                for case let url as URL in enumerator {
                    if cancellation?.isCancelled == true { return result }
                    guard budget.inspectEntry() else {
                        result.addWarning("Cleanup scan stopped at \(budget.stopReason ?? "a resource limit") after inspecting \(budget.inspectedEntries) entries. Results are partial.", priority: true)
                        return result
                    }
                    cancellation?.inspect()
                    if result.candidates.count >= maxCandidates {
                        result.addWarning("Scan stopped after \(maxCandidates) candidates to keep the review usable. Results are partial.", priority: true)
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
                            result.addWarning("Skipped unsafe target: \(url.path)")
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

                        let path = url.standardized.path
                        guard let identity = MacFileSafety.identity(at: path) else {
                            result.addWarning("Could not verify file identity: \(path)")
                            continue
                        }
                        guard seenPaths.insert(path).inserted else { continue }
                        guard seenObjects.insert("\(identity.device):\(identity.inode)").inserted else { continue }
                        result.candidates.append(CleanupCandidate(
                            categoryId: rule.id,
                            categoryName: rule.name,
                            displayName: url.lastPathComponent,
                            path: url.path,
                            approvedRoot: rootPath,
                            kind: isDirectoryValue ? .directory : .file,
                            sizeBytes: size,
                            lastModified: modified,
                            risk: rule.risk,
                            identity: identity
                        ))
                    } catch {
                        result.addWarning("Could not inspect \(url.path): \(error.localizedDescription)")
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

    func diagnostics() -> [String] {
        var warnings: [String] = []
        if fileManager.fileExists(atPath: manifestURL.path) {
            do {
                let data = try Data(contentsOf: manifestURL)
                let records = try decoder.decode([QuarantineEntry].self, from: data)
                if records.contains(where: { !isValidEntry($0) }) {
                    warnings.append("Some quarantine manifest records are invalid and were ignored.")
                }
            } catch {
                warnings.append("The quarantine manifest could not be read. SpacePilot recovered any items with intact entry records: \(error.localizedDescription)")
            }
        }
        return warnings
    }

    func quarantine(_ candidates: [CleanupCandidate], policy: ProtectionPolicy) -> (entries: [QuarantineEntry], warnings: [String]) {
        do {
            try fileManager.createDirectory(at: root, withIntermediateDirectories: true)
        } catch {
            return ([], ["Quarantine is unavailable: \(error.localizedDescription)"])
        }
        guard let rootFD = try? openQuarantineRoot() else {
            return ([], ["Quarantine location is a symbolic link or is not a directory. No items were moved."])
        }
        defer { Darwin.close(rootFD) }
        var manifest = loadManifest()
        var completed: [QuarantineEntry] = []
        var warnings: [String] = []

        for candidate in candidates {
            guard MacPathSafety.isAllowedCleanupTarget(candidatePath: candidate.path, approvedRoot: candidate.approvedRoot) else {
                warnings.append("Skipped unsafe target: \(candidate.path)")
                continue
            }
            if let protectionWarning = quarantineProtectionWarning(for: candidate, policy: policy) {
                warnings.append(protectionWarning)
                continue
            }
            guard candidate.identity != nil else {
                warnings.append("Skipped \(candidate.displayName) because its scan identity is unavailable. Rescan before quarantining.")
                continue
            }

            let id = "\(Self.timestamp())-\(UUID().uuidString)"
            let entryRoot = root.appendingPathComponent(id, isDirectory: true)
            let payload = entryRoot.appendingPathComponent("payload")
            let entry = QuarantineEntry(
                id: id,
                originalPath: candidate.path,
                payloadPath: payload.path,
                displayName: candidate.displayName,
                categoryName: candidate.categoryName,
                kind: candidate.kind,
                sizeBytes: candidate.sizeBytes,
                quarantinedAt: Date(),
                originalLastModified: candidate.lastModified,
                approvedRoot: candidate.approvedRoot,
                canonicalApprovedRoot: MacFileSafety.canonicalDirectoryPath(candidate.approvedRoot)
            )

            do {
                guard id.withCString({ Darwin.mkdirat(rootFD, $0, 0o700) }) == 0 else {
                    throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno))
                }
                let entryFD = id.withCString {
                    Darwin.openat(rootFD, $0, O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC)
                }
                guard entryFD >= 0 else { throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno)) }
                defer { Darwin.close(entryFD) }
                // This durable sidecar is written before the move. It lets recovery
                // reconstruct an entry even if the central manifest write fails.
                try saveSidecar(entry, directoryFD: entryFD)
                try moveToQuarantine(candidate, entryFD: entryFD)
                manifest.append(entry)
                completed.append(entry)
                do {
                    try saveManifest(manifest, directoryFD: rootFD)
                } catch {
                    warnings.append("\(candidate.displayName) was quarantined and is recoverable, but the summary manifest could not be saved: \(error.localizedDescription)")
                }
            } catch {
                // A rejected move must not leave a sidecar for a payload that was
                // never created. Keep the sidecar if a payload exists so recovery
                // can still find an interrupted transaction.
                cleanupUnmovedEntry(id: id, rootFD: rootFD)
                warnings.append("Could not quarantine \(candidate.path): \(error.localizedDescription)")
            }
        }

        return (completed, warnings)
    }

    private func quarantineProtectionWarning(for candidate: CleanupCandidate, policy: ProtectionPolicy) -> String? {
        if candidate.kind == .directory {
            return "Skipped directory \(candidate.path). Review and quarantine individual files; directory contents can change after a scan."
        }
        if policy.isProtected(candidate.path) {
            return "Skipped protected target: \(candidate.path). Review current protection settings and rescan."
        }
        return nil
    }

    func restore(ids: Set<String>) -> [String] {
        restore(ids: ids, destinationDirectory: nil)
    }

    func restore(ids: Set<String>, destinationDirectory: URL?) -> [String] {
        var manifest = loadManifest()
        var warnings: [String] = []

        for entry in manifest.filter({ ids.contains($0.id) }) {
            guard isValidEntry(entry) else {
                warnings.append("Skipped an invalid quarantine record: \(entry.id)")
                continue
            }
            let original = destinationDirectory.map { $0.appendingPathComponent(entry.displayName) }
                ?? URL(fileURLWithPath: entry.originalPath)
            if destinationDirectory == nil {
                guard let approvedRoot = entry.approvedRoot,
                      let canonicalApprovedRoot = entry.canonicalApprovedRoot,
                      MacPathSafety.isAllowedCleanupTarget(candidatePath: original.path, approvedRoot: approvedRoot),
                      let canonicalParent = MacFileSafety.canonicalDirectoryPath(original.deletingLastPathComponent().path),
                      canonicalParent == canonicalApprovedRoot || canonicalParent.hasPrefix(canonicalApprovedRoot + "/")
                else {
                    warnings.append("\(entry.displayName) cannot be restored safely to its original location. Choose Restore to folder instead.")
                    continue
                }
            }
            guard !fileManager.fileExists(atPath: original.path) else {
                warnings.append("Skipped restore because the destination exists: \(original.path). Choose another folder in Recovery.")
                continue
            }

            do {
                try restorePayload(entry, to: original)
                manifest.removeAll { $0.id == entry.id }
                do { try removeEmptyEntryFolder(entry) }
                catch { warnings.append("\(entry.displayName) was restored, but its empty quarantine folder could not be removed: \(error.localizedDescription)") }
            } catch {
                warnings.append("Could not restore \(entry.displayName): \(error.localizedDescription)")
            }
        }

        do { try saveManifest(manifest) }
        catch { warnings.append("Could not update recovery manifest: \(error.localizedDescription)") }
        return warnings
    }

    func purge(ids: Set<String>) -> [String] {
        var manifest = loadManifest()
        var warnings: [String] = []

        for entry in manifest.filter({ ids.contains($0.id) }) {
            guard isValidEntry(entry) else {
                warnings.append("Skipped an invalid quarantine record: \(entry.id)")
                continue
            }
            guard entry.kind == .file else {
                warnings.append("Directory payloads require manual review and cannot be purged automatically: \(entry.displayName)")
                continue
            }
            do {
                let (rootFD, entryFD) = try openEntryDirectory(entry)
                defer {
                    Darwin.close(rootFD)
                    Darwin.close(entryFD)
                }
                var metadata = stat()
                guard Darwin.fstatat(entryFD, "payload", &metadata, AT_SYMLINK_NOFOLLOW) == 0,
                      MacFileSafety.identity(from: metadata)?.kind == .file
                else { throw CocoaError(.fileReadNoPermission) }
                guard Darwin.unlinkat(entryFD, "payload", 0) == 0 else {
                    throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno))
                }
                _ = Darwin.unlinkat(entryFD, "entry.json", 0)
                if Darwin.unlinkat(rootFD, entry.id, AT_REMOVEDIR) != 0 {
                    warnings.append("\(entry.displayName) was purged, but its empty quarantine folder could not be removed.")
                }
                manifest.removeAll { $0.id == entry.id }
            } catch {
                warnings.append("Could not purge \(entry.displayName): \(error.localizedDescription)")
            }
        }

        do { try saveManifest(manifest) }
        catch { warnings.append("Could not update recovery manifest: \(error.localizedDescription)") }
        return warnings
    }

    private func loadManifest() -> [QuarantineEntry] {
        let saved = (try? Data(contentsOf: manifestURL)).flatMap { try? decoder.decode([QuarantineEntry].self, from: $0) } ?? []
        var byID: [String: QuarantineEntry] = [:]
        for entry in saved where isValidEntry(entry) {
            byID[entry.id] = entry
        }
        // Sidecars are the source of truth for interrupted or failed manifest writes.
        for directory in (try? fileManager.contentsOfDirectory(at: root, includingPropertiesForKeys: nil)) ?? [] {
            let sidecar = directory.appendingPathComponent("entry.json")
            guard MacFileSafety.identity(at: directory.path)?.kind == .directory,
                  MacFileSafety.identity(at: sidecar.path)?.kind == .file,
                  let data = try? Data(contentsOf: sidecar),
                  let entry = try? decoder.decode(QuarantineEntry.self, from: data),
                  entry.id == directory.lastPathComponent,
                  isValidEntry(entry)
            else { continue }
            byID[entry.id] = entry
        }
        return Array(byID.values)
    }

    private func saveManifest(_ entries: [QuarantineEntry], directoryFD: Int32? = nil) throws {
        let data = try encoder.encode(entries)
        if let directoryFD {
            try writeAtomically(data, directoryFD: directoryFD, name: "manifest.json", replace: true)
        } else {
            try fileManager.createDirectory(at: root, withIntermediateDirectories: true)
            try writeAtomically(data, in: root, name: "manifest.json", replace: true)
        }
    }

    private func saveSidecar(_ entry: QuarantineEntry, directoryFD: Int32) throws {
        try writeAtomically(encoder.encode(entry), directoryFD: directoryFD, name: "entry.json", replace: false)
    }

    private func writeAtomically(_ data: Data, in directory: URL, name: String, replace: Bool) throws {
        guard let expected = MacFileSafety.identity(at: directory.path), expected.kind == .directory,
              let canonical = MacFileSafety.canonicalDirectoryPath(directory.path),
              let directoryFD = MacFileSafety.openDirectoryWithoutFollowingLinks(URL(fileURLWithPath: canonical))
        else { throw CocoaError(.fileWriteNoPermission) }
        defer { Darwin.close(directoryFD) }
        var directoryMetadata = stat()
        guard fstat(directoryFD, &directoryMetadata) == 0,
              let current = MacFileSafety.identity(from: directoryMetadata),
              MacFileSafety.isSameObject(current, as: expected, includeContents: false)
        else { throw CocoaError(.fileWriteNoPermission) }

        try writeAtomically(data, directoryFD: directoryFD, name: name, replace: replace)
    }

    private func writeAtomically(_ data: Data, directoryFD: Int32, name: String, replace: Bool) throws {

        let temporary = ".\(name).\(UUID().uuidString).tmp"
        let fileFD = temporary.withCString {
            Darwin.openat(directoryFD, $0, O_WRONLY | O_CREAT | O_EXCL | O_NOFOLLOW | O_CLOEXEC, 0o600)
        }
        guard fileFD >= 0 else { throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno)) }
        defer {
            Darwin.close(fileFD)
            _ = temporary.withCString { Darwin.unlinkat(directoryFD, $0, 0) }
        }
        try data.withUnsafeBytes { bytes in
            guard let base = bytes.baseAddress else { return }
            var offset = 0
            while offset < bytes.count {
                let written = Darwin.write(fileFD, base.advanced(by: offset), bytes.count - offset)
                guard written > 0 else { throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno)) }
                offset += written
            }
        }
        guard fsync(fileFD) == 0 else { throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno)) }
        let renamed = temporary.withCString { sourcePointer in
            name.withCString { destinationPointer in
                replace
                    ? Darwin.renameat(directoryFD, sourcePointer, directoryFD, destinationPointer)
                    : Darwin.renameatx_np(directoryFD, sourcePointer, directoryFD, destinationPointer, UInt32(RENAME_EXCL))
            }
        }
        guard renamed == 0 else { throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno)) }
        guard fsync(directoryFD) == 0 else { throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno)) }
    }

    private func entryFolder(for entry: QuarantineEntry) -> URL {
        root.appendingPathComponent(entry.id, isDirectory: true)
    }

    private func payloadURL(for entry: QuarantineEntry) -> URL {
        entryFolder(for: entry).appendingPathComponent("payload")
    }

    private func isValidEntry(_ entry: QuarantineEntry) -> Bool {
        guard !entry.id.isEmpty,
              entry.id.allSatisfy({ $0.isASCII && ($0.isLetter || $0.isNumber || $0 == "-") }),
              URL(fileURLWithPath: entry.payloadPath).standardized.path == payloadURL(for: entry).standardized.path,
              entry.originalPath.hasPrefix("/"),
              URL(fileURLWithPath: entry.originalPath).lastPathComponent == entry.displayName,
              entry.approvedRoot == nil || MacPathSafety.isAllowedCleanupTarget(candidatePath: entry.originalPath, approvedRoot: entry.approvedRoot!),
              entry.canonicalApprovedRoot == nil || entry.canonicalApprovedRoot!.hasPrefix("/"),
              let rootIdentity = MacFileSafety.identity(at: root.path), rootIdentity.kind == .directory
        else { return false }
        // Never trust a manifest's payloadPath as the deletion target.
        if let folderIdentity = MacFileSafety.identity(at: entryFolder(for: entry).path) {
            return folderIdentity.kind == .directory
        }
        return true
    }

    private func openEntryDirectory(_ entry: QuarantineEntry) throws -> (Int32, Int32) {
        guard isValidEntry(entry) else { throw CocoaError(.fileReadNoPermission) }
        let rootFD = try openQuarantineRoot()
        let entryFD = entry.id.withCString {
            Darwin.openat(rootFD, $0, O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC)
        }
        guard entryFD >= 0 else {
            Darwin.close(rootFD)
            throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno))
        }
        return (rootFD, entryFD)
    }

    private func openQuarantineRoot() throws -> Int32 {
        guard let expectedRoot = MacFileSafety.identity(at: root.path), expectedRoot.kind == .directory,
              let canonicalRoot = MacFileSafety.canonicalDirectoryPath(root.path),
              let rootFD = MacFileSafety.openDirectoryWithoutFollowingLinks(URL(fileURLWithPath: canonicalRoot))
        else { throw CocoaError(.fileReadNoPermission) }
        var rootMetadata = stat()
        guard fstat(rootFD, &rootMetadata) == 0,
              let currentRoot = MacFileSafety.identity(from: rootMetadata),
              MacFileSafety.isSameObject(currentRoot, as: expectedRoot, includeContents: false)
        else {
            Darwin.close(rootFD)
            throw CocoaError(.fileReadNoPermission)
        }
        return rootFD
    }

    private func removeEmptyEntryFolder(_ entry: QuarantineEntry) throws {
        let (rootFD, entryFD) = try openEntryDirectory(entry)
        _ = Darwin.unlinkat(entryFD, "entry.json", 0)
        Darwin.close(entryFD)
        defer { Darwin.close(rootFD) }
        guard Darwin.unlinkat(rootFD, entry.id, AT_REMOVEDIR) == 0 else {
            throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno))
        }
    }

    private func cleanupUnmovedEntry(id: String, rootFD: Int32) {
        let entryFD = id.withCString {
            Darwin.openat(rootFD, $0, O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC)
        }
        guard entryFD >= 0 else { return }
        defer { Darwin.close(entryFD) }
        var payloadMetadata = stat()
        guard Darwin.fstatat(entryFD, "payload", &payloadMetadata, AT_SYMLINK_NOFOLLOW) != 0,
              errno == ENOENT
        else { return }
        _ = Darwin.unlinkat(entryFD, "entry.json", 0)
        _ = id.withCString { Darwin.unlinkat(rootFD, $0, AT_REMOVEDIR) }
    }

    private func restorePayload(_ entry: QuarantineEntry, to destination: URL) throws {
        let (rootFD, entryFD) = try openEntryDirectory(entry)
        defer {
            Darwin.close(rootFD)
            Darwin.close(entryFD)
        }
        guard let destinationParent = MacFileSafety.canonicalDirectoryPath(destination.deletingLastPathComponent().path),
              let destinationFD = MacFileSafety.openDirectoryWithoutFollowingLinks(URL(fileURLWithPath: destinationParent))
        else { throw CocoaError(.fileWriteNoPermission) }
        defer { Darwin.close(destinationFD) }
        var payloadMetadata = stat()
        guard Darwin.fstatat(entryFD, "payload", &payloadMetadata, AT_SYMLINK_NOFOLLOW) == 0,
              MacFileSafety.identity(from: payloadMetadata)?.kind == entry.kind
        else { throw CocoaError(.fileReadNoPermission) }
        let destinationName = destination.lastPathComponent
        let moved = destinationName.withCString { name in
            Darwin.renameatx_np(entryFD, "payload", destinationFD, name, UInt32(RENAME_EXCL))
        }
        guard moved == 0 else { throw NSError(domain: NSPOSIXErrorDomain, code: Int(errno)) }
    }

    private func moveToQuarantine(_ candidate: CleanupCandidate, entryFD: Int32) throws {
        let source = URL(fileURLWithPath: candidate.path)
        guard let identity = candidate.identity,
              let parent = MacFileSafety.canonicalDirectoryPath(source.deletingLastPathComponent().path),
              let rootPath = MacFileSafety.canonicalDirectoryPath(candidate.approvedRoot)
        else { throw CocoaError(.fileReadNoPermission) }
        guard parent == rootPath || parent.hasPrefix(rootPath + "/") else {
            throw CocoaError(.fileReadNoPermission)
        }
        try moveExistingItemWithoutFollowingLinks(
            from: URL(fileURLWithPath: parent).appendingPathComponent(source.lastPathComponent),
            destinationParentFD: entryFD,
            destinationName: "payload",
            expected: identity,
            kind: candidate.kind,
            checkContents: candidate.risk == .high
        )
    }

    private func moveExistingItemWithoutFollowingLinks(
        from source: URL,
        destinationParentFD: Int32,
        destinationName: String,
        expected: FileIdentity,
        kind: CleanupTargetKind,
        checkContents: Bool = false
    ) throws {
        guard let canonicalSource = MacFileSafety.canonicalDirectoryPath(source.deletingLastPathComponent().path),
              let sourceParentFD = MacFileSafety.openDirectoryWithoutFollowingLinks(URL(fileURLWithPath: canonicalSource))
        else { throw CocoaError(.fileReadNoPermission) }
        defer { Darwin.close(sourceParentFD) }
        let sourceName = source.lastPathComponent
        var metadata = stat()
        let inspection = sourceName.withCString { Darwin.fstatat(sourceParentFD, $0, &metadata, AT_SYMLINK_NOFOLLOW) }
        guard inspection == 0, let current = MacFileSafety.identity(from: metadata),
              current.kind == kind,
              MacFileSafety.isSameObject(current, as: expected, includeContents: checkContents)
        else { throw CocoaError(.fileReadNoPermission) }
        var destinationMetadata = stat()
        let destinationExists = destinationName.withCString {
            Darwin.fstatat(destinationParentFD, $0, &destinationMetadata, AT_SYMLINK_NOFOLLOW) == 0
        }
        guard !destinationExists else { throw CocoaError(.fileWriteFileExists) }
        let moved = sourceName.withCString { sourcePointer in
            destinationName.withCString { destinationPointer in
                Darwin.renameatx_np(sourceParentFD, sourcePointer, destinationParentFD, destinationPointer, UInt32(RENAME_EXCL))
            }
        }
        guard moved == 0 else {
            let code = errno
            if code == EXDEV {
                throw NSError(domain: NSPOSIXErrorDomain, code: Int(code), userInfo: [NSLocalizedDescriptionKey: "The item is on another volume; it was left in place because a cross-volume copy cannot be moved atomically."])
            }
            throw NSError(domain: NSPOSIXErrorDomain, code: Int(code))
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
    private static let maxDirectoryEntries = 2_000
    private static let maxReceiptsToDecode = 100
    private static let maxReceiptBytes = 128 * 1_024
    private static let maxTotalReadBytes = 4 * 1_048_576

    private struct ReceiptFile {
        let url: URL
        let modifiedSeconds: Int64
        let modifiedNanoseconds: Int64
        let sizeBytes: Int64
    }

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

    @discardableResult
    func save(_ receipt: CleanupReceipt) -> String? {
        do {
            try fileManager.createDirectory(at: root, withIntermediateDirectories: true)
            try encoder.encode(receipt).write(to: root.appendingPathComponent("\(receipt.id).json"), options: .atomic)
            return nil
        } catch {
            return "Could not save cleanup receipt: \(error.localizedDescription)"
        }
    }

    func recent(limit: Int = 25) -> [CleanupReceipt] {
        loadSnapshot(limit: limit).receipts
    }

    func diagnostics() -> [String] {
        loadSnapshot().diagnostics
    }

    func loadSnapshot(limit: Int = 25) -> (receipts: [CleanupReceipt], diagnostics: [String]) {
        guard fileManager.fileExists(atPath: root.path) else { return ([], []) }
        var warningBuffer = ScanWarningBuffer()
        guard let enumerator = fileManager.enumerator(at: root, includingPropertiesForKeys: nil, options: [.skipsSubdirectoryDescendants], errorHandler: { url, error in
            warningBuffer.append({ "Could not enumerate receipts at \(url.path): \(error.localizedDescription)" })
            return false
        }) else {
            return ([], ["Could not enumerate cleanup receipts."])
        }

        var files: [ReceiptFile] = []
        var inspected = 0
        for case let url as URL in enumerator {
            guard inspected < Self.maxDirectoryEntries else {
                warningBuffer.append({ "Receipt folder scan stopped after \(Self.maxDirectoryEntries) entries; some receipts may be omitted." }, priority: true)
                break
            }
            inspected += 1
            guard url.pathExtension.lowercased() == "json" else { continue }
            var metadata = stat()
            guard url.path.withCString({ Darwin.lstat($0, &metadata) }) == 0,
                  metadata.st_mode & mode_t(S_IFMT) == mode_t(S_IFREG)
            else {
                warningBuffer.append({ "Could not read cleanup receipt \(url.lastPathComponent): it is missing or is not a regular file." })
                continue
            }
            files.append(ReceiptFile(url: url, modifiedSeconds: Int64(metadata.st_mtimespec.tv_sec), modifiedNanoseconds: Int64(metadata.st_mtimespec.tv_nsec), sizeBytes: metadata.st_size))
        }

        files.sort {
            if $0.modifiedSeconds != $1.modifiedSeconds { return $0.modifiedSeconds > $1.modifiedSeconds }
            if $0.modifiedNanoseconds != $1.modifiedNanoseconds { return $0.modifiedNanoseconds > $1.modifiedNanoseconds }
            return $0.url.lastPathComponent < $1.url.lastPathComponent
        }
        if files.count > Self.maxReceiptsToDecode {
            warningBuffer.append({ "Only the newest \(Self.maxReceiptsToDecode) receipt files were inspected; \(files.count - Self.maxReceiptsToDecode) older files were omitted." }, priority: true)
        }

        var receipts: [CleanupReceipt] = []
        var totalReadBytes = 0
        for file in files.prefix(Self.maxReceiptsToDecode) {
            guard file.sizeBytes >= 0, file.sizeBytes <= Self.maxReceiptBytes else {
                warningBuffer.append({ "Skipped cleanup receipt \(file.url.lastPathComponent): file exceeds the \(Self.maxReceiptBytes)-byte size limit." })
                continue
            }
            guard totalReadBytes + Int(file.sizeBytes) <= Self.maxTotalReadBytes else {
                warningBuffer.append({ "Receipt loading stopped at the \(Self.maxTotalReadBytes)-byte total read limit; some receipts were omitted." }, priority: true)
                break
            }
            guard let data = readReceiptData(file.url, maxBytes: min(Self.maxReceiptBytes, Self.maxTotalReadBytes - totalReadBytes)) else {
                warningBuffer.append({ "Could not read cleanup receipt \(file.url.lastPathComponent)." })
                continue
            }
            totalReadBytes += data.count
            guard let receipt = try? decoder.decode(CleanupReceipt.self, from: data) else {
                warningBuffer.append({ "Could not read cleanup receipt \(file.url.lastPathComponent)." })
                continue
            }
            receipts.append(receipt)
        }
        receipts.sort { $0.timestamp > $1.timestamp }
        return (Array(receipts.prefix(max(0, min(limit, Self.maxReceiptsToDecode)))), warningBuffer.messages)
    }

    private func readReceiptData(_ url: URL, maxBytes: Int) -> Data? {
        let descriptor = url.path.withCString { Darwin.open($0, O_RDONLY | O_NOFOLLOW | O_CLOEXEC) }
        guard descriptor >= 0 else { return nil }
        defer { Darwin.close(descriptor) }
        var metadata = stat()
        guard Darwin.fstat(descriptor, &metadata) == 0,
              metadata.st_mode & mode_t(S_IFMT) == mode_t(S_IFREG),
              metadata.st_size >= 0,
              metadata.st_size <= maxBytes
        else { return nil }
        let data = FileHandle(fileDescriptor: descriptor, closeOnDealloc: false).readData(ofLength: maxBytes + 1)
        return data.count <= maxBytes ? data : nil
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

    @discardableResult
    func save(_ preferences: MacPreferences) -> String? {
        do {
            try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try encoder.encode(preferences).write(to: url, options: .atomic)
            return nil
        } catch {
            return "Could not save settings: \(error.localizedDescription)"
        }
    }
}

struct StorageScanLimits: Sendable {
    let maxInspectedEntries: Int
    let maxHashBytes: Int64
    let maxElapsedSeconds: TimeInterval

    static let standard = StorageScanLimits(
        maxInspectedEntries: 100_000,
        maxHashBytes: 8 * 1_073_741_824,
        maxElapsedSeconds: 90
    )
}

private struct StorageWorkBudget {
    let limits: StorageScanLimits
    let startedAt = ProcessInfo.processInfo.systemUptime
    var inspectedEntries = 0
    var hashedBytes: Int64 = 0
    var stopReason: String?

    mutating func inspectEntry() -> Bool {
        guard checkTime() else { return false }
        guard inspectedEntries < limits.maxInspectedEntries else {
            stopReason = "the \(limits.maxInspectedEntries)-entry inspection limit"
            return false
        }
        inspectedEntries += 1
        return true
    }

    mutating func allowHash(of size: Int64) -> Bool {
        guard checkTime() else { return false }
        let maximumBytes = max(0, limits.maxHashBytes)
        guard hashedBytes <= maximumBytes, size >= 0, size <= maximumBytes - hashedBytes else {
            stopReason = "the \(Formatters.bytes(maximumBytes)) hashing limit"
            return false
        }
        return true
    }

    mutating func checkTime() -> Bool {
        guard ProcessInfo.processInfo.systemUptime - startedAt < limits.maxElapsedSeconds else {
            stopReason = "the \(Int(limits.maxElapsedSeconds))-second time limit"
            return false
        }
        return true
    }

    var partialWarning: String {
        "Scan stopped at \(stopReason ?? "a resource limit") after inspecting \(inspectedEntries) entries and hashing \(Formatters.bytes(hashedBytes)). Results are partial."
    }
}

final class StorageAnalysisService: @unchecked Sendable {
    private let fileManager: FileManager
    private let roots: [URL]?
    private let limits: StorageScanLimits

    init(fileManager: FileManager = .default, roots: [URL]? = nil, limits: StorageScanLimits = .standard) {
        self.fileManager = fileManager
        self.roots = roots
        self.limits = limits
    }

    func scanLargeFiles(minimumMB: Int, maxResults: Int = 300, policy: ProtectionPolicy, cancellation: ScanCancellation? = nil) -> StorageScanResult<LargeFileInfo> {
        var result = StorageScanResult<LargeFileInfo>()
        let minimumBytes = Int64(min(max(1, minimumMB), 10_000)) * 1_048_576
        var seenPaths = Set<String>()
        var seenObjects = Set<String>()
        var budget = StorageWorkBudget(limits: limits)

        scanRoots: for root in userStorageRoots() {
            if cancellation?.isCancelled == true { return result }
            guard let enumerator = fileManager.enumerator(
                at: root,
                includingPropertiesForKeys: [.isDirectoryKey, .isSymbolicLinkKey, .fileSizeKey, .totalFileAllocatedSizeKey, .contentModificationDateKey],
                options: [.skipsPackageDescendants],
                errorHandler: { url, error in
                    result.addWarning("Could not enumerate \(url.path): \(error.localizedDescription)")
                    return true
                }
            ) else {
                result.addWarning("Could not enumerate storage root: \(root.path)")
                continue
            }

            for case let url as URL in enumerator {
                if cancellation?.isCancelled == true { return result }
                guard budget.inspectEntry() else {
                    result.addWarning("Large-file \(budget.partialWarning)", priority: true)
                    break scanRoots
                }
                cancellation?.inspect()
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
                    if size >= minimumBytes && seenPaths.insert(url.standardized.path).inserted {
                        guard let identity = MacFileSafety.identity(at: url.path) else {
                            result.addWarning("Could not verify file identity: \(url.path)")
                            continue
                        }
                        guard seenObjects.insert("\(identity.device):\(identity.inode)").inserted else { continue }
                        result.items.append(LargeFileInfo(
                            path: url.path,
                            name: url.lastPathComponent,
                            directory: url.deletingLastPathComponent().path,
                            sizeBytes: size,
                            modifiedAt: values.contentModificationDate,
                            recommendation: recommendation(for: url),
                            identity: identity
                        ))
                    }
                } catch {
                    result.addWarning("Could not inspect \(url.path): \(error.localizedDescription)")
                }
            }
        }

        result.items.sort { $0.sizeBytes > $1.sizeBytes }
        if result.items.count > maxResults {
            result.items = Array(result.items.prefix(maxResults))
            result.addWarning("Large-file scan was limited to the largest \(maxResults) files.", priority: true)
        }

        return result
    }

    func scanDuplicates(minimumMB: Int, maxGroups: Int = 100, policy: ProtectionPolicy, cancellation: ScanCancellation? = nil) -> StorageScanResult<DuplicateFileInfo> {
        var result = StorageScanResult<DuplicateFileInfo>()
        let minimumBytes = Int64(min(max(1, minimumMB), 10_000)) * 1_048_576
        var filesBySize: [Int64: [URL]] = [:]
        var seenPaths = Set<String>()
        var seenObjects = Set<String>()
        var budget = StorageWorkBudget(limits: limits)

        inventoryRoots: for root in userStorageRoots() {
            if cancellation?.isCancelled == true { return result }
            guard let enumerator = fileManager.enumerator(
                at: root,
                includingPropertiesForKeys: [.isDirectoryKey, .isSymbolicLinkKey, .fileSizeKey, .totalFileAllocatedSizeKey, .contentModificationDateKey],
                options: [.skipsPackageDescendants],
                errorHandler: { url, error in
                    result.addWarning("Could not enumerate \(url.path): \(error.localizedDescription)")
                    return true
                }
            ) else {
                result.addWarning("Could not enumerate storage root: \(root.path)")
                continue
            }

            for case let url as URL in enumerator {
                if cancellation?.isCancelled == true { return result }
                guard budget.inspectEntry() else {
                    result.addWarning("Duplicate \(budget.partialWarning)", priority: true)
                    break inventoryRoots
                }
                cancellation?.inspect()
                do {
                    let values = try url.resourceValues(forKeys: [.isDirectoryKey, .isSymbolicLinkKey, .fileSizeKey, .totalFileAllocatedSizeKey])
                    if values.isSymbolicLink == true {
                        enumerator.skipDescendants()
                        continue
                    }
                    if values.isDirectory == true || policy.isProtected(url.path) {
                        continue
                    }

                    let size = Int64(values.fileSize ?? 0)
                    if size >= minimumBytes && seenPaths.insert(url.standardized.path).inserted {
                        guard let identity = MacFileSafety.identity(at: url.path) else {
                            result.addWarning("Could not verify file identity: \(url.path)")
                            continue
                        }
                        guard seenObjects.insert("\(identity.device):\(identity.inode)").inserted else { continue }
                        filesBySize[size, default: []].append(url)
                    }
                } catch {
                    result.addWarning("Could not inspect \(url.path): \(error.localizedDescription)")
                }
            }
        }

        budget.stopReason = nil
        var groups = 0
        for (size, urls) in filesBySize where urls.count > 1 {
            if cancellation?.isCancelled == true { return result }
            guard budget.checkTime() else {
                result.addWarning("Duplicate \(budget.partialWarning)", priority: true)
                return result
            }
            var byHash: [String: [URL]] = [:]
            for url in urls {
                if cancellation?.isCancelled == true { return result }
                guard budget.allowHash(of: size) else {
                    result.addWarning("Duplicate \(budget.partialWarning)", priority: true)
                    return result
                }
                if let hash = sha256(url: url, expectedBytes: size, cancellation: cancellation, budget: &budget) {
                    byHash[hash, default: []].append(url)
                } else if cancellation?.isCancelled == true {
                    return result
                } else if budget.stopReason != nil {
                    result.addWarning("Duplicate \(budget.partialWarning)", priority: true)
                    return result
                } else {
                    result.addWarning("Could not hash \(url.path); this file was excluded from duplicate results.")
                }
            }

            for (hash, matches) in byHash where matches.count > 1 {
                groups += 1
                let sorted = matches.sorted { $0.path < $1.path }
                for (index, url) in sorted.enumerated() {
                    let modified = (try? url.resourceValues(forKeys: [.contentModificationDateKey]))?.contentModificationDate
                    guard let identity = MacFileSafety.identity(at: url.path) else {
                        result.addWarning("Could not verify file identity: \(url.path)")
                        continue
                    }
                    result.items.append(DuplicateFileInfo(
                        groupId: "Duplicate set \(groups)",
                        path: url.path,
                        name: url.lastPathComponent,
                        directory: url.deletingLastPathComponent().path,
                        sizeBytes: Int64((try? url.resourceValues(forKeys: [.totalFileAllocatedSizeKey]))?.totalFileAllocatedSize ?? Int(size)),
                        logicalSizeBytes: size,
                        modifiedAt: modified,
                        sha256: hash,
                        isRecommendedForCleanup: index > 0,
                        identity: identity
                    ))
                }
                if groups >= maxGroups {
                    result.addWarning("Duplicate scan was limited to \(maxGroups) groups; results are partial.", priority: true)
                    return result
                }
            }
        }

        return result
    }

    func validateDuplicateSelection(
        all files: [DuplicateFileInfo],
        selectedIDs: Set<UUID>,
        keeperIDs: [String: UUID],
        policy: ProtectionPolicy
    ) -> (candidates: [CleanupCandidate], warnings: [String]) {
        var candidates: [CleanupCandidate] = []
        var warnings: [String] = []
        var budget = StorageWorkBudget(limits: limits)
        for (groupID, group) in Dictionary(grouping: files, by: \.groupId) {
            let selected = group.filter { selectedIDs.contains($0.id) }
            guard !selected.isEmpty else { continue }
            guard let keeperID = keeperIDs[groupID],
                  let keeper = group.first(where: { $0.id == keeperID }),
                  !selectedIDs.contains(keeperID)
            else {
                warnings.append("\(groupID): choose one explicit keeper. No file in this group was moved.")
                continue
            }
            guard isUnchangedDuplicate(keeper, policy: policy, budget: &budget) else {
                if budget.stopReason != nil {
                    warnings.append("Duplicate verification \(budget.partialWarning) No additional files were moved.")
                    return (candidates, warnings)
                }
                warnings.append("\(groupID): the chosen keeper changed or is unavailable. Rescan before quarantining this group.")
                continue
            }
            for file in selected {
                guard isUnchangedDuplicate(file, policy: policy, budget: &budget) else {
                    if budget.stopReason != nil {
                        warnings.append("Duplicate verification \(budget.partialWarning) No additional files were moved.")
                        return (candidates, warnings)
                    }
                    warnings.append("\(file.name) changed or is unavailable. Rescan duplicates before trying again.")
                    continue
                }
                candidates.append(CleanupCandidate(
                    categoryId: "duplicate-file-review",
                    categoryName: "Duplicate file review",
                    displayName: file.name,
                    path: file.path,
                    approvedRoot: file.directory,
                    kind: .file,
                    sizeBytes: file.sizeBytes,
                    lastModified: file.modifiedAt,
                    risk: .high,
                    identity: file.identity
                ))
            }
        }
        return (candidates, warnings)
    }

    private func isUnchangedDuplicate(_ file: DuplicateFileInfo, policy: ProtectionPolicy, budget: inout StorageWorkBudget) -> Bool {
        guard !policy.isProtected(file.path),
              let identity = file.identity,
              let current = MacFileSafety.identity(at: file.path),
              MacFileSafety.isSameObject(current, as: identity, includeContents: true),
              current.kind == .file,
              budget.allowHash(of: file.logicalSizeBytes),
              sha256(url: URL(fileURLWithPath: file.path), expectedBytes: file.logicalSizeBytes, cancellation: nil, budget: &budget) == file.sha256
        else { return false }
        return true
    }

    private func userStorageRoots() -> [URL] {
        if let roots { return roots.filter { fileManager.fileExists(atPath: $0.path) } }
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

    private func sha256(url: URL, expectedBytes: Int64, cancellation: ScanCancellation?, budget: inout StorageWorkBudget) -> String? {
        guard expectedBytes >= 0 else { return nil }
        guard let handle = try? FileHandle(forReadingFrom: url) else { return nil }
        defer { try? handle.close() }

        var hasher = SHA256()
        var remaining = expectedBytes
        while remaining > 0 {
            if cancellation?.isCancelled == true || !budget.checkTime() { return nil }
            let chunkSize = Int(min(remaining, 1_048_576))
            let data = autoreleasepool { handle.readData(ofLength: chunkSize) }
            guard !data.isEmpty else { return nil }
            hasher.update(data: data)
            remaining -= Int64(data.count)
            budget.hashedBytes += Int64(data.count)
        }
        guard cancellation?.isCancelled != true else { return nil }
        var metadata = stat()
        guard fstat(handle.fileDescriptor, &metadata) == 0, metadata.st_size == expectedBytes else { return nil }
        return hasher.finalize().map { String(format: "%02x", $0) }.joined()
    }
}

final class PerformanceAssistService: @unchecked Sendable {
    private static let oneGigabyte: Int64 = 1_073_741_824

    func snapshot(
        cleanupCandidateCount: Int,
        largeFileCount: Int,
        duplicateCount: Int,
        quarantineBytes: Int64
    ) -> PerformanceAssistResult {
        let physicalMemory = ProcessInfo.processInfo.physicalMemory
        let vmOutput = Self.run("/usr/bin/vm_stat", arguments: [])
        let memoryIsAvailable = vmOutput.contains("Pages free:") && vmOutput.contains("Pages inactive:")
        let memory = Self.parseVMStat(vmOutput, physicalMemory: physicalMemory)
        let swapUsed = Self.readSwapUsedBytes()
        let psOutput = Self.run("/bin/ps", arguments: ["-axo", "pid=,rss=,comm="])
        let processRows = Self.parseProcessRows(psOutput)
        let processes = Self.topProcesses(from: processRows)
        let pressure = memoryIsAvailable ? Self.classifyMemoryPressure(memory.usagePercent) : "Unavailable"
        let memorySummary = !memoryIsAvailable
            ? "Memory pressure could not be measured. Refresh RAM Assist to try again."
            : pressure == "Good"
                ? "RAM pressure is healthy. macOS uses available memory for useful file cache."
                : "RAM pressure is \(pressure.lowercased()). Review the largest apps before closing anything."
        let summary = processRows.isEmpty ? memorySummary + " Process sampling is unavailable; refresh to try again." : memorySummary
        let snapshot = SystemPerformanceSnapshot(
            totalMemoryBytes: memory.totalBytes,
            availableMemoryBytes: memory.availableBytes,
            memoryUsagePercent: memory.usagePercent,
            swapUsedBytes: swapUsed,
            processCount: processRows.count,
            uptimeSeconds: ProcessInfo.processInfo.systemUptime,
            memoryPressure: pressure,
            summary: summary
        )
        let recommendations = Self.buildRecommendations(
            snapshot: snapshot,
            processes: processes,
            cleanupCandidateCount: cleanupCandidateCount,
            largeFileCount: largeFileCount,
            duplicateCount: duplicateCount,
            quarantineBytes: quarantineBytes
        )

        return PerformanceAssistResult(snapshot: snapshot, processes: processes, recommendations: recommendations)
    }

    static func classifyMemoryPressure(_ memoryUsagePercent: Double) -> String {
        switch memoryUsagePercent {
        case 90...:
            return "Critical"
        case 80..<90:
            return "High"
        case 70..<80:
            return "Elevated"
        default:
            return "Good"
        }
    }

    static func buildRecommendations(
        snapshot: SystemPerformanceSnapshot,
        processes: [ProcessMemoryInfo],
        cleanupCandidateCount: Int,
        largeFileCount: Int,
        duplicateCount: Int,
        quarantineBytes: Int64
    ) -> [PerformanceRecommendation] {
        var recommendations: [PerformanceRecommendation] = []

        switch snapshot.memoryPressure {
        case "Unavailable":
            recommendations.append(PerformanceRecommendation(
                area: "RAM pressure",
                status: "Unavailable",
                recommendation: "Memory statistics could not be read. Refresh RAM Assist, then check Activity Monitor if the issue persists.",
                impact: "Avoids acting on an incomplete measurement.",
                action: "Refresh RAM Assist"
            ))
        case "Critical":
            recommendations.append(PerformanceRecommendation(
                area: "RAM pressure",
                status: "Critical",
                recommendation: "Save work, close or restart the largest apps, then recheck memory pressure.",
                impact: "Can improve responsiveness immediately.",
                action: "Open Activity Monitor"
            ))
        case "High":
            recommendations.append(PerformanceRecommendation(
                area: "RAM pressure",
                status: "Attention",
                recommendation: "Review the top memory apps and close anything you are not actively using.",
                impact: "Reduces swapping and app stalls.",
                action: "Review top processes"
            ))
        case "Elevated":
            recommendations.append(PerformanceRecommendation(
                area: "RAM pressure",
                status: "Watch",
                recommendation: "Memory use is elevated. Watch browsers, creative apps, VMs, and developer tools.",
                impact: "Prevents slowdowns before they become disruptive.",
                action: "Refresh RAM Assist"
            ))
        default:
            recommendations.append(PerformanceRecommendation(
                area: "RAM pressure",
                status: "Good",
                recommendation: "Memory pressure is healthy. Avoid force-purging RAM; macOS uses cache intentionally.",
                impact: "Keeps the system stable and responsive.",
                action: "No action needed"
            ))
        }

        if let topProcess = processes.first(where: { $0.residentMemoryBytes >= Self.oneGigabyte }) {
            recommendations.append(PerformanceRecommendation(
                area: "Top memory app",
                status: "Review",
                recommendation: "\(topProcess.name) is using a large amount of RAM. Restart it only if it feels stuck or is no longer needed.",
                impact: "Often recovers memory without risky system tweaks.",
                action: "Open Activity Monitor"
            ))
        }

        if let swapUsed = snapshot.swapUsedBytes, swapUsed >= 2 * Self.oneGigabyte {
            recommendations.append(PerformanceRecommendation(
                area: "Swap usage",
                status: "Attention",
                recommendation: "Swap usage is high. Close memory-heavy apps before starting more large tasks.",
                impact: "Reduces disk-backed memory pressure.",
                action: "Review top processes"
            ))
        }

        if snapshot.uptimeSeconds >= 7 * 86_400 {
            recommendations.append(PerformanceRecommendation(
                area: "Restart cadence",
                status: "Review",
                recommendation: "The Mac has been running for a week or more. Restart after saving work if performance feels degraded.",
                impact: "Clears hung helpers, stale drivers, and abandoned app memory.",
                action: "Restart later"
            ))
        }

        if cleanupCandidateCount > 0 || quarantineBytes > 0 {
            recommendations.append(PerformanceRecommendation(
                area: "Disk cache cleanup",
                status: "Ready",
                recommendation: "Review scanned cleanup candidates and purge quarantine only after the Mac stays stable.",
                impact: "Frees disk space without touching active app memory.",
                action: "Review Cleaner"
            ))
        }

        if largeFileCount > 0 || duplicateCount > 0 {
            recommendations.append(PerformanceRecommendation(
                area: "Storage pressure",
                status: "Review",
                recommendation: "Large files and duplicates can reduce free disk space, which macOS needs for swap and updates.",
                impact: "Improves headroom for virtual memory and system updates.",
                action: "Review Storage"
            ))
        }

        recommendations.append(PerformanceRecommendation(
            area: "Login items",
            status: "Manual",
            recommendation: "Review login items and background helpers in macOS System Settings.",
            impact: "Reduces background memory use after restart.",
            action: "Open Login Items"
        ))

        return recommendations
    }

    static func parseVMStat(_ output: String, physicalMemory: UInt64) -> (totalBytes: Int64, availableBytes: Int64, usagePercent: Double) {
        let parsedPageSize = parsePageSize(output) ?? 4_096
        let pageSize = Int64((1_024...65_536).contains(parsedPageSize) ? parsedPageSize : 4_096)
        let pages = parsePageCounts(output)
        let total = Int64(min(physicalMemory, UInt64(Int64.max)))
        let maximumPages = total / pageSize
        var availablePages: Int64 = 0
        for key in ["Pages free", "Pages speculative", "Pages inactive"] {
            let count = max(0, pages[key] ?? 0)
            availablePages += min(count, maximumPages - availablePages)
        }
        let available = availablePages * pageSize
        let used = max(0, total - available)
        let usage = total > 0 ? min(100, max(0, (Double(used) / Double(total)) * 100)) : 0

        return (total, available, usage)
    }

    static func parseProcessList(_ output: String) -> [ProcessMemoryInfo] {
        topProcesses(from: parseProcessRows(output))
    }

    private static func parseProcessRows(_ output: String) -> [ProcessMemoryInfo] {
        output
            .split(separator: "\n")
            .compactMap { line -> ProcessMemoryInfo? in
                let parts = line.split(maxSplits: 2, whereSeparator: { $0 == " " || $0 == "\t" })
                guard parts.count == 3,
                      let pid = Int(parts[0]),
                      let residentKilobytes = Int64(parts[1]),
                      residentKilobytes >= 0,
                      residentKilobytes <= Int64.max / 1_024
                else {
                    return nil
                }

                let command = String(parts[2])
                let name = URL(fileURLWithPath: command).lastPathComponent
                let residentBytes = residentKilobytes * 1_024
                return ProcessMemoryInfo(
                    processId: pid,
                    name: name.isEmpty ? command : name,
                    residentMemoryBytes: residentBytes,
                    commandPath: command,
                    recommendation: recommendation(for: name, residentBytes: residentBytes),
                    safetyNote: "Use Activity Monitor to quit apps. SpacePilot never ends processes automatically."
                )
            }
    }

    private static func topProcesses(from processes: [ProcessMemoryInfo]) -> [ProcessMemoryInfo] {
        processes
            .sorted { $0.residentMemoryBytes > $1.residentMemoryBytes }
            .prefix(25)
            .map { $0 }
    }

    private static func parsePageSize(_ output: String) -> Int? {
        guard let firstLine = output.split(separator: "\n").first else { return nil }
        return firstLine
            .split(whereSeparator: { !$0.isNumber })
            .compactMap { Int($0) }
            .first
    }

    private static func parsePageCounts(_ output: String) -> [String: Int64] {
        var values: [String: Int64] = [:]

        for line in output.split(separator: "\n") {
            guard let separator = line.firstIndex(of: ":") else { continue }
            let key = String(line[..<separator])
            let valuePart = line[line.index(after: separator)...]
            let number = valuePart
                .split(whereSeparator: { !$0.isNumber })
                .compactMap { Int64($0) }
                .first
            if let number {
                values[key] = number
            }
        }

        return values
    }

    private static func readSwapUsedBytes() -> Int64? {
        let output = run("/usr/sbin/sysctl", arguments: ["vm.swapusage"])
        guard let usedRange = output.range(of: "used = ") else { return nil }
        let remainder = output[usedRange.upperBound...]
        let value = remainder
            .split(whereSeparator: { !$0.isNumber && $0 != "." })
            .first
            .flatMap { Double($0) }
        guard let value, value.isFinite, value >= 0 else { return nil }
        let bytes = value * (remainder.contains("G") ? Double(oneGigabyte) : 1_048_576)
        guard bytes.isFinite, bytes < Double(Int64.max) else { return nil }
        return Int64(bytes)
    }

    private static func recommendation(for processName: String, residentBytes: Int64) -> String {
        let normalized = processName.lowercased()
        if normalized.contains("safari") || normalized.contains("chrome") || normalized.contains("firefox") || normalized.contains("brave") {
            return "Restart the browser or reduce tabs/extensions if memory keeps growing."
        }

        if normalized.contains("xcode") || normalized.contains("code") || normalized.contains("studio") {
            return "Close unused projects, simulators, terminals, and build tasks first."
        }

        if residentBytes >= 2 * oneGigabyte {
            return "Restart or quit this app if it is not doing active work."
        }

        if residentBytes >= oneGigabyte {
            return "Review if performance feels slow; otherwise leave it alone."
        }

        return "Monitor only."
    }

    static func run(_ executable: String, arguments: [String], timeoutSeconds: TimeInterval = 10, maxOutputBytes: Int = 2 * 1_048_576) -> String {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = arguments
        let output = Pipe()
        process.standardOutput = output
        process.standardError = output
        let descriptor = output.fileHandleForReading.fileDescriptor
        let flags = Darwin.fcntl(descriptor, F_GETFL)
        guard flags >= 0, Darwin.fcntl(descriptor, F_SETFL, flags | O_NONBLOCK) >= 0 else { return "" }
        let completed = DispatchSemaphore(value: 0)
        process.terminationHandler = { _ in completed.signal() }
        let deadline = ProcessInfo.processInfo.systemUptime + min(max(timeoutSeconds, 0.05), 15)
        let byteLimit = min(max(maxOutputBytes, 1), 2 * 1_048_576)

        do {
            try process.run()
            defer { output.fileHandleForReading.closeFile() }
            var data = Data()
            var reachedEOF = false
            var buffer = [UInt8](repeating: 0, count: 8_192)

            while true {
                let remaining = deadline - ProcessInfo.processInfo.systemUptime
                guard remaining > 0 else {
                    Self.stopTimedOutProcess(process, completed: completed)
                    return ""
                }
                if reachedEOF {
                    if completed.wait(timeout: .now() + .milliseconds(min(50, max(1, Int(remaining * 1_000))))) == .success {
                        break
                    }
                    continue
                }

                var pollState = pollfd(fd: descriptor, events: Int16(POLLIN | POLLHUP | POLLERR), revents: 0)
                let pollResult = Darwin.poll(&pollState, 1, Int32(min(250, max(1, Int(remaining * 1_000)))))
                if pollResult < 0 {
                    if errno == EINTR { continue }
                    Self.stopTimedOutProcess(process, completed: completed)
                    return ""
                }
                guard pollResult > 0 else { continue }

                let readCount = buffer.withUnsafeMutableBytes { rawBuffer in
                    Darwin.read(descriptor, rawBuffer.baseAddress, rawBuffer.count)
                }
                if readCount == 0 {
                    reachedEOF = true
                } else if readCount > 0 {
                    guard readCount <= byteLimit - data.count else {
                        Self.stopTimedOutProcess(process, completed: completed)
                        return ""
                    }
                    data.append(contentsOf: buffer.prefix(readCount))
                } else if errno != EAGAIN && errno != EINTR {
                    Self.stopTimedOutProcess(process, completed: completed)
                    return ""
                }
            }
            guard process.terminationStatus == 0 else { return "" }
            return String(data: data, encoding: .utf8) ?? ""
        } catch {
            return ""
        }
    }

    private static func stopTimedOutProcess(_ process: Process, completed: DispatchSemaphore) {
        guard process.isRunning else { return }
        process.terminate()
        if completed.wait(timeout: .now() + .milliseconds(500)) == .timedOut {
            Darwin.kill(process.processIdentifier, SIGKILL)
            _ = completed.wait(timeout: .now() + .milliseconds(500))
        }
    }
}
