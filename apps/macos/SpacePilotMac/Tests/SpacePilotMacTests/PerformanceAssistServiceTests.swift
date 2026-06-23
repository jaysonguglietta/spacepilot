import XCTest
@testable import SpacePilotMac

final class PerformanceAssistServiceTests: XCTestCase {
    func testClassifiesMemoryPressureThresholds() {
        XCTAssertEqual(PerformanceAssistService.classifyMemoryPressure(42), "Good")
        XCTAssertEqual(PerformanceAssistService.classifyMemoryPressure(70), "Elevated")
        XCTAssertEqual(PerformanceAssistService.classifyMemoryPressure(80), "High")
        XCTAssertEqual(PerformanceAssistService.classifyMemoryPressure(90), "Critical")
    }

    func testParsesVMStatAvailability() {
        let output = """
        Mach Virtual Memory Statistics: (page size of 4096 bytes)
        Pages free:                               1000.
        Pages active:                             4000.
        Pages inactive:                           2000.
        Pages speculative:                         500.
        Pages wired down:                         1000.
        Pages occupied by compressor:              200.
        """

        let result = PerformanceAssistService.parseVMStat(output, physicalMemory: 34_359_738_368)

        XCTAssertEqual(result.availableBytes, 14_336_000)
        XCTAssertGreaterThan(result.usagePercent, 99)
    }

    func testParsesProcessListByResidentMemory() {
        let output = """
          101  204800 /Applications/Safari.app/Contents/MacOS/Safari
          202 1048576 /Applications/Xcode.app/Contents/MacOS/Xcode
          bad line
        """

        let processes = PerformanceAssistService.parseProcessList(output)

        XCTAssertEqual(processes.count, 2)
        XCTAssertEqual(processes[0].processId, 202)
        XCTAssertEqual(processes[0].name, "Xcode")
        XCTAssertEqual(processes[0].residentMemoryBytes, 1_073_741_824)
    }
}
