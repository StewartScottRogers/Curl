---
id: BL-3364
title: Fix AF-0387: BL-3204 requeued twice: first blocked by BL-3199 holding ManagedEasyTransfer.cs, then out of its cost cap with 7 Engine tests failing
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-23
completed: 2026-10-25
---
# BL-3364 — Fix AF-0387: BL-3204 requeued twice: first blocked by BL-3199 holding ManagedEasyTransfer.cs, then out of its cost cap with 7 Engine tests failing

## Goal

The cause of the factory behaviour the audit office reported as AF-0387 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0387 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0387-bl-3204-requeued-twice-first-blocked-by-bl-3199-ho.md`.

Location: `logs/DarkFactory-20261023-005934-L2.log:15`

Location: `logs/DarkFactory-20261023-005934-L2.log:15`

First claim, L7 at 22:48:46: requeued at 22:52:55 after 19 Opus turns ($0.86) and no code, because 'The fix needs Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, and BL-3199, which is in Doing, holds that file' (BL-3204-20261022-192710-L7.jsonl). Second claim, L2 at 01:00:14 as an Opus retry: requeued at 01:07:44 after 38 turns ($1.75), 'Out of this run's cost cap with 7 Engine unit tests still failing'. Its uncommitted work was stashed (L2.log:13). At 03:51:46 L1 found it unclaimable over its cost cap ($2.60 of $3.72) and split it into BL-3271..BL-3273. BL-3271 (L2, 03:54:18 to 04:21:49, $2.91) landed the stashed work. Its first test run, at 03:55:48, shows the same 7 failures (2376/2383), so the stash was recovered and no work was lost. Total: $5.5 and 4 runs for one change.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-*.log -Pattern 'BL-3204 (claim|REQUEUE|stash|split|unclaimable)'
```

- Expected: At most one requeue for BL-3204.
- Actual: Two REQUEUE lines (22:52:55 L7 and 01:07:44 L2), then a cost-cap split at 03:53:43.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause for BL-3204: its first claim ($0.86) found BL-3199 in Doing holding ManagedEasyTransfer.cs, widened touches and requeued with depends-on left empty. That $0.86 came off the second claim's cap, and the second claim ran out with $2.60 of $3.72 spent, its work stashed, and was split. The prompt now has such a run add the holding task to depends-on, which also makes the run waived.
- Fix: 9c6c5732f "fix(factory): a run that only finds prerequisites does not spend the cost cap (ADR-0812)", landed on origin/work/dark-factory 2026-10-24 08:49 Arizona (with 8bf4e3854, which corrects a stale -TestModelChoice case). It covers AF-0387, AF-0404, AF-0405 and AF-0406 (BL-3364, BL-3451, BL-3452, BL-3453), which share one cause: a first claim found only prerequisite work and requeued with no code, and AF-0095's daily rule took its cost off the cap of the claim that did the work, so that claim stopped at the cap with its work stashed, or was unclaimable, and was split.
- Now `Register-PrerequisiteRequeueRun` (DarkFactory/TaskBudget.ps1, called in DarkFactory/ShiftRunner.ps1 before Save-StrayChanges) writes `<ID>-<stamp>[-L<n>].prerequisite-requeue.txt` beside the run log and traces `waive` when the task is in Backlog, nothing is left to stash, no stash of the task waits, and its depends-on names an ID not Done. `Get-TaskRunSpends` skips such runs. The lane prompt (DarkFactory/Prompts.ps1) says so, puts a Doing task holding a needed file into depends-on, and has tasks filed together name the ones they extend in depends-on.
- Not covered mechanically: a second prerequisite requeue, where a first run missed a prerequisite. The prompt's one-pass rule still has to prevent that. Each such run is now waived, so it no longer pushes the working claim over the cap.
- Verified 2026-10-24: dotnet build clean; fast tests green (46 test projects); RunDarkFactory.ps1 -TestTaskBudget (7 new cases), -TestTaskStash, -TestTaskIds, -TestModelChoice, -TestClaimDuplicates and -TestTaskReadiness pass.
- To measure once a shift has run: the finding's measurement over Z:\repos\Curl.logs from 2026-10-24 08:49. BL-3204 is now Deferred, so check the pattern for any task: every `waive` trace line, and no task with a requeue for a held file or prerequisite followed by a cost-cap REQUEUE, an `unclaimable` cost-cap line or a cost-cap split. No shift had run since the fix when this was written, so the task waits in Backlog.
- Measured 2026-10-25 (interactive session, ADR-0761). Fix 9c6c5732f with 8bf4e3854, landed 2026-10-24 08:49 Arizona. Window: 2026-10-24 08:49:00 Arizona to the logs' end. It holds the shifts DarkFactory-20261024-185147, 20261025-021056, 035926, 074620 and 081939, which finished 37 tasks. 203407 and 203504 were refused at start.
  - Commands: `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500 > <scratchpad>\ci-runs.json`, then `powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since '2026-10-24 08:49:00' -LogRoot Z:\repos\Curl.logs -CiRunsJson <scratchpad>\ci-runs.json -OutFile <scratchpad>\process-prereq.json`. Then `Select-String` over those DarkFactory-*.log files for `waive|REQUEUE|unclaimable|split|cost cap|stash`, and a listing of `*.prerequisite-requeue.txt` in the log folder.
  - Numbers: requeues 1, tasksClaimedMoreThanOnce 0, all 39 task rows claims = 1. The one REQUEUE is BL-3468 (L1 19:10:34 in 185147, "Interactive only"), which is not a prerequisite or cost-cap requeue. The logs hold no `waive` line, no `.prerequisite-requeue.txt` file, no `unclaimable` line, and no cost-cap REQUEUE or split. Three `stash uncommitted work kept` lines were written, none after a cost cap: BL-3468 (then REQUEUE interactive only), BL-3469 (then BLOCKED on Stewart) and BL-3471 (then DONE), all in 185147. So no task had a prerequisite requeue followed by a cost-cap outcome, and none had two prerequisite requeues.
  - No prerequisite requeue occurred, so the waive path was not exercised in this window. The criterion's expected result holds. The process auditor's re-audit is what closes the finding.

## Log

- 2026-10-23: Created.
- 2026-10-24: Backlog -> Doing. Interactive session: fixing the prerequisite-requeue cost-cap mechanism (ADR-0811) Criteria at claim: 87cd20bd 9742157d.
- 2026-10-24: Doing -> Backlog. Fix 9c6c5732f (ADR-0811) landed 2026-10-24 08:49; waits for a shift to run since then so the live-log measurement can be taken (ADR-0761)
- 2026-10-24: ADR-0811 named above for this fix was renumbered ADR-0812 (two ADRs took 0811 at once).
- 2026-10-25: Backlog -> Doing. Interactive session: measure the live logs after fix 9c6c5732f and 8bf4e3854 (ADR-0761) Criteria at claim: 87cd20bd 9742157d.
- 2026-10-25: Doing -> Done. Live logs after 9c6c5732f (from 2026-10-24 08:49, 5 shifts, 37 tasks done): 1 requeue (interactive only), no prerequisite requeue, no cost-cap requeue, unclaimable line or split
