import Foundation
import CryptoKit

@main enum ExtendedProbe {
 static func main() throws {
   let fm = FileManager.default
   let base = URL(fileURLWithPath: "/private/tmp", isDirectory: true).appendingPathComponent("spacepilot-audit-extended-" + UUID().uuidString, isDirectory: true)
   try fm.createDirectory(at: base, withIntermediateDirectories: false, attributes: [.posixPermissions: 0o700])
   print("fresh_fixture_directory=\(base.path)")
   let policy = ProtectionPolicy(protectedExtensions: [".pdf"], protectedPaths: [])
   func rule(_ root: URL, directories: Bool) -> CleanupRule {
     CleanupRule(id:"probe",name:"Probe",description:"Temporary fixture",defaultSelected:true,risk:.low,minimumAgeDays:0,locations:[CleanupLocation(rootPath:root.path,includeSubdirectories:true,includeFiles:true,includeDirectories:directories)])
   }
   let dirRoot=base.appendingPathComponent("directory-rule"), folder=dirRoot.appendingPathComponent("cache-folder")
   try fm.createDirectory(at:folder,withIntermediateDirectories:true)
   let protected=folder.appendingPathComponent("keep.pdf")
   try Data("protected-document".utf8).write(to:protected)
   try Data("cache-data".utf8).write(to:folder.appendingPathComponent("cache.tmp"))
   let cleaner=MacCleanerService()
   let scan=cleaner.scan(rules:[rule(dirRoot,directories:true)],policy:policy)
   print("directory_rule_directory_candidates=\(scan.candidates.filter { $0.kind == .directory }.count); protected_leaf_candidates=\(scan.candidates.filter { $0.path == protected.path }.count)")
   if let dirCandidate=scan.candidates.first(where: { $0.path == folder.path }) {
     let q=QuarantineService(root:base.appendingPathComponent("q-directory-rule"))
     let r=q.quarantine([dirCandidate])
     let payload=r.entries.first.map { URL(fileURLWithPath:$0.payloadPath).appendingPathComponent("keep.pdf") }
     print("directory_rule_carried_protected_descendant=\(payload.map { fm.fileExists(atPath:$0.path) } ?? false); warnings=\(r.warnings.count)")
   } else {
     // The current APFS fixture reports zero allocated directory bytes, so scanner output excludes it.
     // Exercise the directory-candidate service contract explicitly without claiming default reachability.
     let modeled=CleanupCandidate(categoryId:"probe",categoryName:"Probe",displayName:folder.lastPathComponent,path:folder.path,approvedRoot:dirRoot.path,kind:.directory,sizeBytes:18,lastModified:Date(),risk:.low)
     let r=QuarantineService(root:base.appendingPathComponent("q-modeled-directory")).quarantine([modeled])
     let payload=r.entries.first.map { URL(fileURLWithPath:$0.payloadPath).appendingPathComponent("keep.pdf") }
     print("modeled_directory_policy_protected=\(policy.isProtected(folder.path)); descendant_policy_protected=\(policy.isProtected(protected.path)); modeled_directory_carried_protected_descendant=\(payload.map { fm.fileExists(atPath:$0.path) } ?? false); warnings=\(r.warnings.count)")
   }

   let swapRoot=base.appendingPathComponent("file-directory-swap")
   try fm.createDirectory(at:swapRoot,withIntermediateDirectories:true)
   let selected=swapRoot.appendingPathComponent("cache.tmp")
   try Data("cache-data".utf8).write(to:selected)
   let before=cleaner.scan(rules:[rule(swapRoot,directories:false)],policy:policy)
   guard let fileCandidate=before.candidates.first(where: { $0.path == selected.path }) else { fatalError("fixture scan did not produce file candidate") }
   try fm.removeItem(at:selected)
   try fm.createDirectory(at:selected,withIntermediateDirectories:true)
   try Data("protected-document".utf8).write(to:selected.appendingPathComponent("keep.pdf"))
   let r2=QuarantineService(root:base.appendingPathComponent("q-file-directory-swap")).quarantine([fileCandidate])
   var isDir: ObjCBool=false
   let payloadExists=r2.entries.first.map { fm.fileExists(atPath:$0.payloadPath,isDirectory:&isDir) } ?? false
   let protectedMoved=r2.entries.first.map { fm.fileExists(atPath:URL(fileURLWithPath:$0.payloadPath).appendingPathComponent("keep.pdf").path) } ?? false
   print("file_to_directory_swap_accepted=\(r2.entries.count == 1); payload_is_directory=\(payloadExists && isDir.boolValue); stored_kind=\(r2.entries.first?.kind.rawValue ?? "none"); protected_descendant_moved=\(protectedMoved); warnings=\(r2.warnings.count)")

   let duplicates=base.appendingPathComponent("stale-duplicate")
   try fm.createDirectory(at:duplicates,withIntermediateDirectories:true)
   let keeper=duplicates.appendingPathComponent("a.tmp"), selectedDuplicate=duplicates.appendingPathComponent("b.tmp")
   let original=Data("identical-original".utf8)
   try original.write(to:keeper); try original.write(to:selectedDuplicate)
   let originalHash=SHA256.hash(data:original).map { String(format:"%02x",$0) }.joined()
   // A synthetic scan snapshot uses the exact DuplicateFileInfo model and manualCandidate conversion.
   let duplicate=DuplicateFileInfo(groupId:"Duplicate set 1",path:selectedDuplicate.path,name:selectedDuplicate.lastPathComponent,directory:duplicates.path,sizeBytes:Int64(original.count),modifiedAt:Date(),sha256:originalHash,isRecommendedForCleanup:true)
   let manual=CleanupCandidate(categoryId:"duplicate-file-review",categoryName:"Duplicate file review",displayName:duplicate.name,path:duplicate.path,approvedRoot:URL(fileURLWithPath:duplicate.path).deletingLastPathComponent().path,kind:.file,sizeBytes:duplicate.sizeBytes,lastModified:duplicate.modifiedAt,risk:.high)
   let unique=Data("now-unique-user-work".utf8)
   try unique.write(to:selectedDuplicate)
   let changedHash=SHA256.hash(data:unique).map { String(format:"%02x",$0) }.joined()
   let r3=QuarantineService(root:base.appendingPathComponent("q-stale-duplicate")).quarantine([manual])
   let uniqueMoved=try r3.entries.first.map { try Data(contentsOf:URL(fileURLWithPath:$0.payloadPath)) == unique } ?? false
   print("duplicate_hash_changed=\(changedHash != duplicate.sha256); stale_duplicate_quarantined=\(r3.entries.count == 1); unique_content_moved=\(uniqueMoved); warnings=\(r3.warnings.count)")
 }
}
