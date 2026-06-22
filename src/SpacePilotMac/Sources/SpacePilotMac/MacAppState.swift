import Foundation
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
    @Published var selectedQuarantineIDs: Set<String> = []
    @Published var largeFiles: [LargeFileInfo] = []
    @Published var duplicateFiles: [DuplicateFileInfo] = []
    @Published var quarantineEntries: [QuarantineEntry] = []
    @Published var receipts: [CleanupReceipt] = []
    @Published var activityLog: [ActivityLogEntry] = []
    @Published var searchText = ""
    @Published var riskFilter = "All risks"
    @Published var statusMessage = "Ready."
    @Published var isBusy = false

    private let preferencesService: PreferencesService
    private let cleanerService: MacCleanerService
    private let quarantineService: QuarantineService
    private let receiptService: ReceiptService
    private let storageService: StorageAnalysisService

    init(
        preferencesService: PreferencesService = PreferencesService(),
        cleanerService: MacCleanerService = MacCleanerService(),
        quarantineService: QuarantineService = QuarantineService(),
        receiptService: ReceiptService = ReceiptService(),
        storageService: StorageAnalysisService = StorageAnalysisService()
    ) {
        self.preferencesService = preferencesService
        self.preferences = preferencesService.load()
        self.cleanupRules = MacCleanupRuleCatalog.defaultRules()
        self.cleanerService = cleanerService
        self.quarantineService = quarantineService
        self.receiptService = receiptService
        self.storageService = storageService
        refreshRecovery()
        addActivity("Info", "SpacePilot for macOS started.")
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
            return matchesSearch && matchesRisk
        }
    }

    var selectedLargeFiles: [LargeFileInfo] {
        largeFiles.filter { selectedLargeFileIDs.contains($0.id) }
    }

    var selectedDuplicates: [DuplicateFileInfo] {
        duplicateFiles.filter { selectedDuplicateIDs.contains($0.id) }
    }

    var summaryText: String {
        if cleanupCandidates.isEmpty {
            return "No scan has been run yet."
        }
        return "\(cleanupCandidates.count) cleanup candidates, \(Formatters.bytes(totalScannedBytes)) found."
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
        preferencesService.save(preferences)
    }

    func scanCleanup() {
        runBusy("Scanning macOS cleanup locations...") {
            let rules = self.cleanupRules
            let policy = self.protectionPolicy
            let service = self.cleanerService
            let result = await Task.detached {
                service.scan(rules: rules, policy: policy)
            }.value
            self.cleanupCandidates = result.candidates
            self.selectedCandidateIDs = Set(result.candidates.filter { candidate in
                rules.first(where: { $0.id == candidate.categoryId })?.defaultSelected == true
            }.map(\.id))
            result.warnings.forEach { self.addActivity("Warn", $0) }
            self.statusMessage = "Scan complete: \(result.candidates.count) items, \(Formatters.bytes(result.totalBytes))."
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
            let result = await Task.detached {
                service.quarantine(selected)
            }.value
            let completedIds = Set(result.entries.map(\.id))
            let completedPaths = Set(result.entries.map(\.originalPath))
            self.cleanupCandidates.removeAll { completedPaths.contains($0.path) }
            self.selectedCandidateIDs.removeAll()
            result.warnings.forEach { self.addActivity("Warn", $0) }
            let receipt = CleanupReceipt(
                id: UUID().uuidString,
                timestamp: Date(),
                mode: "macOS quarantine",
                requestedCount: selected.count,
                completedCount: result.entries.count,
                requestedBytes: selected.reduce(0) { $0 + $1.sizeBytes },
                completedBytes: result.entries.reduce(0) { $0 + $1.sizeBytes },
                items: selected.map { candidate in
                    let entry = result.entries.first { $0.originalPath == candidate.path }
                    return CleanupReceiptItem(
                        path: candidate.path,
                        categoryName: candidate.categoryName,
                        action: entry == nil ? "Skipped" : "Quarantined",
                        sizeBytes: candidate.sizeBytes,
                        quarantineId: entry?.id,
                        message: entry == nil ? "Item was not quarantined. Review warnings." : nil
                    )
                },
                warnings: result.warnings
            )
            self.receiptService.save(receipt)
            self.refreshRecovery()
            self.statusMessage = "Quarantined \(completedIds.count) items."
            self.addActivity("Info", self.statusMessage)
        }
    }

    func scanLargeFiles() {
        runBusy("Scanning personal folders for large files...") {
            let minimum = self.preferences.largeFileMinimumMB
            let policy = self.protectionPolicy
            let service = self.storageService
            let result = await Task.detached {
                service.scanLargeFiles(minimumMB: minimum, policy: policy)
            }.value
            self.largeFiles = result.items
            self.selectedLargeFileIDs.removeAll()
            result.warnings.forEach { self.addActivity("Warn", $0) }
            self.statusMessage = "Large-file scan complete: \(result.items.count) files found."
            self.addActivity("Info", self.statusMessage)
        }
    }

    func scanDuplicates() {
        runBusy("Hashing files to find duplicates...") {
            let minimum = self.preferences.duplicateMinimumMB
            let policy = self.protectionPolicy
            let service = self.storageService
            let result = await Task.detached {
                service.scanDuplicates(minimumMB: minimum, policy: policy)
            }.value
            self.duplicateFiles = result.items
            self.selectedDuplicateIDs = Set(result.items.filter(\.isRecommendedForCleanup).map(\.id))
            result.warnings.forEach { self.addActivity("Warn", $0) }
            self.statusMessage = "Duplicate scan complete: \(result.items.count) duplicate entries found."
            self.addActivity("Info", self.statusMessage)
        }
    }

    func quarantineLargeFiles() {
        quarantineManualFiles(selectedLargeFiles, categoryName: "Large file review", selectedIDs: \.selectedLargeFileIDs)
    }

    func quarantineDuplicates() {
        let selected = duplicateFiles.filter { selectedDuplicateIDs.contains($0.id) }
        let candidates = selected.map {
            manualCandidate(path: $0.path, name: $0.name, categoryName: "Duplicate file review", size: $0.sizeBytes, modified: $0.modifiedAt)
        }
        quarantineManualCandidates(candidates) { completedPaths in
            self.duplicateFiles.removeAll { completedPaths.contains($0.path) }
            self.selectedDuplicateIDs.removeAll()
        }
    }

    func restoreSelectedQuarantine() {
        guard !selectedQuarantineIDs.isEmpty else { return }
        runBusy("Restoring selected quarantine items...") {
            let ids = self.selectedQuarantineIDs
            let service = self.quarantineService
            let warnings = await Task.detached {
                service.restore(ids: ids)
            }.value
            warnings.forEach { self.addActivity("Warn", $0) }
            self.selectedQuarantineIDs.removeAll()
            self.refreshRecovery()
            self.statusMessage = "Restore operation complete."
            self.addActivity("Info", self.statusMessage)
        }
    }

    func purgeSelectedQuarantine() {
        guard !selectedQuarantineIDs.isEmpty else { return }
        runBusy("Purging selected quarantine items...") {
            let ids = self.selectedQuarantineIDs
            let service = self.quarantineService
            let warnings = await Task.detached {
                service.purge(ids: ids)
            }.value
            warnings.forEach { self.addActivity("Warn", $0) }
            self.selectedQuarantineIDs.removeAll()
            self.refreshRecovery()
            self.statusMessage = "Selected quarantine items purged."
            self.addActivity("Info", self.statusMessage)
        }
    }

    func refreshRecovery() {
        quarantineEntries = quarantineService.entries()
        receipts = receiptService.recent()
    }

    func clearActivity() {
        activityLog.removeAll()
        addActivity("Info", "Activity log cleared.")
    }

    private func quarantineManualFiles(_ files: [LargeFileInfo], categoryName: String, selectedIDs: ReferenceWritableKeyPath<MacAppState, Set<UUID>>) {
        let candidates = files.map {
            manualCandidate(path: $0.path, name: $0.name, categoryName: categoryName, size: $0.sizeBytes, modified: $0.modifiedAt)
        }
        quarantineManualCandidates(candidates) { completedPaths in
            self.largeFiles.removeAll { completedPaths.contains($0.path) }
            self[keyPath: selectedIDs].removeAll()
        }
    }

    private func quarantineManualCandidates(_ candidates: [CleanupCandidate], afterComplete: @escaping (Set<String>) -> Void) {
        guard !candidates.isEmpty else {
            statusMessage = "Select files first."
            return
        }

        runBusy("Moving selected files to quarantine...") {
            let service = self.quarantineService
            let result = await Task.detached {
                service.quarantine(candidates)
            }.value
            let completedPaths = Set(result.entries.map(\.originalPath))
            result.warnings.forEach { self.addActivity("Warn", $0) }
            afterComplete(completedPaths)
            self.refreshRecovery()
            self.statusMessage = "Quarantined \(result.entries.count) selected files."
            self.addActivity("Info", self.statusMessage)
        }
    }

    private func manualCandidate(path: String, name: String, categoryName: String, size: Int64, modified: Date?) -> CleanupCandidate {
        CleanupCandidate(
            categoryId: categoryName.replacingOccurrences(of: " ", with: "-").lowercased(),
            categoryName: categoryName,
            displayName: name,
            path: path,
            approvedRoot: URL(fileURLWithPath: path).deletingLastPathComponent().path,
            kind: .file,
            sizeBytes: size,
            lastModified: modified,
            risk: .high
        )
    }

    private func runBusy(_ message: String, operation: @escaping () async -> Void) {
        guard !isBusy else { return }
        isBusy = true
        statusMessage = message
        Task {
            await operation()
            isBusy = false
        }
    }

    private func addActivity(_ level: String, _ message: String) {
        activityLog.insert(ActivityLogEntry(timestamp: Date(), level: level, message: message), at: 0)
    }
}
