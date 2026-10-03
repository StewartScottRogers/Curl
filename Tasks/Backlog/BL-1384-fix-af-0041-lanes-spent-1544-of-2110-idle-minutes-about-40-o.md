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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
