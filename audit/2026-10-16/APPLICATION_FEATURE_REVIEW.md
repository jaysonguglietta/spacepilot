# SpacePilot application feature review

Review date: 2026-10-16. Audited commit: `943a196a12a879a0ca2d9b9e97985fe33b3a5697`. Application source is unchanged. File references below are repository-relative at that commit. See [the security audit](SECURITY_APPLICATION_REVIEW.md) for the consolidated security assessment, reproduction evidence and verification limits.

## Scope and evidence limits

- Reviewed the README, product brief, user guide, architecture, safety model, quality checklist, both platform READMEs and QA matrices, both complete main views/state files, selected models/view models and the services that implement storage, browser targets, preferences, performance and reclaim plans.
- Windows WPF runtime and accessibility have not been exercised locally. macOS SwiftPM/UI launch was blocked by missing host `SwiftUIMacros`; the non-UI core validator passed. Do not represent layout/screen-reader observations below as a completed manual QA or WCAG audit.
- Confirmed below means the control flow or data handling is established by source; it does not imply a production exploit was executed.
- A separately compiled non-UI harness using the unchanged Mac Models/Formatters/Services sources verified fixture-only root duplication and empty-vm_stat behavior. Command and outcome are recorded at the end; it made and removed only its own `/private/tmp` fixture.
- Product priorities: P1 = fix before public release because data safety/recoverability or core workflow is affected; P2 = core feature correctness/reliability/accessibility; P3 = polish/maintainability. These are separate from security severities.

## Product brief reconstructed from implementation

Target users: local Windows/macOS users with disk pressure; power users wanting item-level review and undo; Windows PC maintainers wanting software/startup inventory and WinGet package operations.

Problem: identify cleanup/storage opportunities and practical performance actions while avoiding opaque registry cleaners, process killing and unreviewed personal-file deletion.

Actual workflows:

1. Launch and load preferences/recovery. Windows additionally purges expired quarantine automatically, loads OS inventories, browser profiles, performance and reminder state. Mac starts a performance refresh. Health contains a dismissible first-run note.
2. Scan built-in roots. Windows scans every rule and chooses initial item selections from category preferences; category checkboxes also bulk-toggle existing candidates. Mac scans every rule and auto-selects candidates from default-selected low-risk rules. Neither platform uses category selection to limit the actual filesystem scan.
3. Review Cleaner. Windows supports search/category/risk filters, sortable WPF grids, per-item/category/all/visible selection. Mac supports search/risk and per-item/visible/clear selection. Confirm and execute. Windows uses the global quarantine/permanent-delete mode; Mac always quarantines. Both create normal-cleaner receipts.
4. Storage. Windows storage map summarizes personal-folder subdirectories; both platforms scan personal roots for large files and hash duplicates. Large files start unselected. Duplicates auto-select recommended redundant copies on both platforms. Windows sends manual files through the general cleanup pipeline; Mac uses a separate quarantine path without receipts.
5. Recovery. Both select, restore or purge quarantined payloads, refusing to overwrite an occupied original path. Windows has selected/all purge and latest-receipt export. Mac has selected purge and a summary-only receipt list. There is no guided restore-conflict resolution.
6. Windows-only maintenance: software search + Apps & Features handoff; WinGet check/update/export/import; registry/folder/scheduled-task startup inventory + Windows Startup Settings; browser profile/data-type controls; settings audit, restore-point request and weekly launch reminder.
7. Performance. Both display one sampled snapshot, top 25 processes and advisory text, plus working OS handoff buttons. Advice rows themselves are text, not dead buttons. No app kills, RAM purge or automatic OS setting changes were found in these paths.

Screens: Windows Health/Cleaner/Storage/Browsers/Performance/Software/Startup/Recovery/Settings/Activity. Mac Health/Cleaner/Storage/Performance/Recovery/Settings/Activity. Windows main layout is fixed minimum 980x640 logical pixels; Mac minimum 1080x700.

Key assets/models: filesystem candidates and approved roots; personally meaningful original paths/content; quarantine payloads + manifest; cleanup receipts; safety preferences/protected entries; storage summaries and duplicate hashes; process metadata; Windows app/package/startup inventory. A local application, not a multi-tenant SaaS: authentication, account roles and billing are not relevant features to add.

Important edge cases: files change/disappear between scan/action; active application databases; no free space to move across volumes; manifest/receipt persistence failure; restore original path occupied; stale protection policy; duplicate selected keeper; partial scans/permissions; parser/process failure; integer thresholds; canceled/long scan; redirected/overlapping personal folders; accessibility at large text scaling.

Done should mean: core scan/review/confirm/quarantine/receipt/restore/purge lifecycle verified end-to-end on each target OS, including failures and conflict recovery; current claims accurately describe behavior; desktop/keyboard/assistive-tech QA and signed release verification completed. The source does not support a claim that this evidence is already complete.

## Claimed versus implemented feature matrix

| Feature or claim | Windows | macOS | Assessment / evidence |
|---|---|---|---|
| Native local-only desktop app | WPF/.NET 8 | SwiftUI/SwiftPM | Implemented architecture; no accounts/backend required by product. README and Architecture agree. |
| First-run safety note | Health banner, preference stored | Health banner, preference stored | Implemented; nonmodal and can be bypassed by normal navigation. WPF MainWindow.xaml:164-191; Mac Views.swift:129-131,159-181. |
| Approved-root cleanup scan | Rules + age/path/link filters | Rules + age/path/link filters | Real services; security guardrails covered by the security audit. Windows scans all `_rules` at MainViewModel.cs:710; Mac all cleanupRules at MacAppState.swift:141-146. |
| Item preview | Category, risk, size, modified date, full path grid | Category, risk, size, truncated path row | Mac model holds date but row does not show it (Views.swift:240-269); shared guide claims age shown. |
| Search/category/risk | All three | Search/risk only | Scoped parity gap, not vulnerability. Mac has no category picker (Views.swift:190-200); search can match category text. |
| Sorting | WPF DataGrid built-in sort, initial size descending | Service-ordered list; no sort control | Partial parity. |
| Select all/visible/clear | All three plus category toggles | Visible/clear plus row toggles | Implemented subsets. |
| Conservative personal-file selection | Large files unselected; duplicate copies preselected | Large files unselected; duplicate copies preselected | Explicit-item-only/no-auto-choice claim is contradicted for duplicates; see F02. |
| Confirmation | Global setting can turn confirmation off | Confirmation dialogs always used | Mac setting is dead; Windows manual file labels can lie about mode (F03/F04). |
| Quarantine default | Yes, can globally disable | Always quarantine regardless of setting | Same default, different actual configuration semantics. |
| Recoverable manual large/duplicate cleanup | Shared pipeline, can actually permanently delete when global mode off | Separate always-quarantine path | Windows labeling defect F03; Mac receipt omission F05. |
| Receipts | JSON save, recent summary grid, export latest | Normal-cleaner JSON save, recent summary list | Mac manual operations omitted; no Mac export/detail navigation. |
| Before/after free space in receipts | System drive only | Not modeled | WPF receipt fields real but can refer to wrong volume for manual files on other drives (MainViewModel.cs:831,897,1954-1969). Mac scoped parity gap. |
| Quarantine restore / purge | Selected restore, selected/all purge | Selected restore/purge | Core actions exist; original-path conflicts warn, require manual intervention. |
| Retention | Auto-purge on every launch based on current setting | Stored setting unused | F04 for Mac; Windows should explicitly communicate auto-purge behavior/expiry. |
| Storage map | Implemented, misses root-level files | No map | Windows correctness F09; Mac intentional small scope but shared guide should mark Windows-only. |
| SHA-256 duplicate verification | Logical-size groups + hashes; newest timestamp keeper | Allocated-size groups + hashes; lexicographic keeper | Real cryptographic matching. Mac false negatives possible with allocation differences; both lack group keeper enforcement. |
| Recommended reclaim plan | Dynamic service, stale after ordinary scan | Static four-step instructional plan | F07 Windows; Mac scoped parity, not dynamic estimate. |
| RAM Assist | Physical/commit/process/uptime + OS handoffs | vm_stat/swap/process/uptime + OS handoffs | Real read-only feature; error-state/measurement flaws F11/F12. |
| Browser profiles/cache/cookies/history/sessions | Profile data-type selection, shared cleanup pipeline | Omitted | Intentional Windows-only scope. Firefox History deletes shared bookmark DB (F01). Profile screen lacks item-level preview before action. |
| Installed software search / uninstall | Registry inventory + Windows Apps & Features | Omitted | Intentional handoff, no silent uninstall; installed apps view has useful search-empty state. |
| WinGet updates/export/import | Commands with explicit update/import confirmation | Omitted | Real feature; the security audit covers execution/parser/security/release. |
| Startup inventory/impact | Registry, folder, Task Scheduler; heuristic impact | OS Login Items button only | Windows impact is heuristic by source/scope; not measured startup performance. Localized `schtasks` parsing gap F13. |
| Weekly scan reminder | Scheduler launch reminder (not automatic cleanup) | Omitted | Windows-only, accurate README wording; heading says Scheduled scans, should maintain reminder clarity. |
| Protected paths/extensions | Editable paths/extensions; cleanup recheck | Scan-time policy but extensions display-only; no path management | Mac feature gap (not promised in Mac list as fully editable), shared “tune protected policies” subtitle overstates UI. Root audits actual safety enforcement. |
| Preferences | Automatic save on property edits | Explicit Save; threshold also saves before scan | Persistence errors swallowed on both; the security audit covers recovery significance. Mac Save has no success/failure feedback. |
| Activity | In-memory capped 250 entries | In-memory unbounded | Intended session-only, documented. Mac can accumulate many file warnings; no persistent diagnostic export. |
| Busy/loading/cancel | Most commands disabled; indeterminate bar; no exposed Cancel | Global Scan/Clean disabled, other actionable buttons remain enabled and are silently ignored while busy | F08. No user cancellation on either platform. |
| Empty/error/success states | Cleaner/software/startup empty overlays; other grids blank; errors in status/activity | Cleaner/storage/performance/quarantine empty panels; errors mostly activity/footer | Mac zero-result scan indistinguishable from never scanned; restore/purge report success-like status even warnings (F10). |
| Accessibility | Named shell/nav/search/grids/actions, custom focus visual | Native SwiftUI controls, some labels | Good foundations; Mac selection uses unlabeled icon buttons instead of semantic Toggle; runtime QA outstanding. |
| Public release readiness | CI/tests/package/signing hooks; outstanding clean-OS QA/signing | Package scripts/core tests; outstanding signing/notarization/QA | README explicitly lists limitations. Do not conflate planned QA matrix with executed evidence. |

## Confirmed product defects, ranked

### F01 — Firefox “History” cleanup removes its bookmarks database

Priority P1; product severity High; confidence High. Affects Windows `MainViewModel.GetBrowserHistoryTargets` at MainViewModel.cs:1728-1733, browser UI MainWindow.xaml:672-675, execution at MainViewModel.cs:1660-1667,1400-1415.

The History checkbox selects `places.sqlite` wholesale. Mozilla confirms that database holds bookmarks, download records and browsing history, not just history: https://support.mozilla.org/en-US/kb/about-your-important-data-and-their-files and https://firefox-source-docs.mozilla.org/browser/places/architecture-overview.html . A user intending only to clear history can lose bookmarks/download metadata. Quarantine makes the file recoverable until purge, but Firefox may recreate the original database; restore refuses overwrite, so ordinary app restore is not a complete conflict workflow. Automatic browser bookmark backup restoration may reduce impact; no claim that all bookmarks are necessarily permanently lost on next launch.

Fix: disable Firefox History cleanup for now and offer browser-native clear-history handoff. A future logical database edit must respect Mozilla-supported behavior, browser lock state, WAL/SHM, backup/transaction consistency and preservation of bookmarks; do not rename broad DB deletion as a safe history operation.

Validation: synthetic Firefox profile with a bookmark and visit/download row; invoke history-only action, reopen Firefox and verify bookmark preserved. Test occupied-database restore/conflicts, active Firefox, and permanent-delete configuration. No user browser data should be used for the test.

### F02 — Duplicate workflow automatically chooses personal files and does not enforce a keeper

Priority P1; product severity Medium (combined with F03 becomes High); confidence High. Windows `DuplicateFileViewModel.cs:10-14`; `StorageAnalysisService.cs:169-187`; Mac `MacAppState.swift:234-246`; `Services.swift:520-534`. Docs claim explicit personal-file selection and no automatically chosen personal documents/media/downloads (`docs/USER_GUIDE.md:132`; README Safety Boundaries).

A duplicate scan checks all copies except the chosen keeper automatically. Windows chooses newest modified time; Mac chooses lexicographically first path. No state guard prevents the user from also selecting the keeper; Windows MainViewModel.cs:1056-1074 and Mac MacAppState.swift:254-262 send all selected files to cleanup. Byte equality does not establish that either file path is disposable: projects, media libraries and user workflows can depend on both locations. A confirmation of all recommended copies is not explicit per-item review.

Fix: default all duplicate rows to unselected. Render actual groups, require an explicit keeper per group, show full contextual paths, and reject a cleanup batch leaving no currently verified existing copy outside it. Revalidate identity/hash/freshness at action time as covered by the security audit. Recommendations can remain advisory.

Validation: scan two equal files: zero selected; keeper cannot be queued alongside the last surviving copy; different same-hash paths remain visible and no cleanup happens until explicit selection. Repeat after a file disappears/changes.

### F03 — Windows “Quarantine selected” can permanently delete personal files

Priority P1; product severity High; confidence High. MainWindow.xaml:562-565 and586-589 label actions as quarantine. MainViewModel.cs:1035-1074 routes both through RunCandidateCleanupAsync. That path obeys global UseQuarantine at829-879; confirmation is conditional on ConfirmBeforeCleanup at813-827. Settings exposes both switches at MainWindow.xaml:1098-1105.

A user can disable quarantine for routine temp cleanup, later click “Quarantine selected large files/duplicates,” and actually call File.Delete through CleanerService. With confirmations disabled, that click needs no final cleanup confirmation; restore-point reminder can also be disabled. High-risk personal selections are not protected by a separate non-bypassable review requirement. This is a reachable configuration-dependent data-loss bug, not an auth exploit.

Fix: force quarantine for all manual personal-file workflows regardless of generic temp-cleaner setting; use a dedicated typed operation mode rather than reading global mode in a shared action. Always require a scope-specific final review for high-risk operations. If permanent personal-file deletion is deliberately supported, name it explicitly and make its irreversible confirmation mandatory.

Validation: matrix UseQuarantine true/false × ConfirmBeforeCleanup true/false; invoke manual-large and duplicate action with a synthetic file. Every quarantine-labeled action must create manifest/payload and remain restorable. Test label and operation mode in state integration tests.

### F04 — macOS safeguards/retention controls are stored but do nothing

Priority P2; product severity Medium; confidence High. Views.swift:631-633 exposes confirmBeforeCleanup, useQuarantine and quarantineRetentionDays; Models.swift:211-215 models them. MacAppState.swift:173-215 and353-370 always call quarantine. Views.swift:229-236,330-335,383-388 always confirm. No state/service use of those three values exists beyond persistence/UI. QuarantineService has restore/purge but no expiry operation (Services.swift:221-354).

Turning confirmation/quarantine off has no effect (safe conservative behavior, still misleading). Retention suggests a lifecycle the app never performs, so retained payloads can consume disk indefinitely. “Save settings” gives no feedback and silently swallowed write errors compound this.

Fix: remove unsupported toggles and label quarantine as always-on; label retention advisory if no expiry engine is intended. Prefer explicit review/consent for expiry over adding silent deletion just to make the toggle work. Make persistence return Result and display success/failure. Add only supported safety settings when behavior is designed/tested.

Validation: change every visible setting, relaunch and verify observable documented behavior. Assert unsupported setting controls absent. Test unwritable preferences directory yields recoverable error.

### F05 — macOS personal-file cleanup bypasses receipts

Priority P1/P2; product severity Medium; confidence High. Normal cleanup constructs/saves receipt at MacAppState.swift:190-211. Large/duplicate path at343-370 only quarantines, updates results, refreshes recovery and logs an aggregate count. The sole receiptService.save occurrence is normal-cleaner flow. Shared README claims the same quarantine/receipt pipeline for personal files.

Users moving their most important files cannot audit requested paths, per-item outcomes, bytes or warnings in a persisted receipt; session activity disappears at relaunch. Quarantine manifest still retains successful paths, so this is not total loss of original-path metadata.

Fix: centralize all cleanup through one result + receipt service, preserving explicit operation mode and risk-specific confirmation. Persist requested/succeeded/skipped/failed outcomes for personal files and recovery changes, and surface receipt persistence failures.

Validation: normal, large and duplicate cleanup each produces a matching receipt, including partial/missing/permission failure. Relaunch and confirm all outcomes remain inspectable.

### F06 — macOS overlapping cleanup roots double-count and duplicate file operations

Priority P2; product severity Medium; confidence High. Services.swift:70-71 includes recursive Library/Logs plus its DiagnosticReports child. Caches at82 overlaps SwiftPM cache at104. scan loops139-213 append every match with a fresh UUID and no seen-path map.

An old log in DiagnosticReports appears twice in the same category and is auto-selected twice. A sufficiently old SwiftPM file appears as both User app caches and Swift Package cache. Estimates, selected counts and requested receipt bytes inflate; first move succeeds and later duplicate move warns missing. Candidate limit may be reached early, hiding other unique opportunities.

Fix: normalize/deduplicate physical candidates by identity or canonical non-following path before sizing/selection. Assign deterministic most-specific rule and preserve source-rule tags if useful. Remove redundant roots. Also deduplicate any manual batches defensively.

Validation: synthetic DiagnosticReports and SwiftPM files older than both thresholds; one unique candidate/path and one receipt outcome, byte total exactly once. Validate with overlapping custom roots.

### F07 — Windows reclaim plan is stale after the primary scan

Priority P2; product severity Low/Medium; confidence High. ScanAsync MainViewModel.cs:706-744 replaces PreviewEntries, calls RefreshSelectionSummaries, and sets scan status; RefreshSelectionSummaries:1524-1538 updates properties/command states only. ReclaimPlan is separate observable collection; refresh occurs in initial load, storage/recovery and cleanup paths (1615-1627), never ordinary scan.

Fresh first scan populates Health cleanup estimate but “Safe cleanup” row still reflects initial empty collection (usually zero). Later scans can show old amounts until another unrelated storage/recovery refresh. This undermines the primary decision screen.

Fix: derive plan from current observable state or explicitly recompute once after scan mutations/protection changes. Avoid recomputing per row to preserve scan performance.

Validation: initial zero plan → scan with known low-risk bytes → plan equals new candidates; rescan with different result → plan replaces old amount without unrelated navigation/action.

### F08 — macOS busy actions silently disappear; neither UI supports cancel

Priority P2; product severity Medium; confidence High for control state, runtime duration untested. Only global Scan and Cleaner Quarantine check `state.isBusy` (Views.swift:85,211). Large/duplicate scans and actions at298-306,351-359, performance refresh406-410, recovery restore/purge550-555 remain enabled. runBusy returns immediately while busy at MacAppState.swift:387-394 without status/queue. Init already starts a refresh at53.

Clicking an apparently available action during a long scan does nothing. Some dialogs can open and accept before the operation is dropped. Both UIs expose indeterminate progress only; Windows services accept CancellationToken in scanners but MainViewModel passes none and exposes no Cancel. Mac scan/hash loops have no cancellation path. A large personal archive can lock out cleanup/recovery actions for an unbounded time.

Fix: disable all operation-start buttons and selection-changing dangerous controls consistently, show active task/progress, expose cooperative cancellation with a safe partial-result policy. Return an explicit Busy outcome if invoked programmatically; preserve the last result on cancellation. OS read-only handoffs may remain available.

Validation: delayed injected operation; other commands visibly disabled, no accepted dialog is silently discarded, Cancel returns UI to usable state, partial file mutations remain recorded.

### F09 — Windows storage map misses files directly in personal root folders

Priority P2; product severity Medium; confidence High. StorageAnalysisService.ScanFolderUsage at77 iterates only top-level directories; at86-101 adds size/type counts only for files recursively inside those directories. No root-file enumeration. GetUserStorageRoots:290-306 returns Downloads, Desktop, Documents, Pictures, Videos, Music.

A 20 GB ISO directly in Downloads contributes nothing to storage map or file-type table, although the large-file scanner finds it. A root containing only files yields an empty map, contradicting “storage usage” labels and producing materially misleading totals. Max-result truncation also is not surfaced by Windows map/large/duplicate APIs (23-26,55-58,190-193).

Fix: count direct files under each root (e.g., explicit “Files in Downloads” bucket) and keep global extension totals inclusive. Show scanned scope, permission skips and truncation with partial-result status; do not imply whole-disk usage.

Validation: fixture containing root-level ISO plus nested photo; folder/file-type totals include both exactly once, large scan/map agree for included scope; zero directories still produces root-files result.

### F10 — macOS zero-result and partial failure feedback is misleading

Priority P2; product severity Low/Medium; confidence High. summaryText MacAppState.swift:91-95 reports “No scan has been run yet” whenever candidates empty, even after successful empty scan/cleanup. Large/duplicate empty views say “No … scan yet” whenever lists empty (Views.swift:325-326,378-379). Restore/purge status is unconditional “operation complete” / “items purged” after warning-only service result (MacAppState.swift:265-294); warning detail only in Activity. Receipt list shows warning count without expandable detail (Views.swift:596-614).

Fix: track operation state independently (`notRun/loading/complete/partial/failed/canceled`) plus timestamp and counts. Report succeeded/skipped/failed counts and inline link to detailed outcomes. Provide retry/restore-conflict “choose another destination” flow. Keep warning details inspectable after relaunch in receipts.

Validation: empty success vs never-run vs filtered-empty vs access denied are distinct; all restore failures cannot show “selected items purged”; warning count opens actual paths/messages.

### F11 — RAM Assist converts failed samples into alarm or healthy advice

Priority P2; product severity Medium; confidence High. Mac run returns empty on launch failure and ignores exit status at Services.swift:858-873. parseVMStat at742-753 defaults all absent page counts to zero; with valid physicalMemory it produces available=0 and usage=100. snapshot classifies that Critical at597,623-633. Windows CaptureSnapshot returns Unknown if GlobalMemoryStatusEx fails at PerformanceAssistService.cs:141-152; BuildRecommendations default branch at51-76 says memory pressure healthy for any non-Critical/High/Elevated value, including Unknown.

The diagnostic tool may tell users to close apps because sampling failed, or say memory healthy after counters failed. Both use simple usage-percentage thresholds as “memory pressure”; macOS actual pressure includes free memory, swap rate, wired memory and cached files per Apple: https://support.apple.com/guide/activity-monitor/view-memory-usage-actmntr1004/mac . This is a heuristic, not an OS memory-pressure reading.

Fix: return typed result/error, validate required vm_stat fields/exit code and reasonable values, render Unknown with retry and no pressure recommendations on invalid data. Separate “RAM usage estimate” from actual OS pressure. Add measurement timestamps/freshness and update advisory dependencies only after their inventories were actually loaded.

Validation: empty/truncated/unrecognized vm_stat; nonzero child exit; counter API fail; absent swap; unusual page size. All display Unknown/error and neither critical nor healthy advice. Compare valid samples against Activity Monitor/Windows tools under controlled load, with tolerances.

### F12 — macOS process collector can deadlock before first usable session

Priority P1/P2; product severity Medium; confidence High for pattern, a controlled helper reproduction is included in the audit evidence. Services.swift:858-870 redirects stdout/stderr, waits for process exit before reading stdout, and never drains stderr. `ps -axo pid=,rss=,comm=` (594) scales with process count/path lengths. Once output exceeds pipe capacity, child blocks writing while parent blocks waiting. No timeout/cancel exists. Initial MacAppState init invokes this refresh at53 and runBusy remains true until completion at387-394, preventing future scan operations.

Fix: drain both streams concurrently while child runs; wait with a timeout, terminate on cancellation, cap collected bytes safely and return failure state. Swift Process readability handlers or concurrent reads can implement this without awaiting completion first. Do not test with real destructive operations.

Validation: fake helper emits >1 MB on stdout and stderr, then exits; collection completes quickly and diagnostics parse or fail clearly. Fake never-exiting helper times out and UI busy resets. Verify many-process host startup. The controlled helper proves the wait-before-read failure pattern; a real process-table flood was not performed.

### F13 — Windows scheduled-task startup inventory fails on localized OS output

Priority P2; product severity Medium; confidence High for English-only parser, localized-runtime validation outstanding. StartupService.cs:108 invokes `/Query /FO CSV /V` then137-139 matches English `TaskName`, `Task To Run`, `Schedule Type`; disabled/trigger filters use English at153,158-159. Missing headers map to -1 and GetCsvValue returns empty232, causing all tasks to be skipped150-155. Inventory says successfully loaded with count, no error signal.

Startup impact is also assigned from source/scope, not measured: registry All users=Medium / Current user=Low64; folder always Low94; tasks always Medium171. The RAM Assist High-impact branch can never be driven by these generated entries (PerformanceAssistService.cs:91-93), so do not present these as measured impact.

Fix: use Task Scheduler COM/API or structured XML task definitions rather than localized display CSV. Expose access/parse failure. Label source-based guidance as estimated/manual, or collect actual OS startup impact if available. Add direct Task Scheduler handoff for task rows instead of only generic Startup Settings.

Validation: non-English Windows/task CSV fixture, disabled task, boot/logon/other trigger, denied API access, mixed-status startup list. Confirm English and localized results consistent.

### F14 — macOS accepts an integer threshold that crashes a storage scan

Priority P2; product severity Medium; confidence High. Storage TextFields bind directly to `Int` preferences at Views.swift:294 and347 with no range validation, although Settings Steppers advertise bounded values. `scanLargeFiles` and `scanDuplicates` multiply `Int64(max(1, minimumMB)) * 1_048_576` at Services.swift:428,478. Swift's normal integer multiplication traps on overflow. A valid Int typed into either field can therefore terminate the app; preferences are saved before scan at Views.swift:299,352, so the value persists into the next attempted scan. Text entry also permits negative/zero values that are silently treated as 1 MB rather than the displayed threshold.

Fix: validate at form boundary with a connected helpful message, clamp/validate again at preference loading and service boundary, and use `multipliedReportingOverflow` or checked conversion. Choose one documented supported range consistently across TextField and Stepper. Disable Scan until valid. Do not silently save invalid values.

Validation: 0, negative, letters, decimal, Int.max, upper-limit+1, and malformed persisted preferences; none crash, valid range persists and is used exactly. Fixture arithmetic verified that Int.max multiplication overflows using reportingOverflow without intentionally crashing the application.

## Accessibility / layout review (source evidence, runtime QA required)

Strengths:

- WPF has AutomationProperties names for shell, main nav/actions, search, grids and progress. App.xaml:22-34 defines visible dashed focus; applies to buttons/checks/text/combos/grids at41-63,82-95. TextBlock wrapping is enabled globally at36-39, so do not incorrectly claim all unannotated TextBlocks fail wrapping.
- SwiftUI uses native NavigationSplitView/List/Form/Picker/Stepper/Button; scan has Cmd-R shortcut (Views.swift:83); primary search and progress have explicit accessibility labels.
- Risk/status are generally text plus color rather than color alone. No web ARIA checklist is appropriate for these native apps.

Confirmed semantic gap, P2:

- Mac item selection is an icon-only plain Button (Views.swift:247-250,562-571,785-788), without contextual accessibilityLabel/value or Toggle semantics. SF Symbol auto descriptions cannot communicate which specific path is being selected or its checked state reliably. Use native Toggle with `.toggleStyle(.checkbox)` and contextual file label, or explicit label/value/traits and keyboard Space semantics. Validate VoiceOver announces name/path, selected state and action, and selection is operable without mouse.
- Windows blank-header selection grid columns have no per-item AutomationProperties override (MainWindow.xaml:424,573,597,1031). Test Narrator result; no assertion of failure without runtime evidence. Provide names combining item path and selection state if default DataGrid peer context proves insufficient.

Layout/state risks requiring manual validation, P2/P3:

- Windows Health renders long reclaim plan + settings lists in an unscrollable Grid/StackPanel (MainWindow.xaml:157-280). At minimum window/text scaling, content may clip. Settings/performance have explicit scrolls; Health does not. Add bounded ScrollViewer/adaptive layout if reproduced. Test 980x640 and Windows text scaling 150/200% on laptop displays.
- Mac metrics use horizontal HStacks with 3/4 cards (Views.swift:132-136,423-444) and performance two-column HStack446. Minimum1080x700 at SpacePilotMacApp.swift:11 hides narrower layouts; test scaled displays/long path names/VoiceOver. This is a desktop app: lack of phone layout is not itself a defect.
- Mac status footer single-line truncation Views.swift:110; paths in rows truncated with no reveal/full-path action. Consider tooltip/copy/reveal and accessible full text. No Finder/Explorer reveal action exists in storage/cleaner on either platform, making a high-risk path review harder.
- Sidebar Windows buttons have no persistent selected/active state styling/binding (App.xaml:72-80; MainWindow.xaml:66-115), though heading changes. Add selection state/automation selection semantics if retained as Button-based navigation.
- WPF hardcoded palette ignores high contrast/dark theme (App.xaml:6-20). Test system high contrast before accessibility claims; do not assert contrast failure solely from palette.
- Mac Save settings / NSWorkspace OS handoffs provide no success/failure status (MacAppState.swift:135-137,326-336; Views.swift:651-653). Windows OS handoffs catch and display failures2041-2062.

## Scoped parity / design gaps (not security vulnerabilities)

1. Shared guide should label Windows-only storage map, category picker, configurable extensions/protected path editor, receipt export/drive-space/expiry, software/startup/browser workflows explicitly. Mac README correctly scopes many, but User Guide's Cleaner/Storage/Recovery general sections overstate parity (lines110-132,170-176).
2. Mac Health reclaim plan is static guidance, not generated recommendation/estimated opportunity. If keeping scope small, call it “Suggested workflow” and do not imply measured prioritized reclaim planning.
3. Receipts are summary-only in both UIs. Windows exports latest only, cannot select/export older one through UI; Mac no export or reveal. Add item/outcome details, per-receipt export and warning detail. Preserve accurate free-space scope for multivolume cleanup.
4. Restore conflict safety is good (no overwrite) but recovery flow stops at warning. Add “restore to another folder” with safe explicit destination and progress; retain original path/manifest on fail.
5. macOS no category/rule configuration or editable protected paths/extensions, although settings subtitle says tune protected policies. Users can manually select medium-risk rows; no ability to opt a whole category into defaults. Decide scoped semantics rather than adding switches for appearance.
6. Windows storage result limits are hidden; Mac 5000 cleaner candidate cap stops rules early before final sort, so early temp/log roots can crowd later useful candidates. Need partial/truncated state with rescan/scope control rather than exhaustive claim.
7. Duplicates not grouped visually; all entries flat and no keeper lock. No storage search/filter/sort controls on Mac and no explicit path reveal on either. Prioritize safe review/context over new broad features.
8. Performance advice action strings are TextBlocks/Text, not clickable; not a dead control defect. Convert useful actions into real command enum/navigation only if desired; keep no-action/read-only copy clear.
9. Native app architectures have reasonable UI/state/service models but huge orchestration files (Windows state2064 lines, XAML1324; Mac state400, views828, Services875). Break into feature services/view models only around fixes, not an unrelated rewrite. Use shared cleanup orchestration to eliminate divergent receipt/mode handling.
10. No user cancellation or persistent diagnostic export, no automated end-to-end/state/UI tests found by targeted searches. Existing unit/core safety tests do not establish all current feature workflows work. No fake charts/unconnected marketing navigation observed.

## Suggested feature remediation roadmap

Before public release:

- Disable Firefox database-as-history operation; force quarantine + explicit personal-file selection + mandatory high-risk review; keeper safeguards.
- Make manifest/receipt persistence failures visible and recovery reliable (security findings).
- Route Mac manual files through receipt pipeline; remove misleading settings; repair long-running process collector and invalid diagnostic states.
- Fix unique-path counting, map accounting and reclaim-plan refresh. Add focused integration tests for these control flows.

Next reliability/accessibility increment:

- Operation-state model, cancel/progress, consistent busy controls, partial/error summaries, restore conflict flow, receipt details/export.
- Semantic selection controls, keyboard/screen-reader pass, scaling/scroll/high-contrast tests on actual Windows and macOS.
- Locale-independent startup API; honest estimated pressure/impact labels; clear scanned scope/truncation and platform-specific docs.

Later product improvement:

- Signed/notarized clean installs and updates, opt-in diagnostics, clearly consented retention UX (release review), carefully justified feature parity; don't add cloud/auth/billing unless scope changes.

## Targeted product validation plan

1. Per-platform fixture-only end-to-end: scan → filter/select → confirm → quarantine → receipt inspection → relaunch → restore → conflict → restore-as → purge → receipt; verify original paths and bytes.
2. Regression matrix for UseQuarantine/confirm/risk and category defaults; personal selections zero initially and keeper protection. No personal original deleted by quarantine-labeled action.
3. Data correctness: overlap roots, root-level files, differing allocated/logical sizes, file disappears/changes, max result limits, inaccessible root, multi-volume free space; results show partial scope and no duplicate counts.
4. Diagnostic collectors: empty/corrupt/huge output, command error/timeout, protected/exiting process, Unknown RAM sample; no bogus healthy/critical state, busy always resets and Cancel works.
5. Persistence: corrupt/unwritable preferences/receipt/manifest, multiple launches/processes, crash between move and record (security review), and old schema; errors actionable, recoverable items visible.
6. Windows 10/11 standard/elevated supported setups, non-English locale, missing/offline WinGet; Mac14+ Intel/ARM supported configuration with full build runtime. Record commit/artifact/toolchain and actual result, not just a matrix template.
7. Keyboard-only/Narrator/VoiceOver select/review/quarantine/restore/purge; high contrast, dark mode, text scale and minimum windows; all actionable controls contextual, all destructive modes explained, every operation has outcome.

## Inspected files

Complete files / principal review:

- README.md
- docs/PRODUCT_BRIEF.md
- docs/USER_GUIDE.md
- docs/QUALITY_CHECKLIST.md
- docs/SAFETY_MODEL.md
- docs/ARCHITECTURE.md
- docs/windows/README.md
- docs/macos/README.md
- docs/windows/QA_MATRIX.md
- docs/macos/QA_MATRIX.md
- apps/windows/src/SpacePilot/MainWindow.xaml
- apps/windows/src/SpacePilot/App.xaml
- apps/windows/src/SpacePilot/ViewModels/MainViewModel.cs
- apps/windows/src/SpacePilot/ViewModels/DuplicateFileViewModel.cs
- apps/windows/src/SpacePilot/ViewModels/StorageFileViewModel.cs
- apps/windows/src/SpacePilot/Services/StorageAnalysisService.cs
- apps/windows/src/SpacePilot/Services/ReclaimPlanService.cs
- apps/windows/src/SpacePilot/Services/StartupService.cs
- apps/windows/src/SpacePilot/Services/PreferencesService.cs
- apps/windows/src/SpacePilot/Services/ProtectionPolicyService.cs
- apps/windows/src/SpacePilot/Services/PerformanceAssistService.cs (1-260, pertinent sample/recommendation methods)
- apps/windows/src/SpacePilot/Services/CleanerService.cs (1-280, pertinent scan/cleanup path)
- apps/macos/SpacePilotMac/Sources/SpacePilotMac/Views.swift
- apps/macos/SpacePilotMac/Sources/SpacePilotMac/MacAppState.swift
- apps/macos/SpacePilotMac/Sources/SpacePilotMac/Models.swift
- apps/macos/SpacePilotMac/Sources/SpacePilotMac/Services.swift
- apps/macos/SpacePilotMac/Sources/SpacePilotMac/SpacePilotMacApp.swift

Targeted test inventory search: apps/windows/tests/SpacePilot.Tests, apps/macos/SpacePilotMac/Tests, apps/macos/SpacePilotMac/Validation. No state/view/storage/browser/startup tests matched the targeted symbol search; the security audit contains the complete test/build inventory/results.

## Open questions / unresolved evidence

- Are browser history/cookie/session controls supposed to promise selective history-only cleanup or broad profile reset? Current labels imply selective and Firefox implementation exceeds scope.
- Should manual personal-file action ever support irreversible deletion, and what exact consent must remain mandatory? Current operation naming is unsafe.
- Is Windows startup impact intentionally a heuristic, and do locale support claims exist? APIs needed for measured impact differ from configured entries.
- What are the intended retention semantics: auto-delete on startup, scheduled expiry, advisory expiry, or explicit review? Platforms currently disagree.
- Are overlapping/redirected Windows personal folders supported? Exact-root dedup exists; ancestor-overlap dedup not present; self-duplicate risk should be tested before making a separate finding.
- Should duplicate grouping use logical length and disk-reclaim estimate use allocated bytes? Mac conflates them at Services.swift:501-503. Equal logical contents can have different allocation/compression; needs APFS fixture QA.
- No claim of completed runtime visual/accessibility review. Host tooling limitation does not establish shipped runtime crash or layout failure; the security audit records exact build diagnostics.

## Additional executed fixture validation

The [fixture runner](run-mac-feature-proof.sh) compiled unchanged production Models.swift, Formatters.swift and Services.swift with the [fixture source](mac-feature-probe.swift). Compile and execution both exited 0. This bypasses SwiftUI and does not replace the complete build evidence. The exact result is in the [proof output](mac-feature-proof.txt).

Output:

```text
fixture-only scan: log occurrences=2; SwiftPM occurrences=2; total candidates=4
empty vm_stat: available=0; usage=100.0; pressure=Critical
max Int threshold multiplication overflows=true
```

The harness created one 4096-byte old log and one 4096-byte old SwiftPM cache below a temporary synthetic home, ran the production default rules and cleaner, asserted each appeared twice, then deleted the fixture. It called the production empty-vm_stat parser/classifier and asserted the false Critical result. The arithmetic check used reportingOverflow; no forced crash/destructive operation or live user scan was performed.
