---
id: AF-0032
title: Lanes spent 1299 of 1793 idle minutes waiting on overlapping touches (about 38% of lane time)
auditor: process
severity: Medium
status: closed
reason: Re-audit 2026-10-07_0844.md: the reproduction no longer reproduces.
key: process:logs:overlap-waits:overlap-wait
task: BL-1374
found: 2026-10-03
found-at: d1db9881553d55cd92c0e74bb41561c3dea9ea84
scorecard: 2026-10-03_1233.md
closed: 2026-10-07
closed-by: 2026-10-07_0844.md
---
# AF-0032 - Lanes spent 1299 of 1793 idle minutes waiting on overlapping touches (about 38% of lane time)

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261003-061205-L2.log:2`: Lanes spent 1299 of 1793 idle minutes waiting on overlapping touches (about 38% of lane time). Reported by an auditor flagged unreliable in 2026-10-03_1233.md.

## Evidence

Location: `logs/DarkFactory-20261003-061205-L2.log:2`

waitOverlapMinutes 1299, laneIdleMinutes 1793; lane time is about 9 lanes x 6.3 h = 3400 min, so overlap is about 38%, over the 20% bar. L2 at 06:12:28: 'No task can start yet: every ready task overlaps one in Doing'. I did not identify which touches serialised the lanes.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json) | Select-Object laneIdleMinutes,waitOverlapMinutes
```

- Expected: waitOverlapMinutes under 20% of lane time (about 680 min).
- Actual: waitOverlapMinutes 1299

## Re-audits

- 2026-10-03 | 2026-10-03_1459.md | reproduces: yes | The tool gives laneIdleMinutes=2110 and waitOverlapMinutes=1544, not 1793 and 1299. The defect (overlap waits at about 40% of lane time) persists with different numbers.
- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | Ran the reproduction: laneIdleMinutes 837, waitOverlapMinutes 782, laneMinutes 4135.32. Overlap is 18.9% of lane time, under the 20% bar; the finding's 1299 of 1793 (about 38%) no longer appears. Caveat: the log folder no longer holds the DarkFactory-20261003-061205 shift, so the measured period differs from the original.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
- 2026-10-07: accepted -> closed. Re-audit 2026-10-07_0844.md: the reproduction no longer reproduces.
