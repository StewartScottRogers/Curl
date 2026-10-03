---
id: AF-0041
title: Lanes spent 1544 of 2110 idle minutes (about 40% of lane time) waiting on overlapping touches
auditor: process
severity: Medium
status: proposed
reason:
key: process:logs:logs:overlap-wait
task: none
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
closed:
closed-by:
---
# AF-0041 - Lanes spent 1544 of 2110 idle minutes (about 40% of lane time) waiting on overlapping touches

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261003-145757-L4.log:2`: Lanes spent 1544 of 2110 idle minutes (about 40% of lane time) waiting on overlapping touches.

## Evidence

Location: `logs/DarkFactory-20261003-145757-L4.log:2`

waitOverlapMinutes=1544, laneIdleMinutes=2110, against roughly 3800 lane minutes (9 lanes, about 7h each). The wait message reads 'No task can start yet: every ready task overlaps one in Doing' (lanes 3 to 6 right after start). That is far over the 20% threshold. I did not identify the specific touches that serialised the lanes.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json) | Select-Object laneIdleMinutes,waitOverlapMinutes
```

- Expected: waitOverlapMinutes under 20% of lane time (under about 760).
- Actual: laneIdleMinutes 2110, waitOverlapMinutes 1544.

## Re-audits

## Log

- 2026-10-03: filed proposed.
