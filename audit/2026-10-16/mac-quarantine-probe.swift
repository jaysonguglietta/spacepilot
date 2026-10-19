import Foundation

@main enum AuditProbe {
 static func main() throws {
   let fm = FileManager.default
   let base = URL(fileURLWithPath: "/private/tmp", isDirectory: true).appendingPathComponent("spacepilot-audit-proof-" + UUID().uuidString, isDirectory: true)
   try fm.createDirectory(at: base, withIntermediateDirectories: false, attributes: [.posixPermissions: 0o700])
   print("fresh_fixture_directory=\(base.path)")
   func candidate(_ source: URL, _ approved: URL) -> CleanupCandidate {
     CleanupCandidate(categoryId: "probe", categoryName: "Probe", displayName: source.lastPathComponent, path: source.path, approvedRoot: approved.path, kind: .file, sizeBytes: 4, lastModified: Date(), risk: .low)
   }
   func writeManifest(_ entry: QuarantineEntry, at root: URL) throws {
     let encoder = JSONEncoder(); encoder.dateEncodingStrategy = .iso8601
     try encoder.encode([entry]).write(to: root.appendingPathComponent("manifest.json"))
   }
   // All fixtures are confined to the supplied audit directory.
   let q1=base.appendingPathComponent("q-forged"), outside=base.appendingPathComponent("unrelated")
   try fm.createDirectory(at:q1,withIntermediateDirectories:true)
   try fm.createDirectory(at:outside,withIntermediateDirectories:true)
   let victim=outside.appendingPathComponent("must-survive.txt")
   try Data("keep".utf8).write(to:victim)
   let forged=QuarantineEntry(id:"forged",originalPath:base.appendingPathComponent("fake-original").path,payloadPath:victim.path,displayName:"normal cache",categoryName:"User temp",kind:.file,sizeBytes:4,quarantinedAt:Date(),originalLastModified:nil)
   try writeManifest(forged,at:q1)
   let s1=QuarantineService(root:q1)
   print("manifest_visible_entries=\(s1.entries().count)")
   let purgeWarnings=s1.purge(ids:["forged"])
   print("forged_manifest_deleted_unrelated_parent=\(!fm.fileExists(atPath:outside.path)); warnings=\(purgeWarnings.count)")

   let approved=base.appendingPathComponent("approved"), nested=approved.appendingPathComponent("nested"), other=base.appendingPathComponent("outside-root"), q2=base.appendingPathComponent("q-symlink")
   try fm.createDirectory(at:nested,withIntermediateDirectories:true)
   try fm.createDirectory(at:other,withIntermediateDirectories:true)
   let selected=nested.appendingPathComponent("cache.tmp")
   try Data("safe".utf8).write(to:selected)
   let c=candidate(selected,approved)
   try fm.moveItem(at:nested,to:approved.appendingPathComponent("nested-before-swap"))
   let external=other.appendingPathComponent("cache.tmp")
   try Data("keep".utf8).write(to:external)
   try fm.createSymbolicLink(at:nested,withDestinationURL:other)
   let result=QuarantineService(root:q2).quarantine([c])
   print("ancestor_symlink_accepted=\(result.entries.count == 1); outside_file_moved=\(!fm.fileExists(atPath:external.path)); warnings=\(result.warnings.count)")

   let a3=base.appendingPathComponent("approved-save-failure"), q3=base.appendingPathComponent("q-save-failure")
   try fm.createDirectory(at:a3,withIntermediateDirectories:true)
   try fm.createDirectory(at:q3.appendingPathComponent("manifest.json"),withIntermediateDirectories:true)
   let file3=a3.appendingPathComponent("file.tmp")
   try Data("keep".utf8).write(to:file3)
   let s3=QuarantineService(root:q3), r3=s3.quarantine([candidate(file3,a3)])
   print("failed_manifest_reported_success=\(r3.entries.count == 1); warnings=\(r3.warnings.count); recovery_entries=\(s3.entries().count); original_missing=\(!fm.fileExists(atPath:file3.path))")
 }
}
