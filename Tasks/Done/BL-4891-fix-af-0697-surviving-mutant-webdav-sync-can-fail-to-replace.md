---
id: BL-4891
title: Fix AF-0697: Surviving mutant: --webdav-sync can fail to replace an existing local file it re-downloads
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebDavTools.UnitLibrary, Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavSyncTransferHookTests.cs]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-4891 — Fix AF-0697: Surviving mutant: --webdav-sync can fail to replace an existing local file it re-downloads

## Goal

The defect the audit office reported as AF-0697 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0697 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0697-surviving-mutant-webdav-sync-can-fail-to-replace-a.md`.

Location: `Curl.Extension.WebDavTools.UnitLibrary/Transfer/WebDavSyncTransferHook.cs:126`

Location: `Curl.Extension.WebDavTools.UnitLibrary/Transfer/WebDavSyncTransferHook.cs:126`

'File.Move(partialPath, item.LocalPath, overwrite: true);' mutated to 'overwrite: false' survived (drawn library). The planner downloads a file whose local copy is older or a different length (WebDavSyncPlannerTests). Moving over that copy without overwrite throws IOException, so a sync that updates a file would fail. WebDavSyncTransferHookTests only sync into an empty folder, or a second time when the file is current.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.WebDavTools.UnitLibrary/Transfer/WebDavSyncTransferHook.cs:126:true -Member Keep -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Pipeline `feature` run as a test-only fix: the production line is right; only a test was missing. New test `ExecuteAsync_ALocalCopyOfADifferentLength_IsReplacedByTheDownload` in `Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavSyncTransferHookTests.cs` syncs over a stale local copy of a different length and checks it is replaced, has the server's last-modified time and leaves no partial file.
- `touches` widened to that test file: the task named only the library, its tests belong in the matching `.UnitTests` project, and no task in Doing on work/dark-factory names it.
- Reproduction: a lane cannot run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand (`overwrite: false` at WebDavSyncTransferHook.cs:126). The new test failed (killed) and passed again once the mutation was reverted. The re-audit confirms it with the tool.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: no failures; WebDavTools.UnitTests 100 passed. No production code changed, so Measure-CodeQuality was not needed.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-06: Doing -> Done. --webdav-sync replacing a stale local copy is tested; the overwrite:false mutant is killed
