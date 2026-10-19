import Foundation

@main
struct FeatureFixtureCheck {
    static func main() throws {
        let fixtureRoot = URL(fileURLWithPath: "/private/tmp/spacepilot-feature-fixture-\(UUID().uuidString)", isDirectory: true)
        let home = fixtureRoot.appendingPathComponent("home", isDirectory: true)
        let diagnostics = home.appendingPathComponent("Library/Logs/DiagnosticReports", isDirectory: true)
        let swiftPM = home.appendingPathComponent("Library/Caches/org.swift.swiftpm", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: fixtureRoot) }
        try FileManager.default.createDirectory(at: diagnostics, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: swiftPM, withIntermediateDirectories: true)
        let log = diagnostics.appendingPathComponent("fixture.log")
        let cache = swiftPM.appendingPathComponent("fixture.cache")
        try Data(repeating: 65, count: 4096).write(to: log)
        try Data(repeating: 66, count: 4096).write(to: cache)
        for url in [log, cache] {
            try FileManager.default.setAttributes([.modificationDate: Date(timeIntervalSinceNow: -30 * 86_400)], ofItemAtPath: url.path)
        }
        let rules = MacCleanupRuleCatalog.defaultRules(home: home, temporaryDirectory: fixtureRoot.appendingPathComponent("missing-temp").path)
        let scan = MacCleanerService().scan(rules: rules, policy: ProtectionPolicy(protectedExtensions: [], protectedPaths: []))
        let logCount = scan.candidates.filter { $0.path == log.path }.count
        let cacheCount = scan.candidates.filter { $0.path == cache.path }.count
        print("fixture-only scan: log occurrences=\(logCount); SwiftPM occurrences=\(cacheCount); total candidates=\(scan.candidates.count)")
        precondition(logCount == 2 && cacheCount == 2)
        let invalidSample = PerformanceAssistService.parseVMStat("", physicalMemory: 16 * 1_073_741_824)
        let pressure = PerformanceAssistService.classifyMemoryPressure(invalidSample.usagePercent)
        print("empty vm_stat: available=\(invalidSample.availableBytes); usage=\(invalidSample.usagePercent); pressure=\(pressure)")
        precondition(invalidSample.availableBytes == 0 && invalidSample.usagePercent == 100 && pressure == "Critical")
        let overflowCheck = Int64(Int.max).multipliedReportingOverflow(by: 1_048_576)
        print("max Int threshold multiplication overflows=\(overflowCheck.overflow)")
        precondition(overflowCheck.overflow)
    }
}
