---
id: BL-5261
title: Fix AF-0810: Lanes waited 43 of 198.38 lane minutes (21.7%) because every ready task overlapped one in Doing
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-15
completed:
---
# BL-5261 — Fix AF-0810: Lanes waited 43 of 198.38 lane minutes (21.7%) because every ready task overlapped one in Doing

## Goal

The cause of the factory behaviour the audit office reported as AF-0810 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0810 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0810-lanes-waited-43-of-198-38-lane-minutes-21-7-becaus.md`.

Location: `logs/DarkFactory-20261115-144752-L3.log:28`

Location: `logs/DarkFactory-20261115-144752-L3.log:28`

waitOverlapMinutes 43, laneMinutes 198.38. Wait lines 'No task can start yet: every ready task overlaps one in Doing' from L3 (15:09, 15:22 to 15:41), L4 (15:16 to 15:24), L5 (15:12 to 15:24), L2 (15:06). The coordinator then cut lanes from 5 to 1. The wait lines do not name the touches. Of the Done tasks, BL-5245 touches the whole Curl.Engine.UnitLibrary project; BL-5242 touches Curl.Conformance.LibTest.UnitLibrary.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-15 -LogRoot <log folder> -CiRunsJson <log folder>\ci-runs.json -OutFile process.json; Select-String -Path <log folder>\DarkFactory-20261115-144752-L*.log -Pattern 'overlaps one in Doing'
```

- Expected: waitOverlapMinutes at most 20% of laneMinutes.
- Actual: 43 of 198.38 minutes, 21.7%.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
