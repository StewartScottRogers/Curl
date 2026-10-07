---
id: AF-0041
title: Lanes spent 1544 of 2110 idle minutes (about 40% of lane time) waiting on overlapping touches
auditor: process
severity: Medium
status: closed
reason: Re-audit 2026-10-07_0844.md: the reproduction no longer reproduces.
key: process:logs:logs:overlap-wait
task: BL-1384
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
closed: 2026-10-07
closed-by: 2026-10-07_0844.md
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

- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | Ran the reproduction: laneIdleMinutes 837, waitOverlapMinutes 782 of 4135.32 lane minutes (18.9%), not the 1544 of 2110 (about 40%) the finding reports. Same caveat as AF-0032: the 2026-10-03 06:12 shift's logs are no longer in the folder.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
- 2026-10-07: accepted -> closed. Re-audit 2026-10-07_0844.md: the reproduction no longer reproduces.
