---
id: AF-0810
title: Lanes waited 43 of 198.38 lane minutes (21.7%) because every ready task overlapped one in Doing
auditor: process
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: process:logs:overlap-wait:overlap-wait
reproduction: none
task: BL-5261
tasks: BL-5261
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-15: filed proposed.
