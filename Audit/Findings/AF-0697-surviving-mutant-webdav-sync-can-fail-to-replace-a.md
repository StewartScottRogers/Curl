---
id: AF-0697
title: Surviving mutant: --webdav-sync can fail to replace an existing local file it re-downloads
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.WebDavTools.UnitLibrary/Transfer/WebDavSyncTransferHook.cs:Keep-true:surviving-mutant
reproduction: mutation Curl.Extension.WebDavTools.UnitLibrary/Transfer/WebDavSyncTransferHook.cs:126:true
task: BL-4891
tasks: BL-4891
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0697 - Surviving mutant: --webdav-sync can fail to replace an existing local file it re-downloads

## Summary

High finding from the quality auditor at `Curl.Extension.WebDavTools.UnitLibrary/Transfer/WebDavSyncTransferHook.cs:126`: Surviving mutant: --webdav-sync can fail to replace an existing local file it re-downloads.

## Evidence

Location: `Curl.Extension.WebDavTools.UnitLibrary/Transfer/WebDavSyncTransferHook.cs:126`

'File.Move(partialPath, item.LocalPath, overwrite: true);' mutated to 'overwrite: false' survived (drawn library). The planner downloads a file whose local copy is older or a different length (WebDavSyncPlannerTests). Moving over that copy without overwrite throws IOException, so a sync that updates a file would fail. WebDavSyncTransferHookTests only sync into an empty folder, or a second time when the file is current.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.WebDavTools.UnitLibrary/Transfer/WebDavSyncTransferHook.cs:126:true -Member Keep -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-05: filed proposed.
