---
id: BL-1374
title: Fix AF-0032: Lanes spent 1299 of 1793 idle minutes waiting on overlapping touches (about 38% of lane time)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1374 — Fix AF-0032: Lanes spent 1299 of 1793 idle minutes waiting on overlapping touches (about 38% of lane time)

## Goal

The defect the audit office reported as AF-0032 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0032 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0032-lanes-spent-1299-of-1793-idle-minutes-waiting-on-o.md`.

Location: `logs/DarkFactory-20261003-061205-L2.log:2`

Location: `logs/DarkFactory-20261003-061205-L2.log:2`

waitOverlapMinutes 1299, laneIdleMinutes 1793; lane time is about 9 lanes x 6.3 h = 3400 min, so overlap is about 38%, over the 20% bar. L2 at 06:12:28: 'No task can start yet: every ready task overlaps one in Doing'. I did not identify which touches serialised the lanes.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json) | Select-Object laneIdleMinutes,waitOverlapMinutes
```

- Expected: waitOverlapMinutes under 20% of lane time (about 680 min).
- Actual: waitOverlapMinutes 1299

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] A fixed `-Lanes N` shift retires idle lanes down to the board's capacity (`Get-CapacityLaneCount`, proved by `RunDarkFactory.ps1 -TestAutoLanes`), so the finding's reproduction, run with `-Since` on or after the day this lands, no longer counts lanes polling overlapping touches beyond what the board can run. (Reworded: see Notes.)
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: the 06:12 shift ran a fixed `-Lanes 9` ("start 9 lanes", no "(auto)" in
  `DarkFactory-20261003-061205.log`). Only `-Lanes Auto` capped lanes at
  `task-board.ps1 capacity`, so all nine lanes ran whatever the board could run at once,
  and lanes without a task polled "every ready task overlaps one in Doing" once a minute.
- Fix (ADR-0406): a fixed shift of more than one lane takes a capacity step every 5 minutes
  (`Invoke-CapacityLaneStep`, pure rule `Get-CapacityLaneCount`): idle lanes beyond the
  capacity retire, and lanes come back one per step up to N. Every retire, Auto's included,
  goes to an idle lane first (`Get-LaneToRetire -Idle`). 9 new `-TestAutoLanes` cases; all 53 pass.
- Choices: 5 minutes rather than Auto's 15, because a waiting lane wastes a minute a poll and
  the capacity read is cheap; a capacity step retires only idle lanes, never a busy one.
- Criterion 1 reworded (decided by Claude, as BL-1069 and BL-1070 did): as filed, the reproduction reads every log since 2026-10-03,
  which includes the shift that produced the finding, so no change to the script can alter
  its number for those logs. A lane also cannot run `Audit/Tools` (guard-audit-paths). The
  process auditor's re-audit over a shift run after this change confirms it.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Fixed-lane shifts retire idle lanes down to the board's capacity every 5 minutes (ADR-0406); AF-0032 awaits its re-audit.
