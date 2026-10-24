---
id: BL-3452
title: Fix AF-0405: BL-3315 requeued twice, each time for missing prerequisite work, then deferred over its cost cap and split
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-24
completed:
---
# BL-3452 — Fix AF-0405: BL-3315 requeued twice, each time for missing prerequisite work, then deferred over its cost cap and split

## Goal

The cause of the factory behaviour the audit office reported as AF-0405 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0405 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0405-bl-3315-requeued-twice-each-time-for-missing-prere.md`.

Location: `logs/DarkFactory-20261023-110315-L7.log:14`

Location: `logs/DarkFactory-20261023-110315-L7.log:14`

Claim 1, L5 07:12:57 (sonnet, 40 turns, 22 min): REQUEUE 07:35:46 'Split: OS_ERRNO needs Abstractions/Networking/Engine/Http work, filed as BL-3328 and BL-3329'. Claim 2, L7 11:06:57 (opus retry, 45 turns, 18 min): REQUEUE 11:32:02 'Waits on BL-3342 (PoolingConnector drops LastSocketError) and BL-3343'. At 17:14:50 and 17:15:20 L3 and L4 logged it 'unclaimable cost cap 2.35 of 3.07'. L4 split it at 17:16:38 into 'BL-3424, BL-3425'. At 17:16:23 BL-3234's split had already taken those same IDs, and L2 renumbered the duplicates to BL-3428/BL-3429 at 17:20:59. Cause: twice claimed before the code it depended on existed. Measured cost $2.35 (5.3x the median).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-*.log -Pattern 'BL-3315 (claim|REQUEUE|split|unclaimable)|BL-342[45] -> '
```

- Expected: BL-3315 claimed once its prerequisites were done, with no REQUEUE
- Actual: two claims, two REQUEUEs for missing prerequisites, unclaimable over cap, split IDs that collided with BL-3234's and were renumbered

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause for BL-3315: two claims each found only prerequisite work (BL-3328/BL-3329, then BL-3342/BL-3343), added it to depends-on and requeued with no code. Both runs' cost ($2.35 in all) came off the cap, so the claim after them was unclaimable at $2.35 of $3.07 and the task was split. Under the fix both runs are waived, so the claim that does the work runs with a whole cap. The second requeue happened because the first run missed a prerequisite. Only the prompt's one-pass rule prevents that; the fix takes away its cost-cap consequence. The colliding split IDs (BL-3424/BL-3425) are the claim/TaskIds area, which another agent is working on.
- Fix: 9c6c5732f "fix(factory): a run that only finds prerequisites does not spend the cost cap (ADR-0811)", landed on origin/work/dark-factory 2026-10-24 08:49 Arizona (with 8bf4e3854, which corrects a stale -TestModelChoice case). It covers AF-0387, AF-0404, AF-0405 and AF-0406 (BL-3364, BL-3451, BL-3452, BL-3453), which share one cause: a first claim found only prerequisite work and requeued with no code, and AF-0095's daily rule took its cost off the cap of the claim that did the work, so that claim stopped at the cap with its work stashed, or was unclaimable, and was split.
- Now `Register-PrerequisiteRequeueRun` (DarkFactory/TaskBudget.ps1, called in DarkFactory/ShiftRunner.ps1 before Save-StrayChanges) writes `<ID>-<stamp>[-L<n>].prerequisite-requeue.txt` beside the run log and traces `waive` when the task is in Backlog, nothing is left to stash, no stash of the task waits, and its depends-on names an ID not Done. `Get-TaskRunSpends` skips such runs. The lane prompt (DarkFactory/Prompts.ps1) says so, puts a Doing task holding a needed file into depends-on, and has tasks filed together name the ones they extend in depends-on.
- Verified 2026-10-24: dotnet build clean; fast tests green (46 test projects); RunDarkFactory.ps1 -TestTaskBudget (7 new cases), -TestTaskStash, -TestTaskIds, -TestModelChoice, -TestClaimDuplicates and -TestTaskReadiness pass.
- To measure once a shift has run: the finding's measurement over Z:\repos\Curl.logs from 2026-10-24 08:49. BL-3315 is now Deferred, so check the pattern for any task: every `waive` trace line, no task `unclaimable` or split over its cost cap after prerequisite REQUEUEs, and how many tasks still have two prerequisite REQUEUEs. No shift had run since the fix when this was written, so the task waits in Backlog.

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Interactive session: fixing the prerequisite-requeue cost-cap mechanism (ADR-0811) Criteria at claim: 87cd20bd 9742157d.
- 2026-10-24: Doing -> Backlog. Fix 9c6c5732f (ADR-0811) landed 2026-10-24 08:49; waits for a shift to run since then so the live-log measurement can be taken (ADR-0761)
