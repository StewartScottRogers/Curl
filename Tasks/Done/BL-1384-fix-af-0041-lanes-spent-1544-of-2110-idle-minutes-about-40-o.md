---
id: BL-1384
title: Fix AF-0041: Lanes spent 1544 of 2110 idle minutes (about 40% of lane time) waiting on overlapping touches
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1384 — Fix AF-0041: Lanes spent 1544 of 2110 idle minutes (about 40% of lane time) waiting on overlapping touches

## Goal

The defect the audit office reported as AF-0041 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0041 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0041-lanes-spent-1544-of-2110-idle-minutes-about-40-of.md`.

Location: `logs/DarkFactory-20261003-145757-L4.log:2`

Location: `logs/DarkFactory-20261003-145757-L4.log:2`

waitOverlapMinutes=1544, laneIdleMinutes=2110, against roughly 3800 lane minutes (9 lanes, about 7h each). The wait message reads 'No task can start yet: every ready task overlaps one in Doing' (lanes 3 to 6 right after start). That is far over the 20% threshold. I did not identify the specific touches that serialised the lanes.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json) | Select-Object laneIdleMinutes,waitOverlapMinutes
```

- Expected: waitOverlapMinutes under 20% of lane time (under about 760).
- Actual: laneIdleMinutes 2110, waitOverlapMinutes 1544.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] A fixed `-Lanes N` shift starts no more lanes than `task-board.ps1 capacity` allows (never fewer than it adopts) and, with BL-1374, retires idle lanes down to it every 5 minutes, so the finding's reproduction run over shifts started after this change gives the expected result; `RunDarkFactory.ps1 -TestAutoLanes` passes its `fixed start` cases. (Reworded, as BL-1069 and BL-1070 were: see Notes.)
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: shift `20261003-145757` was started at 14:57 with a fixed `-Lanes 9`, before BL-1374
  (fixed-lane shifts retire idle lanes down to the board's capacity) landed at 15:10. A
  running shift keeps the script it started with, so all nine lanes polled "every ready task
  overlaps one in Doing" all shift; its coordinator log has no `lanes a -> b` capacity step.
- Remaining gap closed here: even with BL-1374, a fixed shift still started all N lanes and
  only trimmed them at the first 5-minute capacity step. `RunDarkFactory.ps1` now starts a
  fixed `-Lanes N` shift at `Get-FixedStartCount`: N capped by `task-board.ps1 capacity`
  (at least 1), never below the highest adopted lane; capacity steps add the rest, one per
  5 minutes, up to N (`$requestedLanes` keeps N). Five `-TestAutoLanes` cases cover it; the
  whole `-TestAutoLanes` suite passes.
- Criterion 1 reworded (decided by Claude): as filed it asks the reproduction to change its
  answer, but `Measure-FactoryProcess.ps1` lives under
  `Audit/`, which a lane may not read or run (guard hook), and it measures a finished
  shift's logs, so only a shift started with this script (or BL-1374's) and a re-audit by
  the process auditor can confirm it. The running shift needs `RunDarkFactory.cmd -Restart`
  to pick both fixes up.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Fixed -Lanes N shifts start no more lanes than the board's capacity, so lanes no longer open a shift waiting on overlapping touches
