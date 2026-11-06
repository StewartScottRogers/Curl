---
id: BL-4891
title: Fix AF-0697: Surviving mutant: --webdav-sync can fail to replace an existing local file it re-downloads
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebDavTools.UnitLibrary]
requirement: none
created: 2026-11-06
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
