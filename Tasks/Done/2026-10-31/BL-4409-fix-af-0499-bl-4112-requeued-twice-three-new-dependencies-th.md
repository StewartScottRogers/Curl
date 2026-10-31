---
id: BL-4409
title: Fix AF-0499: BL-4112 requeued twice (three new dependencies, then the 80-turn cap at $5.10) and then Deferred and split
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4409 — Fix AF-0499: BL-4112 requeued twice (three new dependencies, then the 80-turn cap at $5.10) and then Deferred and split

## Goal

The cause of the factory behaviour the audit office reported as AF-0499 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0499 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0499-bl-4112-requeued-twice-three-new-dependencies-then.md`.

Location: `logs/DarkFactory-20261029-140509-L5.log`

Location: `logs/DarkFactory-20261029-140509-L5.log`

L3 claimed it at 10:12:12 and requeued it at 10:18:11 ('Waits on BL-4196 (shared WebSocket codec), BL-4194 and BL-4195'). L5 claimed it at 15:16:55. That run took 81 turns, 54.7 min and $5.10, and was requeued at 16:25:53: 'Dark factory stopped at its 80-turn cap, so the task is too big for one run'. The split run split-BL-4112-20261029-164929-L7.jsonl (14 turns, $0.21) split it into BL-4294..BL-4297, and it was Deferred ('cost cap exceeded'). $5.81 was spent with no Done; the 80-turn run produced nothing that integrated.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4112 (claim|end|REQUEUE|split)'
```

- Expected: A task too big for one run is split before a full 80-turn run is spent on it.
- Actual: 2 REQUEUE (new dependencies; 80-turn cap), then split and Deferred.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-31 (interactive): measured with Measure-FactoryProcess.ps1 over a snapshot of Z:/repos/Curl.logs (files written since 2026-10-30 12:00; the one live run BL-4420-L1 left out as running) with a fresh 500-run CI list, at about 11:10 Arizona 2026-10-31. Since fix eb3e574f6 (ADR-1155, 10-30 08:40): tasksClaimedMoreThanOnce 0, requeues 4 (each on a task claimed once), no turn-cap stop and no split run in the coordinator logs, unfinishedRuns empty; 42 tasks done, median 12.0 min, $0.94 per task. The incident the finding names predates the fix.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Interactive measurement Criteria at claim: 87cd20bd 9742157d.
- 2026-10-31: Doing -> Done. Measured 2026-10-31: the finding's expected result holds since its fix landed (see Notes); build clean, fast tests 114 assemblies passed
