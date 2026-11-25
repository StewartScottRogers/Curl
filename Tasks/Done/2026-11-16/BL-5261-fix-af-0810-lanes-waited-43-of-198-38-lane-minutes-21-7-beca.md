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
completed: 2026-11-16
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

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: the audit task filer (Audit/Tools/New-TasksFromAcceptedFindings.ps1) named a whole project as each task's 	ouches; it now names the file (audit branch 1d71f0bc4a, merged to master in PR #359), and BL-5249..BL-5260 were narrowed to their files in 9d2713e6d0, pushed 2026-11-15 20:41 Arizona.
- Window: shifts 20261115-204203 and 20261115-215641 (2026-11-15 20:42 to 22:42 Arizona), the live Z:\repos\Curl.logs, CI runs from a fresh gh run list.
- Measure-FactoryProcess.ps1 -Since '2026-11-15 20:42': waitOverlapMinutes 0 of laneMinutes 410.38 (0%), against at most 20%; no "overlaps one in Doing" line in any lane log. 12 lanes ran the 12 tasks at once.
- Build and fast tests: this task changed only audit tooling and task files; CI's Build and test job passed on Windows, Linux and macOS for PR #359's head 1d71f0bc4a.

## Log

- 2026-11-15: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-16: Doing -> Done. Measured 0 of 410 lane minutes waiting on overlap after the touches fix (PR #359, 9d2713e6d0)
