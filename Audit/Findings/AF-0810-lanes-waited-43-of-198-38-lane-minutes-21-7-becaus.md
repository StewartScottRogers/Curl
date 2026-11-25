---
id: AF-0810
title: Lanes waited 43 of 198.38 lane minutes (21.7%) because every ready task overlapped one in Doing
auditor: process
severity: Medium
status: closed
reason: Re-audit 2026-11-16_0514.md: the reproduction no longer reproduces.
key: process:logs:overlap-wait:overlap-wait
reproduction: none
task: BL-5261
tasks: BL-5261
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed: 2026-11-16
closed-how: reliable-reaudit
closed-by: 2026-11-16_0514.md
---
# AF-0810 - Lanes waited 43 of 198.38 lane minutes (21.7%) because every ready task overlapped one in Doing

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261115-144752-L3.log:28`: Lanes waited 43 of 198.38 lane minutes (21.7%) because every ready task overlapped one in Doing.

## Evidence

Location: `logs/DarkFactory-20261115-144752-L3.log:28`

waitOverlapMinutes 43, laneMinutes 198.38. Wait lines 'No task can start yet: every ready task overlaps one in Doing' from L3 (15:09, 15:22 to 15:41), L4 (15:16 to 15:24), L5 (15:12 to 15:24), L2 (15:06). The coordinator then cut lanes from 5 to 1. The wait lines do not name the touches. Of the Done tasks, BL-5245 touches the whole Curl.Engine.UnitLibrary project; BL-5242 touches Curl.Conformance.LibTest.UnitLibrary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-15 -LogRoot <log folder> -CiRunsJson <log folder>\ci-runs.json -OutFile process.json; Select-String -Path <log folder>\DarkFactory-20261115-144752-L*.log -Pattern 'overlaps one in Doing'
```

- Expected: waitOverlapMinutes at most 20% of laneMinutes.
- Actual: 43 of 198.38 minutes, 21.7%.

## Re-audits

- 2026-11-16 | 2026-11-16_0514.md | reproduces: no | Ran Measure-FactoryProcess.ps1 -Since 2026-11-15 with the logs and ci-runs.json. waitOverlapMinutes=0 of laneMinutes=462.8 (0%), laneIdleMinutes=0. The window holds shifts that finished tasks (tasksDone=14). Select-String for 'overlaps one in Doing' found no match; DarkFactory-20261115-144752-L*.log does not exist in the copy, and no log holds the phrase.

## Log

- 2026-11-15: filed proposed.
- 2026-11-16: accepted -> closed. Re-audit 2026-11-16_0514.md: the reproduction no longer reproduces.
