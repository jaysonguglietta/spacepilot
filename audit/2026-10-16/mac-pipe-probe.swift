import Foundation
@main enum PipeProbe {
 static func main() throws {
   // Exact implementation pattern from PerformanceAssistService.run, using controlled local input.
   let process = Process()
   process.executableURL = URL(fileURLWithPath:"/bin/cat")
   process.arguments = [CommandLine.arguments[1]]
   let output = Pipe()
   process.standardOutput = output
   process.standardError = Pipe()
   try process.run()
   FileHandle.standardOutput.write(Data("helper_started; waiting_before_drain\n".utf8))
   process.waitUntilExit()
   let data = output.fileHandleForReading.readDataToEndOfFile()
   FileHandle.standardOutput.write(Data("completed_bytes=\(data.count)\n".utf8))
 }
}
