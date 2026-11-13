---
id: AF-0697
title: Surviving mutant: --webdav-sync can fail to replace an existing local file it re-downloads
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.WebDavTools.UnitLibrary/Transfer/WebDavSyncTransferHook.cs:Keep-true:surviving-mutant
reproduction: mutation Curl.Extension.WebDavTools.UnitLibrary/Transfer/WebDavSyncTransferHook.cs:126:true
task: BL-4891
tasks: BL-4891
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed: 2026-11-12
closed-how: mechanical
closed-by: 2026-11-12_2105.md
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

- 2026-11-12 | 2026-11-12_2105.md | reproduces: no | Ran the -Site command: WebDavSyncTransferHook.cs:126 'File.Move(..., overwrite: true)' -> 'overwrite: false' was killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-05: filed proposed.
- 2026-11-12: accepted -> closed. Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
