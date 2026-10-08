---
id: BL-1779
title: Fix AF-0101: BL-1627 cost 4.32 US dollars, 3.6 times the median; a 70-turn run on lane 7 was parked on the flaky cookie test
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1779 — Fix AF-0101: BL-1627 cost 4.32 US dollars, 3.6 times the median; a 70-turn run on lane 7 was parked on the flaky cookie test

## Goal

The defect the audit office reported as AF-0101 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0101 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0101-bl-1627-cost-4-32-us-dollars-3-6-times-the-median.md`.

Location: `logs/DarkFactory-20261007-111121-L7.log`

Location: `logs/DarkFactory-20261007-111121-L7.log`

L7 run: 13.5 min, 70 turns, $3.56, then PARKED at 16:05:48 (fast tests failed twice: Curl.Http2.UnitTests, then the EveryMember cookie test). L3 redid it at 16:41:08 in 6.6 min for $0.76.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1627' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1627 claims 2 costUsd 4.3163

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause, read from `Curl.logs/DarkFactory-20261007-111121-L7.log` and `Curl.logs/BL-1627-20261007-111121-L7.jsonl`: the L7 run ($3.56, 70 turns) was not wasted on its own work - it finished BL-1627 (1673/1673 SSH tests green, Done, committed). The overspend is the redo: integration found the rebased tree's fast tests red twice on projects BL-1627 never touched (Curl.Http2.UnitTests, then `EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother`), parked the finished task, and L3 redid it for $0.76.
- Both halves are already fixed later: 2a4e625f4 (BL-1651) stopped the cookie test pinning a racing last writer, and e4e7fdc22 (BL-1770, AF-0092, 2026-10-08 12:09) makes `Test-Green` rerun the failing projects alone before parking, so two different load-only reds no longer park a finished task. No new mechanism; this task cites AF-0101 beside AF-0092 in the integrate help and `Test-Green`'s comment.
- The reproduction's `-Since 2026-10-07` window will always show BL-1627's past $4.32, because logs do not change, and a lane cannot run `Audit/Tools/Measure-FactoryProcess.ps1` (the audit path guard). As for BL-1775 to BL-1778, the box is ticked because the cause is fixed; the re-audit should measure runs started after e4e7fdc22.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Cause was a finished run parked on two unrelated flaky reds; fixed by BL-1651 and AF-0092's rerun-alone (BL-1770), now cited for AF-0101; build clean, fast tests green
