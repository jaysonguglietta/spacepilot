# Windows QA Matrix

Use this matrix before a public SpacePilot release.

## Operating Systems

| Platform | Standard User | Administrator | Notes |
| --- | --- | --- | --- |
| Windows 10 latest supported build | Required | Required | Validate WPF rendering, temp cleanup, WinGet fallback, startup inventory. |
| Windows 11 latest stable build | Required | Required | Primary release target. |
| Windows 11 with Storage Sense enabled | Required | Optional | Verify settings audit copy and Storage Settings links. |
| Windows 11 with Storage Sense disabled | Required | Optional | Verify settings audit warning state. |

## Install And Launch

| Scenario | Expected Result |
| --- | --- |
| Run from source with `dotnet run` | App launches with SpacePilot title and icon. |
| Run framework-dependent publish folder | `SpacePilot.exe` launches when desktop runtime is present. |
| Run self-contained publish folder | `SpacePilot.exe` launches without separately installing runtime. |
| Run unsigned zip artifact | Windows may warn, but app should launch after user approval. |
| Run signed artifact | Signature is valid and app launches without unknown-publisher warning. |
| Extract installer ZIP | ZIP contains MSI, MSI checksum, and install note. |
| Install MSI | Start menu shortcut launches SpacePilot. |
| Uninstall MSI | Installed files and shortcut are removed. User data remains unless manually deleted. |

## Core Workflows

| Workflow | Standard User | Administrator | Expected Result |
| --- | --- | --- | --- |
| First-run safety note | Required | Required | Note appears once and can be dismissed. |
| Cleanup scan | Required | Required | Candidates are listed with category, risk, size, and path. |
| Cleanup with quarantine | Required | Required | Files move to quarantine and receipt is created. |
| Restore from quarantine | Required | Required | Files return to original path when path is free. |
| Purge quarantine | Required | Required | Quarantine entry is removed and disk space is reclaimed. |
| Large-file scan | Required | Required | Large files above threshold are listed without auto-selection. |
| Duplicate scan | Required | Required | Duplicate groups are hash-verified before display. |
| RAM Assist refresh | Required | Required | Memory counters, pressure, uptime, process count, top processes, and recommendations refresh without terminating apps. |
| RAM Assist OS handoff | Required | Required | Task Manager, Resource Monitor, and Power Settings buttons open the expected Windows tools. |
| Browser profile discovery | Required | Required | Installed browser profiles are listed. |
| Browser cache cleanup | Required | Required | Cache cleanup works when browser is closed; locked files warn. |
| Software inventory | Required | Required | Installed apps are searchable. |
| WinGet updates | Required | Required | Missing WinGet fails gracefully; available WinGet returns updates or empty state. |
| Startup inventory | Required | Required | Registry, startup folder, and task entries are displayed or empty state is useful. |
| Settings audit | Required | Required | Checks show status and useful descriptions. |
| Weekly reminder | Required | Required | Task can be enabled and disabled. |
| Activity log | Required | Required | Important actions and warnings are visible. |

## Safety Cases

| Case | Expected Result |
| --- | --- |
| Candidate is approved root itself | Cleanup is blocked. |
| Candidate is outside approved root | Cleanup is blocked. |
| Candidate is under sibling path with same prefix | Cleanup is blocked. |
| Candidate is a reparse point | Scanner skips it. |
| Protected extension selected elsewhere | Cleanup skips it. |
| Protected path selected elsewhere | Cleanup skips it. |
| Original restore path already exists | Restore is skipped with warning. |
| File disappears after scan | Cleanup continues and reports useful warning if needed. |
| File locked by running app | Cleanup does not crash and reports warning. |
| Protected process path hidden | RAM Assist still lists process name/PID/memory and shows a useful protected-path placeholder. |
| High RAM pressure | RAM Assist recommends reviewing top processes instead of force-freeing memory. |

## Accessibility

| Area | Expected Result |
| --- | --- |
| Keyboard navigation | Sidebar, top actions, tab controls, grids, forms, and footer can be reached without mouse. |
| Focus visibility | Focus indicator is visible on buttons, text boxes, combo boxes, check boxes, and grids. |
| Screen reader naming | Main shell, navigation, primary buttons, data grids, and status progress expose useful names. |
| Text scaling | Important labels and controls do not overlap when Windows text size is increased. |
| Contrast | Main text, muted text, buttons, and status badges meet contrast expectations. |

## Release Evidence

Record for each release candidate:

- Git commit.
- Build artifact name.
- SHA-256 checksum.
- Windows versions tested.
- Signing certificate subject and thumbprint.
- Installer result.
- Known issues.
- Release decision.
