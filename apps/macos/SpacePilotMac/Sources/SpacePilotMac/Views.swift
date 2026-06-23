import SwiftUI

struct RootView: View {
    @EnvironmentObject private var state: MacAppState

    var body: some View {
        NavigationSplitView {
            List(MacSection.allCases, selection: $state.selectedSection) { section in
                Label(section.rawValue, systemImage: icon(for: section))
                    .tag(section)
            }
            .navigationSplitViewColumnWidth(min: 180, ideal: 210)
            .safeAreaInset(edge: .bottom) {
                VStack(alignment: .leading, spacing: 4) {
                    Text("SpacePilot")
                        .font(.headline)
                    Text("macOS cleanup toolkit")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding()
            }
        } detail: {
            VStack(spacing: 0) {
                HeaderView()
                Divider()
                Group {
                    switch state.selectedSection {
                    case .health:
                        HealthView()
                    case .cleaner:
                        CleanerView()
                    case .storage:
                        StorageView()
                    case .performance:
                        PerformanceView()
                    case .recovery:
                        RecoveryView()
                    case .settings:
                        SettingsView()
                    case .activity:
                        ActivityView()
                    }
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                Divider()
                FooterView()
            }
        }
    }

    private func icon(for section: MacSection) -> String {
        switch section {
        case .health: "gauge.with.dots.needle.67percent"
        case .cleaner: "sparkles"
        case .storage: "externaldrive"
        case .performance: "memorychip"
        case .recovery: "arrow.uturn.backward.circle"
        case .settings: "gearshape"
        case .activity: "list.bullet.rectangle"
        }
    }
}

struct HeaderView: View {
    @EnvironmentObject private var state: MacAppState

    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 4) {
                Text(state.selectedSection.rawValue)
                    .font(.largeTitle.weight(.semibold))
                Text(subtitle)
                    .foregroundStyle(.secondary)
            }
            Spacer()
            Button {
                state.scanCleanup()
            } label: {
                Label("Scan", systemImage: "magnifyingglass")
            }
            .keyboardShortcut("r", modifiers: [.command])
            .buttonStyle(.borderedProminent)
            .disabled(state.isBusy)
        }
        .padding()
    }

    private var subtitle: String {
        switch state.selectedSection {
        case .health: state.productSubtitle
        case .cleaner: "Review every cache, temp, and log candidate before quarantine."
        case .storage: "Find large files and verified duplicates without auto-deleting personal data."
        case .performance: "Inspect memory pressure, top apps, swap use, and safe performance actions."
        case .recovery: "Restore quarantined files or purge them to reclaim disk space."
        case .settings: "Tune safety defaults and protected file policies."
        case .activity: "Review scan, cleanup, restore, warning, and receipt events."
        }
    }
}

struct FooterView: View {
    @EnvironmentObject private var state: MacAppState

    var body: some View {
        HStack {
            Text(state.statusMessage)
                .foregroundStyle(.secondary)
                .lineLimit(1)
            Spacer()
            if state.isBusy {
                ProgressView()
                    .controlSize(.small)
                    .accessibilityLabel("Operation in progress")
            }
        }
        .padding(.horizontal)
        .padding(.vertical, 10)
    }
}

struct HealthView: View {
    @EnvironmentObject private var state: MacAppState

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                if state.preferences.isFirstRun {
                    FirstRunBanner()
                }
                HStack(spacing: 12) {
                    MetricCard(title: "Cleanup estimate", value: Formatters.bytes(state.totalScannedBytes), detail: state.summaryText)
                    MetricCard(title: "Selected", value: Formatters.bytes(state.selectedCleanupBytes), detail: state.selectedSummary)
                    MetricCard(title: "Quarantine", value: Formatters.bytes(state.quarantineBytes), detail: "\(state.quarantineEntries.count) recoverable items")
                }
                Panel("Recommended reclaim plan") {
                    VStack(alignment: .leading, spacing: 12) {
                        PlanRow(number: "1", title: "Scan safe Mac cleanup roots", text: "Start with temp files, user logs, and opt-in caches. Review before quarantine.")
                        PlanRow(number: "2", title: "Review large files", text: "Find space-heavy files in personal folders and quarantine only what you recognize.")
                        PlanRow(number: "3", title: "Verify duplicates", text: "Use SHA-256 matching before selecting redundant copies.")
                        PlanRow(number: "4", title: "Purge later", text: "Quarantine first; purge only when you are sure you do not need the files back.")
                    }
                }
                Panel("macOS safety boundaries") {
                    VStack(alignment: .leading, spacing: 8) {
                        Label("No broad deletion of Documents, Desktop, Downloads, or app containers.", systemImage: "checkmark.shield")
                        Label("No system cleanup outside approved user-owned roots.", systemImage: "checkmark.shield")
                        Label("Symbolic links are skipped to avoid unexpected traversal.", systemImage: "checkmark.shield")
                        Label("Personal-file cleanup requires explicit selection.", systemImage: "checkmark.shield")
                    }
                }
            }
            .padding()
        }
    }
}

struct FirstRunBanner: View {
    @EnvironmentObject private var state: MacAppState

    var body: some View {
        HStack(alignment: .top) {
            Image(systemName: "checkmark.shield")
                .font(.title2)
                .foregroundStyle(.blue)
            VStack(alignment: .leading, spacing: 4) {
                Text("Safety-first cleanup")
                    .font(.headline)
                Text("SpacePilot scans first, shows every candidate, skips protected paths, and moves files to quarantine by default.")
                    .foregroundStyle(.secondary)
            }
            Spacer()
            Button("Got it") {
                state.dismissFirstRun()
            }
        }
        .padding()
        .background(.blue.opacity(0.08), in: RoundedRectangle(cornerRadius: 8))
        .overlay(RoundedRectangle(cornerRadius: 8).stroke(.blue.opacity(0.25)))
    }
}

struct CleanerView: View {
    @EnvironmentObject private var state: MacAppState
    @State private var confirmingClean = false

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                TextField("Search candidates", text: $state.searchText)
                    .textFieldStyle(.roundedBorder)
                    .accessibilityLabel("Search cleanup candidates")
                Picker("Risk", selection: $state.riskFilter) {
                    Text("All risks").tag("All risks")
                    ForEach(RiskLevel.allCases) { risk in
                        Text(risk.rawValue).tag(risk.rawValue)
                    }
                }
                .frame(width: 160)
                Button("Select visible") {
                    state.selectAllFiltered()
                }
                Button("Clear") {
                    state.clearSelection()
                }
                Button("Quarantine selected") {
                    confirmingClean = true
                }
                .buttonStyle(.borderedProminent)
                .disabled(state.selectedCandidateIDs.isEmpty || state.isBusy)
            }
            .padding()
            List(state.filteredCandidates) { candidate in
                CandidateRow(candidate: candidate, isSelected: state.selectedCandidateIDs.contains(candidate.id)) {
                    state.toggleCandidate(candidate)
                }
            }
            .overlay {
                if state.filteredCandidates.isEmpty {
                    EmptyState(
                        title: "No cleanup items to show",
                        message: "Run a scan or adjust filters to review macOS cleanup candidates.",
                        systemImage: "sparkles"
                    )
                }
            }
        }
        .confirmationDialog("Move selected items to quarantine?", isPresented: $confirmingClean) {
            Button("Quarantine selected", role: .destructive) {
                state.cleanSelected()
            }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("Selected files move into SpacePilot quarantine so they can be restored later.")
        }
    }
}

struct CandidateRow: View {
    let candidate: CleanupCandidate
    let isSelected: Bool
    let toggle: () -> Void

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            Button(action: toggle) {
                Image(systemName: isSelected ? "checkmark.square.fill" : "square")
            }
            .buttonStyle(.plain)
            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text(candidate.displayName)
                        .font(.headline)
                    RiskBadge(risk: candidate.risk)
                    Spacer()
                    Text(Formatters.bytes(candidate.sizeBytes))
                        .font(.headline)
                }
                Text(candidate.categoryName)
                    .foregroundStyle(.secondary)
                Text(candidate.path)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(2)
            }
        }
        .padding(.vertical, 4)
    }
}

struct StorageView: View {
    @EnvironmentObject private var state: MacAppState

    var body: some View {
        TabView {
            LargeFilesView()
                .tabItem { Text("Large files") }
            DuplicatesView()
                .tabItem { Text("Duplicates") }
        }
        .padding()
    }
}

struct LargeFilesView: View {
    @EnvironmentObject private var state: MacAppState
    @State private var confirming = false

    var body: some View {
        VStack(spacing: 12) {
            HStack {
                Text("Minimum MB")
                TextField("Minimum MB", value: $state.preferences.largeFileMinimumMB, format: .number)
                    .frame(width: 90)
                    .textFieldStyle(.roundedBorder)
                    .onSubmit { state.savePreferences() }
                Button("Scan large files") {
                    state.savePreferences()
                    state.scanLargeFiles()
                }
                Button("Quarantine selected") {
                    confirming = true
                }
                .buttonStyle(.borderedProminent)
                .disabled(state.selectedLargeFileIDs.isEmpty)
                Spacer()
            }
            List(state.largeFiles) { file in
                SelectableFileRow(
                    title: file.name,
                    subtitle: file.recommendation,
                    path: file.path,
                    size: file.sizeBytes,
                    isSelected: state.selectedLargeFileIDs.contains(file.id)
                ) {
                    if state.selectedLargeFileIDs.contains(file.id) {
                        state.selectedLargeFileIDs.remove(file.id)
                    } else {
                        state.selectedLargeFileIDs.insert(file.id)
                    }
                }
            }
            .overlay {
                if state.largeFiles.isEmpty {
                    EmptyState(title: "No large-file scan yet", message: "Run a scan to inspect large personal files before deciding what to quarantine.", systemImage: "externaldrive")
                }
            }
        }
        .confirmationDialog("Quarantine selected large files?", isPresented: $confirming) {
            Button("Quarantine selected", role: .destructive) {
                state.quarantineLargeFiles()
            }
            Button("Cancel", role: .cancel) {}
        }
    }
}

struct DuplicatesView: View {
    @EnvironmentObject private var state: MacAppState
    @State private var confirming = false

    var body: some View {
        VStack(spacing: 12) {
            HStack {
                Text("Minimum MB")
                TextField("Minimum MB", value: $state.preferences.duplicateMinimumMB, format: .number)
                    .frame(width: 90)
                    .textFieldStyle(.roundedBorder)
                    .onSubmit { state.savePreferences() }
                Button("Find duplicates") {
                    state.savePreferences()
                    state.scanDuplicates()
                }
                Button("Quarantine selected") {
                    confirming = true
                }
                .buttonStyle(.borderedProminent)
                .disabled(state.selectedDuplicateIDs.isEmpty)
                Spacer()
            }
            List(state.duplicateFiles) { file in
                SelectableFileRow(
                    title: "\(file.groupId): \(file.name)",
                    subtitle: file.isRecommendedForCleanup ? "Recommended redundant copy" : "Keep one copy from each group",
                    path: file.path,
                    size: file.sizeBytes,
                    isSelected: state.selectedDuplicateIDs.contains(file.id)
                ) {
                    if state.selectedDuplicateIDs.contains(file.id) {
                        state.selectedDuplicateIDs.remove(file.id)
                    } else {
                        state.selectedDuplicateIDs.insert(file.id)
                    }
                }
            }
            .overlay {
                if state.duplicateFiles.isEmpty {
                    EmptyState(title: "No duplicate scan yet", message: "SpacePilot hashes same-size files before showing duplicate groups.", systemImage: "doc.on.doc")
                }
            }
        }
        .confirmationDialog("Quarantine selected duplicates?", isPresented: $confirming) {
            Button("Quarantine selected", role: .destructive) {
                state.quarantineDuplicates()
            }
            Button("Cancel", role: .cancel) {}
        }
    }
}

struct PerformanceView: View {
    @EnvironmentObject private var state: MacAppState

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                HStack {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("RAM Assist")
                            .font(.title3.weight(.semibold))
                        Text(state.performanceSummary)
                            .foregroundStyle(.secondary)
                    }
                    Spacer()
                    Button {
                        state.refreshPerformance()
                    } label: {
                        Label("Refresh", systemImage: "arrow.clockwise")
                    }
                    Button {
                        state.openActivityMonitor()
                    } label: {
                        Label("Activity Monitor", systemImage: "waveform.path.ecg")
                    }
                    Button {
                        state.openLoginItems()
                    } label: {
                        Label("Login Items", systemImage: "person.crop.circle.badge.gearshape")
                    }
                }

                HStack(spacing: 12) {
                    MetricCard(
                        title: "Memory used",
                        value: state.performanceSnapshot.map { Formatters.bytes($0.usedMemoryBytes) } ?? "Not sampled",
                        detail: state.memoryUsagePercentText
                    )
                    MetricCard(
                        title: "Available RAM",
                        value: state.performanceSnapshot.map { Formatters.bytes($0.availableMemoryBytes) } ?? "Not sampled",
                        detail: state.performanceSnapshot.map { "of \(Formatters.bytes($0.totalMemoryBytes)) total" } ?? "Refresh RAM Assist"
                    )
                    MetricCard(
                        title: "Pressure",
                        value: state.performanceSnapshot?.memoryPressure ?? "Not sampled",
                        detail: state.swapSummary
                    )
                    MetricCard(
                        title: "Uptime",
                        value: state.performanceSnapshot?.uptimeText ?? "Not sampled",
                        detail: state.performanceSnapshot.map { "\($0.processCount) processes sampled" } ?? "Process list pending"
                    )
                }

                HStack(alignment: .top, spacing: 16) {
                    Panel("Top memory processes") {
                        VStack(alignment: .leading, spacing: 10) {
                            Text(state.performanceProcessSummary)
                                .foregroundStyle(.secondary)
                            ForEach(state.memoryProcesses) { process in
                                ProcessMemoryRow(process: process)
                            }
                            if state.memoryProcesses.isEmpty {
                                EmptyState(title: "No process sample yet", message: "Refresh RAM Assist to inspect resident memory by process.", systemImage: "memorychip")
                            }
                        }
                    }

                    Panel("Strong improvements") {
                        VStack(alignment: .leading, spacing: 12) {
                            ForEach(state.performanceRecommendations) { item in
                                PerformanceRecommendationRow(item: item)
                            }
                            if state.performanceRecommendations.isEmpty {
                                EmptyState(title: "No recommendations yet", message: "Refresh RAM Assist to build safe next steps.", systemImage: "checklist")
                            }
                        }
                    }
                }

                Panel("Safety boundary") {
                    VStack(alignment: .leading, spacing: 8) {
                        Label("SpacePilot does not purge RAM or terminate apps automatically.", systemImage: "checkmark.shield")
                        Label("macOS reuses available memory for cache; force-clearing it can make apps slower.", systemImage: "checkmark.shield")
                        Label("Use Activity Monitor when you intentionally want to quit a memory-heavy app.", systemImage: "checkmark.shield")
                    }
                    .foregroundStyle(.secondary)
                }
            }
            .padding()
        }
    }
}

struct ProcessMemoryRow: View {
    let process: ProcessMemoryInfo

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack(alignment: .firstTextBaseline) {
                Text(process.name)
                    .font(.headline)
                Text("PID \(process.processId)")
                    .foregroundStyle(.secondary)
                Spacer()
                Text(Formatters.bytes(process.residentMemoryBytes))
                    .font(.headline)
            }
            Text(process.recommendation)
                .foregroundStyle(.secondary)
            Text(process.commandPath)
                .font(.caption)
                .foregroundStyle(.secondary)
                .lineLimit(1)
        }
        .padding(.vertical, 6)
        .overlay(alignment: .bottom) {
            Divider()
        }
    }
}

struct PerformanceRecommendationRow: View {
    let item: PerformanceRecommendation

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack {
                Text(item.area)
                    .font(.headline)
                Spacer()
                Text(item.status)
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(.blue)
            }
            Text(item.recommendation)
                .foregroundStyle(.secondary)
            Text(item.impact)
                .font(.caption)
                .foregroundStyle(.secondary)
            Text(item.action)
                .font(.caption.weight(.semibold))
        }
        .padding(.vertical, 6)
        .overlay(alignment: .bottom) {
            Divider()
        }
    }
}

struct RecoveryView: View {
    @EnvironmentObject private var state: MacAppState
    @State private var confirmingPurge = false

    var body: some View {
        TabView {
            VStack(spacing: 12) {
                HStack {
                    Button("Refresh") { state.refreshRecovery() }
                    Button("Restore selected") { state.restoreSelectedQuarantine() }
                        .disabled(state.selectedQuarantineIDs.isEmpty)
                    Button("Purge selected") { confirmingPurge = true }
                        .buttonStyle(.borderedProminent)
                        .disabled(state.selectedQuarantineIDs.isEmpty)
                    Spacer()
                    Text("\(state.quarantineEntries.count) items, \(Formatters.bytes(state.quarantineBytes))")
                        .foregroundStyle(.secondary)
                }
                List(state.quarantineEntries) { entry in
                    HStack(alignment: .top, spacing: 12) {
                        Button {
                            if state.selectedQuarantineIDs.contains(entry.id) {
                                state.selectedQuarantineIDs.remove(entry.id)
                            } else {
                                state.selectedQuarantineIDs.insert(entry.id)
                            }
                        } label: {
                            Image(systemName: state.selectedQuarantineIDs.contains(entry.id) ? "checkmark.square.fill" : "square")
                        }
                        .buttonStyle(.plain)
                        VStack(alignment: .leading, spacing: 4) {
                            HStack {
                                Text(entry.displayName)
                                    .font(.headline)
                                Spacer()
                                Text(Formatters.bytes(entry.sizeBytes))
                            }
                            Text("\(entry.categoryName) - \(Formatters.date(entry.quarantinedAt))")
                                .foregroundStyle(.secondary)
                            Text(entry.originalPath)
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                    }
                }
                .overlay {
                    if state.quarantineEntries.isEmpty {
                        EmptyState(title: "Quarantine is empty", message: "Cleaned files appear here before permanent purge.", systemImage: "arrow.uturn.backward.circle")
                    }
                }
            }
            .padding()
            .tabItem { Text("Quarantine") }

            List(state.receipts) { receipt in
                VStack(alignment: .leading, spacing: 4) {
                    HStack {
                        Text(receipt.mode)
                            .font(.headline)
                        Spacer()
                        Text(Formatters.date(receipt.timestamp))
                            .foregroundStyle(.secondary)
                    }
                    Text("\(receipt.completedCount) of \(receipt.requestedCount) completed, \(Formatters.bytes(receipt.completedBytes))")
                        .foregroundStyle(.secondary)
                    if !receipt.warnings.isEmpty {
                        Text("\(receipt.warnings.count) warnings")
                            .foregroundStyle(.orange)
                    }
                }
            }
            .padding()
            .tabItem { Text("Receipts") }
        }
        .confirmationDialog("Permanently purge selected quarantine items?", isPresented: $confirmingPurge) {
            Button("Purge selected", role: .destructive) {
                state.purgeSelectedQuarantine()
            }
            Button("Cancel", role: .cancel) {}
        }
    }
}

struct SettingsView: View {
    @EnvironmentObject private var state: MacAppState

    var body: some View {
        Form {
            Section("Cleanup safeguards") {
                Toggle("Ask before cleanup", isOn: $state.preferences.confirmBeforeCleanup)
                Toggle("Move cleanup items to quarantine", isOn: $state.preferences.useQuarantine)
                Stepper("Quarantine retention: \(state.preferences.quarantineRetentionDays) days", value: $state.preferences.quarantineRetentionDays, in: 1...90)
            }
            Section("Storage thresholds") {
                Stepper("Large-file scan: \(state.preferences.largeFileMinimumMB) MB", value: $state.preferences.largeFileMinimumMB, in: 25...10_000, step: 25)
                Stepper("Duplicate scan: \(state.preferences.duplicateMinimumMB) MB", value: $state.preferences.duplicateMinimumMB, in: 5...10_000, step: 5)
            }
            Section("Protected extensions") {
                Text(state.preferences.protectedExtensions.joined(separator: ", "))
                    .foregroundStyle(.secondary)
                Text("Protected extensions are skipped by cleanup and storage review.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Section("Local data") {
                Text(MacAppPaths.applicationSupportRoot.path)
                    .textSelection(.enabled)
                    .foregroundStyle(.secondary)
            }
            Button("Save settings") {
                state.savePreferences()
            }
        }
        .formStyle(.grouped)
        .padding()
    }
}

struct ActivityView: View {
    @EnvironmentObject private var state: MacAppState

    var body: some View {
        VStack {
            HStack {
                Text("Activity log")
                    .font(.headline)
                Spacer()
                Button("Clear log") {
                    state.clearActivity()
                }
            }
            .padding()
            List(state.activityLog) { entry in
                HStack(alignment: .top) {
                    Text(Formatters.date(entry.timestamp))
                        .frame(width: 160, alignment: .leading)
                        .foregroundStyle(.secondary)
                    Text(entry.level)
                        .frame(width: 70, alignment: .leading)
                        .font(.headline)
                    Text(entry.message)
                }
            }
        }
    }
}

struct MetricCard: View {
    let title: String
    let value: String
    let detail: String

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(title)
                .foregroundStyle(.secondary)
            Text(value)
                .font(.title.weight(.semibold))
            Text(detail)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding()
        .background(.background, in: RoundedRectangle(cornerRadius: 8))
        .overlay(RoundedRectangle(cornerRadius: 8).stroke(.quaternary))
    }
}

struct Panel<Content: View>: View {
    let title: String
    @ViewBuilder let content: Content

    init(_ title: String, @ViewBuilder content: () -> Content) {
        self.title = title
        self.content = content()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(title)
                .font(.title3.weight(.semibold))
            content
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding()
        .background(.background, in: RoundedRectangle(cornerRadius: 8))
        .overlay(RoundedRectangle(cornerRadius: 8).stroke(.quaternary))
    }
}

struct PlanRow: View {
    let number: String
    let title: String
    let text: String

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            Text(number)
                .font(.headline)
                .foregroundStyle(.blue)
                .frame(width: 26)
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.headline)
                Text(text)
                    .foregroundStyle(.secondary)
            }
        }
    }
}

struct RiskBadge: View {
    let risk: RiskLevel

    var body: some View {
        Text(risk.rawValue)
            .font(.caption.weight(.semibold))
            .padding(.horizontal, 7)
            .padding(.vertical, 2)
            .background(color.opacity(0.18), in: Capsule())
            .foregroundStyle(color)
    }

    private var color: Color {
        switch risk {
        case .low: .green
        case .medium: .orange
        case .high: .red
        }
    }
}

struct SelectableFileRow: View {
    let title: String
    let subtitle: String
    let path: String
    let size: Int64
    let isSelected: Bool
    let toggle: () -> Void

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            Button(action: toggle) {
                Image(systemName: isSelected ? "checkmark.square.fill" : "square")
            }
            .buttonStyle(.plain)
            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text(title)
                        .font(.headline)
                    Spacer()
                    Text(Formatters.bytes(size))
                        .font(.headline)
                }
                Text(subtitle)
                    .foregroundStyle(.secondary)
                Text(path)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(2)
            }
        }
        .padding(.vertical, 4)
    }
}

struct EmptyState: View {
    let title: String
    let message: String
    let systemImage: String

    var body: some View {
        VStack(spacing: 10) {
            Image(systemName: systemImage)
                .font(.largeTitle)
                .foregroundStyle(.secondary)
            Text(title)
                .font(.title3.weight(.semibold))
            Text(message)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
                .frame(maxWidth: 420)
        }
        .padding()
    }
}
