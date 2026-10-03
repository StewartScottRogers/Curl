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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
