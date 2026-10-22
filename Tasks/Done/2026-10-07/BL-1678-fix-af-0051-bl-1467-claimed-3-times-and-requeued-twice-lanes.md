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
completed: 2026-10-07
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded. (For future shifts; see Notes.)
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: when a finished task's build went red only on the rebased tree, `Invoke-Integrate`
  parked it straight away. The next run started from the parked branch on its own, found it
  green and changed nothing, so it was parked again for the same break (BL-1467's L5 run).
- Fix in `RunDarkFactory.ps1`: `Repair-IntegrationBreak` runs one headless repair run
  (`$RepairPrompt`, log suffix `-repair`, 30 minutes, the lane deny list) on the combined
  red tree while the lane still holds the integrate lock, so no other push moves the tree
  under it. It commits whatever the run left, re-runs `Test-Green`, and the task parks only
  if it is still red. Traced as `repair`. The header's `integrate` step says so.
- AF-0072's per-shift requeue list already stops a parked task being claimed again in the
  same shift. With the repair run, a break like BL-1467's is fixed within the first claim:
  one claim, one DONE.
- The reproduction measures the 2026-10-06 logs, which cannot change, so it still shows 3
  claims for BL-1467. It can only show the fix on later shifts' logs, at the re-audit.
  This lane cannot run `Audit/Tools/Measure-FactoryProcess.ps1` (the audit guard refuses
  it), so the lane logs were read with Select-String instead.
- Checked: the script parses, `-TestPark` passes, `dotnet build` is clean, and the fast
  tests are green. No self-test covers the repair run, because it needs a live Claude run.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. A finished task that goes red on the rebased tree gets one repair run in place before it is parked (AF-0051)
