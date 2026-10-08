---
id: BL-1678
title: Fix AF-0051: BL-1467 claimed 3 times and requeued twice: lanes 4 and 5 each failed to integrate after rebasing onto other lanes' work
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1678 — Fix AF-0051: BL-1467 claimed 3 times and requeued twice: lanes 4 and 5 each failed to integrate after rebasing onto other lanes' work

## Goal

The defect the audit office reported as AF-0051 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0051 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0051-bl-1467-claimed-3-times-and-requeued-twice-lanes-4.md`.

Location: `logs/DarkFactory-20261006-200306-L4.log:42`

Location: `logs/DarkFactory-20261006-200306-L4.log:42`

Lane 4 claimed BL-1467 at 06:36:49 (commit b0b89735e) and finished at 06:42:51 (40 turns, 5.9 min). At 06:45:57 it logged 'PARKED build failed after rebasing onto the other lanes' work' and REQUEUE (606d11f9c). Lanes 1, 3 and 6 integrated BL-1465, BL-1528 and BL-1455 between 06:40:57 and 06:45:39. Lane 5 claimed it at 06:49:10 (L5.log:62) and ran only 1.9 min (21 turns): build ok, test ok, move Done, with no fix for the integration break. It was parked again at 06:54:30 (L5.log:70, 365389a10). Lane 4's third claim at 07:00:50 fixed the build: 'build FAIL 4 error(s)' at 07:02:14, then ok, and DONE at 07:09:01 (f42b1420c). The errors in BL-1467-20261006-200306-L4.jsonl are 'CS0103: The name DiagnosticAssertionLines does not exist' and 'CS0407: string CredentialCacheStore.DefaultCacheName() / KeytabStore.DefaultKeytabName() has the wrong return type'. These are conflicts with work integrated in parallel. The second run repeated the in-lane check that had already passed instead of reproducing the integration failure, so it was wasted. The task's own Log confirms both requeues.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1467' | Select-Object id,claims,outcome; Select-String -Path ..\logs\DarkFactory-20261006-200306-L4.log,..\logs\DarkFactory-20261006-200306-L5.log -Pattern 'BL-1467 (claim|PARKED|REQUEUE|DONE)'
```

- Expected: BL-1467 claims 1: one claim and one DONE.
- Actual: BL-1467 claims 3, outcome done. Claims at 06:36:49 (L4), 06:49:10 (L5) and 07:00:50 (L4); PARKED/REQUEUE at 06:45:57 and 06:54:30; DONE at 07:09:01.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
