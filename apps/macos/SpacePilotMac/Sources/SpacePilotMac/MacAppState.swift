import Foundation
import AppKit
import SwiftUI

@MainActor
final class MacAppState: ObservableObject {
    @Published var selectedSection: MacSection = .health
    @Published var preferences: MacPreferences
    @Published var cleanupRules: [CleanupRule]
    @Published var cleanupCandidates: [CleanupCandidate] = []
    @Published var selectedCandidateIDs: Set<UUID> = []
    @Published var selectedLargeFileIDs: Set<UUID> = []
    @Published var selectedDuplicateIDs: Set<UUID> = []
    @Published var chosenDuplicateKeepers: [String: UUID] = [:]
    @Published var selectedQuarantineIDs: Set<String> = []
    @Published var largeFiles: [LargeFileInfo] = []
    @Published var duplicateFiles: [DuplicateFileInfo] = []
    @Published var quarantineEntries: [QuarantineEntry] = []
    @Published var receipts: [CleanupReceipt] = []
    @Published var performanceSnapshot: SystemPerformanceSnapshot?
    @Published var memoryProcesses: [ProcessMemoryInfo] = []
    @Published var performanceRecommendations: [PerformanceRecommendation] = []
    @Published var volumeSnapshot: VolumeStorageSnapshot?
    @Published var volumeWarning: String?
    @Published var activityLog: [ActivityLogEntry] = []
    @Published var searchText = ""
    @Published var riskFilter = "All risks"
    @Published var categoryFilter = "All categories"
    @Published var statusMessage = "Ready."
    @Published var isBusy = false
    @Published var cleanupScanComplete = false
    @Published var largeFileScanComplete = false
    @Published var duplicateScanComplete = false
    @Published var lastWarnings: [String] = []
    @Published var recoveryWarnings: [String] = []
    @Published var cleanupWarnings: [String] = []
    @Published var largeFileWarnings: [String] = []
    @Published var duplicateWarnings: [String] = []
    @Published var scanProgressCount = 0

    private let preferencesService: PreferencesService
    private let cleanerService: MacCleanerService
    private let quarantineService: QuarantineService
    private let receiptService: ReceiptService
    private let storageService: StorageAnalysisService
    private let performanceService: PerformanceAssistService
    private let volumeStorageService: VolumeStorageService
    private var activeScanCancellation: ScanCancellation?
    private var progressTask: Task<Void, Never>?

    var canCancelScan: Bool { activeScanCancellation != nil }

    init(
        preferencesService: PreferencesService = PreferencesService(),
        cleanerService: MacCleanerService = MacCleanerService(),
        quarantineService: QuarantineService = QuarantineService(),
        receiptService: ReceiptService = ReceiptService(),
        storageService: StorageAnalysisService = StorageAnalysisService(),
        performanceService: PerformanceAssistService = PerformanceAssistService(),
        volumeStorageService: VolumeStorageService = VolumeStorageService()
    ) {
        self.preferencesService = preferencesService
        self.preferences = preferencesService.load()
        self.cleanupRules = MacCleanupRuleCatalog.defaultRules()
        self.cleanerService = cleanerService
        self.quarantineService = quarantineService
        self.receiptService = receiptService
        self.storageService = storageService
        self.performanceService = performanceService
        self.volumeStorageService = volumeStorageService
        refreshRecovery()
        refreshVolumeStorage()
        addActivity("Info", "SpacePilot for macOS started.")
        refreshPerformance()
    }

    var productSubtitle: String {
        "Safe cleanup and storage recovery for macOS."
    }

    var totalScannedBytes: Int64 {
        cleanupCandidates.reduce(0) { $0 + $1.sizeBytes }
    }

    var selectedCleanupBytes: Int64 {
        cleanupCandidates.filter { selectedCandidateIDs.contains($0.id) }.reduce(0) { $0 + $1.sizeBytes }
    }

    var quarantineBytes: Int64 {
        quarantineEntries.reduce(0) { $0 + $1.sizeBytes }
    }

    var filteredCandidates: [CleanupCandidate] {
        cleanupCandidates.filter { candidate in
            let matchesSearch = searchText.isEmpty
                || candidate.displayName.localizedCaseInsensitiveContains(searchText)
                || candidate.categoryName.localizedCaseInsensitiveContains(searchText)
                || candidate.path.localizedCaseInsensitiveContains(searchText)
            let matchesRisk = riskFilter == "All risks" || candidate.risk.rawValue == riskFilter
            let matchesCategory = categoryFilter == "All categories" || candidate.categoryId == categoryFilter
            return matchesSearch && matchesRisk && matchesCategory
        }
    }

    var selectedLargeFiles: [LargeFileInfo] {
        largeFiles.filter { selectedLargeFileIDs.contains($0.id) }
    }

    var selectedDuplicates: [DuplicateFileInfo] {
        duplicateFiles.filter { selectedDuplicateIDs.contains($0.id) }
    }

    var duplicateGroups: [DuplicateGroup] {
        Dictionary(grouping: duplicateFiles, by: \.groupId)
            .map { DuplicateGroup(id: $0.key, files: $0.value.sorted { $0.path < $1.path }) }
            .sorted { $0.id.localizedStandardCompare($1.id) == .orderedAscending }
    }

    var summaryText: String {
        if cleanupCandidates.isEmpty {
            return cleanupScanComplete ? "Scan complete: no eligible cleanup files found." : "No scan has been run yet."
        }
        return "\(cleanupCandidates.count) files to review" + (cleanupWarnings.contains { $0.contains("partial") } ? " (partial scan)" : "")
    }

    func refreshVolumeStorage() {
        do {
            volumeSnapshot = try volumeStorageService.snapshot()
            volumeWarning = nil
        } catch {
            volumeSnapshot = nil
            volumeWarning = error.localizedDescription
        }
    }

    func reviewCleanupCategory(_ ruleID: String) {
        searchText = ""
        riskFilter = "All risks"
        categoryFilter = ruleID
        selectedSection = .cleaner
    }

    var performanceSummary: String {
        performanceSnapshot?.summary ?? "Refresh RAM Assist to inspect memory pressure and background load."
    }

    var memoryUsagePercentText: String {
        guard let snapshot = performanceSnapshot else { return "Not sampled" }
        guard snapshot.memoryPressure != "Unavailable" else { return "Unavailable" }
        return "\(Int(snapshot.memoryUsagePercent.rounded()))%"
    }

    var swapSummary: String {
        guard let swapUsed = performanceSnapshot?.swapUsedBytes else {
            return "Swap data is not available."
        }

        return "\(Formatters.bytes(swapUsed)) swap used."
    }

    var performanceProcessSummary: String {
        memoryProcesses.isEmpty
            ? (performanceSnapshot == nil ? "Refresh RAM Assist to list the highest-memory processes." : "Process sampling is unavailable. Refresh RAM Assist to try again.")
            : "\(memoryProcesses.count) top processes by resident memory."
    }

    var selectedSummary: String {
        "\(selectedCandidateIDs.count) selected, \(Formatters.bytes(selectedCleanupBytes))"
    }

    var protectionPolicy: ProtectionPolicy {
        ProtectionPolicy(protectedExtensions: preferences.protectedExtensions, protectedPaths: preferences.protectedPaths)
    }

    func dismissFirstRun() {
        preferences.isFirstRun = false
        savePreferences()
        addActivity("Info", "First-run safety note dismissed.")
    }

    func savePreferences() {
        guard (25...10_000).contains(preferences.largeFileMinimumMB),
              (5...10_000).contains(preferences.duplicateMinimumMB)
        else {
            statusMessage = "Enter a large-file minimum of 25–10,000 MB and a duplicate minimum of 5–10,000 MB. Settings were not saved."
            addActivity("Warn", statusMessage)
            return
        }
        if let warning = preferencesService.save(preferences) {
            statusMessage = warning
            addActivity("Warn", warning)
        } else {
            statusMessage = "Settings saved."
        }
    }

    func scanCleanup() {
        guard !isBusy else { statusMessage = "Wait for the current operation to finish."; return }
        let cancellation = beginScanProgress()
        runBusy("Scanning macOS cleanup locations...") {
            defer { self.endScanProgress() }
            let rules = self.cleanupRules
            let policy = self.protectionPolicy
            let service = self.cleanerService
            let result = await Task.detached {
                service.scan(rules: rules, policy: policy, cancellation: cancellation)
            }.value
            guard !cancellation.isCancelled else {
                self.statusMessage = "Cleanup scan cancelled after \(cancellation.inspectedCount) inspected items. Previous results remain."
                return
            }
            self.cleanupCandidates = result.candidates
            self.cleanupScanComplete = true
            self.selectedCandidateIDs = Set(result.candidates.filter { candidate in
                rules.first(where: { $0.id == candidate.categoryId })?.defaultSelected == true
            }.map(\.id))
            self.lastWarnings = result.warnings
            self.cleanupWarnings = result.warnings
            self.refreshVolumeStorage()
            result.warnings.forEach { self.addActivity("Warn", $0) }
            self.statusMessage = "Scan complete: \(result.candidates.count) items, \(Formatters.bytes(result.totalBytes))" + (result.warnings.isEmpty ? "." : "; \(result.warnings.count) warnings.")
            self.addActivity("Info", self.statusMessage)
        }
    }

    func toggleCandidate(_ candidate: CleanupCandidate) {
        if selectedCandidateIDs.contains(candidate.id) {
            selectedCandidateIDs.remove(candidate.id)
        } else {
            selectedCandidateIDs.insert(candidate.id)
        }
    }

    func selectAllFiltered() {
        selectedCandidateIDs.formUnion(filteredCandidates.map(\.id))
    }

    func clearSelection() {
        selectedCandidateIDs.removeAll()
    }

    func cleanSelected() {
        let selected = cleanupCandidates.filter { selectedCandidateIDs.contains($0.id) }
        guard !selected.isEmpty else {
            statusMessage = "Select cleanup items first."
            return
        }

        runBusy("Moving selected items to quarantine...") {
            let service = self.quarantineService
            let policy = self.protectionPolicy
            let result = await Task.detached {
                service.quarantine(selected, policy: policy)
            }.value
            let completedPaths = Set(result.entries.map(\.originalPath))
            self.cleanupCandidates.removeAll { completedPaths.contains($0.path) }
            self.selectedCandidateIDs = Set(self.cleanupCandidates.filter { self.selectedCandidateIDs.contains($0.id) }.map(\.id))
            self.recordQuarantine(selected, entries: result.entries, warnings: result.warnings, mode: "macOS cleanup")
        }
    }

    func scanLargeFiles() {
        guard (25...10_000).contains(preferences.largeFileMinimumMB) else {
            statusMessage = "Large-file minimum must be between 25 and 10,000 MB."
            return
        }
        guard !isBusy else { statusMessage = "Wait for the current operation to finish."; return }
        let cancellation = beginScanProgress()
        runBusy("Scanning personal folders for large files...") {
            defer { self.endScanProgress() }
            let minimum = self.preferences.largeFileMinimumMB
            let policy = self.protectionPolicy
            let service = self.storageService
            let result = await Task.detached {
                service.scanLargeFiles(minimumMB: minimum, policy: policy, cancellation: cancellation)
            }.value
            guard !cancellation.isCancelled else {
                self.statusMessage = "Large-file scan cancelled after \(cancellation.inspectedCount) inspected items. Previous results remain."
                return
            }
            self.largeFiles = result.items
            self.largeFileScanComplete = true
            self.selectedLargeFileIDs.removeAll()
            self.lastWarnings = result.warnings
            self.largeFileWarnings = result.warnings
            result.warnings.forEach { self.addActivity("Warn", $0) }
            self.statusMessage = "Large-file scan complete: \(result.items.count) files found" + (result.warnings.isEmpty ? "." : "; \(result.warnings.count) warnings.")
            self.addActivity("Info", self.statusMessage)
        }
    }

    func scanDuplicates() {
        guard (5...10_000).contains(preferences.duplicateMinimumMB) else {
            statusMessage = "Duplicate minimum must be between 5 and 10,000 MB."
            return
        }
        guard !isBusy else { statusMessage = "Wait for the current operation to finish."; return }
        let cancellation = beginScanProgress()
        runBusy("Hashing files to find duplicates...") {
            defer { self.endScanProgress() }
            let minimum = self.preferences.duplicateMinimumMB
            let policy = self.protectionPolicy
            let service = self.storageService
            let result = await Task.detached {
                service.scanDuplicates(minimumMB: minimum, policy: policy, cancellation: cancellation)
            }.value
            guard !cancellation.isCancelled else {
                self.statusMessage = "Duplicate scan cancelled after \(cancellation.inspectedCount) inspected items. Previous results remain."
                return
            }
            self.duplicateFiles = result.items
            self.duplicateScanComplete = true
            self.selectedDuplicateIDs.removeAll()
            self.chosenDuplicateKeepers.removeAll()
            self.lastWarnings = result.warnings
            self.duplicateWarnings = result.warnings
            result.warnings.forEach { self.addActivity("Warn", $0) }
            self.statusMessage = "Duplicate scan complete: \(result.items.count) duplicate entries found. Select copies to quarantine; keep at least one per group" + (result.warnings.isEmpty ? "." : "; \(result.warnings.count) warnings.")
            self.addActivity("Info", self.statusMessage)
        }
    }

    func quarantineLargeFiles() {
        quarantineManualFiles(selectedLargeFiles, categoryName: "Large file review", selectedIDs: \.selectedLargeFileIDs)
    }

    func toggleDuplicate(_ file: DuplicateFileInfo) {
        if selectedDuplicateIDs.contains(file.id) {
            selectedDuplicateIDs.remove(file.id)
            return
        }
        guard let keeper = chosenDuplicateKeepers[file.groupId] else {
            statusMessage = "Choose a keeper in \(file.groupId) before selecting copies."
            return
        }
        guard keeper != file.id else {
            statusMessage = "This is the keeper for \(file.groupId). Choose another keeper first."
            return
        }
        selectedDuplicateIDs.insert(file.id)
    }

    func chooseDuplicateKeeper(_ file: DuplicateFileInfo) {
        chosenDuplicateKeepers[file.groupId] = file.id
        selectedDuplicateIDs.remove(file.id)
        statusMessage = "Keeping \(file.name) in \(file.groupId). Review other copies before quarantine."
    }

    func quarantineDuplicates() {
        let all = duplicateFiles
        let selectedIDs = selectedDuplicateIDs
        let keepers = chosenDuplicateKeepers
        let requested = all.filter { selectedIDs.contains($0.id) }.map {
            manualCandidate(path: $0.path, name: $0.name, categoryName: "Duplicate file review", size: $0.sizeBytes, modified: $0.modifiedAt, identity: $0.identity)
        }
        guard !selectedIDs.isEmpty else { return }
        runBusy("Rechecking duplicate groups before quarantine...") {
            let storage = self.storageService
            let policy = self.protectionPolicy
            let validated = await Task.detached {
                storage.validateDuplicateSelection(all: all, selectedIDs: selectedIDs, keeperIDs: keepers, policy: policy)
            }.value
            guard !validated.candidates.isEmpty else {
                self.recordQuarantine(requested, entries: [], warnings: validated.warnings, mode: "Duplicate file review")
                return
            }
            let service = self.quarantineService
            let result = await Task.detached {
                service.quarantine(validated.candidates, policy: policy)
            }.value
            let completedPaths = Set(result.entries.map(\.originalPath))
            self.duplicateFiles.removeAll { completedPaths.contains($0.path) }
            let remainingGroupCounts = Dictionary(grouping: self.duplicateFiles, by: \.groupId).mapValues(\.count)
            self.duplicateFiles.removeAll { remainingGroupCounts[$0.groupId, default: 0] < 2 }
            let remainingIDs = Set(self.duplicateFiles.map(\.id))
            self.selectedDuplicateIDs.formIntersection(remainingIDs)
            self.chosenDuplicateKeepers = self.chosenDuplicateKeepers.filter { remainingGroupCounts[$0.key, default: 0] >= 2 }
            self.recordQuarantine(
                requested,
                entries: result.entries,
                warnings: validated.warnings + result.warnings,
                mode: "Duplicate file review"
            )
        }
    }

    func restoreSelectedQuarantine() {
        restoreSelectedQuarantine(to: nil)
    }

    func restoreSelectedQuarantine(to directory: URL?) {
        guard !selectedQuarantineIDs.isEmpty else { return }
        let ids = selectedQuarantineIDs.intersection(Set(quarantineEntries.map(\.id)))
        guard !ids.isEmpty else {
            selectedQuarantineIDs.removeAll()
            refreshRecovery()
            statusMessage = "The selected recovery items are no longer available. Refresh and select an item again."
            return
        }
        runBusy("Restoring selected quarantine items...") {
            let service = self.quarantineService
            let outcome = await Task.detached {
                let before = Set(service.entries().map(\.id)).intersection(ids)
                return (before, service.restore(ids: ids, destinationDirectory: directory))
            }.value
            let (before, warnings) = outcome
            self.lastWarnings = warnings
            warnings.forEach { self.addActivity("Warn", $0) }
            self.selectedQuarantineIDs.removeAll()
            self.refreshRecovery()
            self.refreshVolumeStorage()
            let restored = before.subtracting(Set(self.quarantineEntries.map(\.id))).count
            self.statusMessage = "Restored \(restored) of \(ids.count) items" + (warnings.isEmpty ? "." : "; \(warnings.count) warnings.")
            self.addActivity("Info", self.statusMessage)
        }
    }

    func purgeSelectedQuarantine() {
        guard !selectedQuarantineIDs.isEmpty else { return }
        let ids = selectedQuarantineIDs.intersection(Set(quarantineEntries.map(\.id)))
        guard !ids.isEmpty else {
            selectedQuarantineIDs.removeAll()
            refreshRecovery()
            statusMessage = "The selected recovery items are no longer available. Refresh and select an item again."
            return
        }
        runBusy("Purging selected quarantine items...") {
            let service = self.quarantineService
            let outcome = await Task.detached {
                let before = Set(service.entries().map(\.id)).intersection(ids)
                return (before, service.purge(ids: ids))
            }.value
            let (before, warnings) = outcome
            self.lastWarnings = warnings
            warnings.forEach { self.addActivity("Warn", $0) }
            self.selectedQuarantineIDs.removeAll()
            self.refreshRecovery()
            self.refreshVolumeStorage()
            let purged = before.subtracting(Set(self.quarantineEntries.map(\.id))).count
            self.statusMessage = "Purged \(purged) of \(ids.count) items" + (warnings.isEmpty ? "." : "; \(warnings.count) warnings.")
            self.addActivity("Info", self.statusMessage)
        }
    }

    func refreshRecovery() {
        quarantineEntries = quarantineService.entries()
        selectedQuarantineIDs.formIntersection(Set(quarantineEntries.map(\.id)))
        let receiptSnapshot = receiptService.loadSnapshot()
        receipts = receiptSnapshot.receipts
        recoveryWarnings = quarantineService.diagnostics() + receiptSnapshot.diagnostics
    }

    func refreshPerformance() {
        runBusy("Refreshing RAM Assist...") {
            let service = self.performanceService
            let cleanupCandidateCount = self.cleanupCandidates.count
            let largeFileCount = self.largeFiles.count
            let duplicateCount = self.duplicateFiles.count
            let quarantineBytes = self.quarantineBytes
            let result = await Task.detached {
                service.snapshot(
                    cleanupCandidateCount: cleanupCandidateCount,
                    largeFileCount: largeFileCount,
                    duplicateCount: duplicateCount,
                    quarantineBytes: quarantineBytes
                )
            }.value

            self.performanceSnapshot = result.snapshot
            self.memoryProcesses = result.processes
            self.performanceRecommendations = result.recommendations
            self.statusMessage = "RAM Assist refreshed: \(result.snapshot.memoryPressure) memory pressure."
            self.addActivity("Info", self.statusMessage)
        }
    }

    func openActivityMonitor() {
        NSWorkspace.shared.open(URL(fileURLWithPath: "/System/Applications/Utilities/Activity Monitor.app"))
    }

    func openLoginItems() {
        guard let url = URL(string: "x-apple.systempreferences:com.apple.LoginItems-Settings.extension") else {
            return
        }

        NSWorkspace.shared.open(url)
    }

    func clearActivity() {
        activityLog.removeAll()
        addActivity("Info", "Activity log cleared.")
    }

    private func quarantineManualFiles(_ files: [LargeFileInfo], categoryName: String, selectedIDs: ReferenceWritableKeyPath<MacAppState, Set<UUID>>) {
        let candidates = files.map {
            manualCandidate(path: $0.path, name: $0.name, categoryName: categoryName, size: $0.sizeBytes, modified: $0.modifiedAt, identity: $0.identity)
        }
        quarantineManualCandidates(candidates, mode: categoryName) { completedPaths in
            self.largeFiles.removeAll { completedPaths.contains($0.path) }
            for file in files where completedPaths.contains(file.path) {
                self[keyPath: selectedIDs].remove(file.id)
            }
        }
    }

    private func quarantineManualCandidates(_ candidates: [CleanupCandidate], mode: String, afterComplete: @escaping (Set<String>) -> Void) {
        guard !candidates.isEmpty else {
            statusMessage = "Select files first."
            return
        }

        runBusy("Moving selected files to quarantine...") {
            let service = self.quarantineService
            let policy = self.protectionPolicy
            let result = await Task.detached {
                service.quarantine(candidates, policy: policy)
            }.value
            let completedPaths = Set(result.entries.map(\.originalPath))
            afterComplete(completedPaths)
            self.recordQuarantine(candidates, entries: result.entries, warnings: result.warnings, mode: mode)
        }
    }

    private func recordQuarantine(_ candidates: [CleanupCandidate], entries: [QuarantineEntry], warnings: [String], mode: String) {
        var byPath: [String: QuarantineEntry] = [:]
        for entry in entries { byPath[entry.originalPath] = entry }
        let receipt = CleanupReceipt(
            id: UUID().uuidString,
            timestamp: Date(),
            mode: mode,
            requestedCount: candidates.count,
            completedCount: entries.count,
            requestedBytes: candidates.reduce(0) { $0 + $1.sizeBytes },
            completedBytes: entries.reduce(0) { $0 + $1.sizeBytes },
            items: candidates.map { candidate in
                let entry = byPath[candidate.path]
                return CleanupReceiptItem(
                    path: candidate.path,
                    categoryName: candidate.categoryName,
                    action: entry == nil ? "Skipped" : "Quarantined",
                    sizeBytes: candidate.sizeBytes,
                    quarantineId: entry?.id,
                    message: entry == nil ? warnings.first(where: { $0.contains(candidate.path) || $0.contains(candidate.displayName) }) ?? "Item was not quarantined; rescan and review warnings." : nil
                )
            },
            warnings: warnings
        )
        var allWarnings = warnings
        if let receiptWarning = receiptService.save(receipt) {
            allWarnings.append(receiptWarning)
        }
        allWarnings.forEach { addActivity("Warn", $0) }
        lastWarnings = allWarnings
        refreshRecovery()
        refreshVolumeStorage()
        statusMessage = "Quarantined \(entries.count) of \(candidates.count) items. Purge them in Recovery to free disk space" + (allWarnings.isEmpty ? "." : "; \(allWarnings.count) warnings.")
        addActivity("Info", statusMessage)
    }

    private func manualCandidate(path: String, name: String, categoryName: String, size: Int64, modified: Date?, identity: FileIdentity?) -> CleanupCandidate {
        CleanupCandidate(
            categoryId: categoryName.replacingOccurrences(of: " ", with: "-").lowercased(),
            categoryName: categoryName,
            displayName: name,
            path: path,
            approvedRoot: URL(fileURLWithPath: path).deletingLastPathComponent().path,
            kind: .file,
            sizeBytes: size,
            lastModified: modified,
            risk: .high,
            identity: identity
        )
    }

    private func runBusy(_ message: String, operation: @escaping () async -> Void) {
        guard !isBusy else {
            statusMessage = "An operation is already in progress. Wait for it to finish."
            return
        }
        isBusy = true
        statusMessage = message
        Task {
            await operation()
            isBusy = false
        }
    }

    func cancelCurrentScan() {
        guard let activeScanCancellation else { return }
        activeScanCancellation.cancel()
        statusMessage = "Stopping scan after the current file..."
    }

    private func beginScanProgress() -> ScanCancellation {
        let cancellation = ScanCancellation()
        activeScanCancellation = cancellation
        scanProgressCount = 0
        progressTask = Task {
            while !Task.isCancelled {
                try? await Task.sleep(for: .milliseconds(300))
                if Task.isCancelled { break }
                scanProgressCount = cancellation.inspectedCount
            }
        }
        return cancellation
    }

    private func endScanProgress() {
        scanProgressCount = activeScanCancellation?.inspectedCount ?? scanProgressCount
        activeScanCancellation = nil
        progressTask?.cancel()
        progressTask = nil
    }

    private func addActivity(_ level: String, _ message: String) {
        activityLog.insert(ActivityLogEntry(timestamp: Date(), level: level, message: message), at: 0)
    }
}
