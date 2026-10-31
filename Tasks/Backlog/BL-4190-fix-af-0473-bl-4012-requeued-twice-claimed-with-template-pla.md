---
id: BL-4190
title: Fix AF-0473: BL-4012 requeued twice (claimed with template-placeholder criteria, then the run budget ran out with stashed work) and then Deferred
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-29
completed:
---
# BL-4190 — Fix AF-0473: BL-4012 requeued twice (claimed with template-placeholder criteria, then the run budget ran out with stashed work) and then Deferred

## Goal

The cause of the factory behaviour the audit office reported as AF-0473 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0473 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0473-bl-4012-requeued-twice-claimed-with-template-place.md`.

Location: `logs/DarkFactory-20261028-201954-L6.log:19`

Location: `logs/DarkFactory-20261028-201954-L6.log:19`

Requeue 1 at 2026-10-28 19:06:36: 'Waits on BL-4018 ...; Goal and criteria are still the template placeholders and must be written in Backlog' (DarkFactory-20261028-172341-L11.log:65). A task with no real criteria was claimable. Requeue 2 at 21:13:58: 'Run budget ran out; 3 quality-gate failures left in the stashed work' (DarkFactory-20261028-201954-L6.log:19). The task was then Deferred and split into BL-4057 and BL-4058. The tool reports claims=2, 73.4 min, cost $2.72.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-4012 (claim|REQUEUE)'
```

- Expected: Fewer than 2 requeues; no claim of a task whose criteria are template placeholders.
- Actual: BL-4012: 2 REQUEUE lines, outcome requeued, then Deferred.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-31 (interactive): measured with Measure-FactoryProcess.ps1 over a snapshot of Z:/repos/Curl.logs (files written since 2026-10-30 12:00; the one live run BL-4420-L1 left out as running) with a fresh 500-run CI list, at about 11:10 Arizona 2026-10-31. Since fix eb3e574f6 (ADR-1155, 10-30 08:40): tasksClaimedMoreThanOnce 0, requeues 4 (each on a task claimed once), no turn-cap stop and no split run in the coordinator logs, unfinishedRuns empty; 42 tasks done, median 12.0 min, $0.94 per task. The incident the finding names predates the fix.

- Fix eb3e574f6 (pushed to work/dark-factory 2026-10-30 about 08:40 Arizona, ADR-1155): a split for size files every part, the rest of the parent included, and Defers the parent instead of requeueing it; the deadline rule hands work back to be stashed instead of filing a task to wait on; a run stopped at the turn cap gets the one-shot split run at once; and a task whose log already shows a Doing -> Backlog requeue is split (or Blocked for triage) instead of being claimed a third time. Lanes load the factory at start, so measure from the first shift started after the push (the 14:07 hand-over at the latest). Measured before the fix (2026-10-29 to 2026-10-30 08:12): 61 REQUEUE lines, 25 split for size, 22 waits on another task (these already hold through depends-on), 5 out of time, 3 turn cap, 3 cost cap; claimed 3 times: BL-4170, BL-4124, BL-4132.

## Log

- 2026-10-29: Created.
