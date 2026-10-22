---
id: BL-3091
title: Split a task that hit its cost cap with one bounded headless run
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3090]
touches: [DarkFactory/TaskStash.ps1, DarkFactory/Claim.ps1, DarkFactory/ShiftRunner.ps1, DarkFactory/Integrate.ps1, DarkFactory/Prompts.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3091 — Split a task that hit its cost cap with one bounded headless run

## Goal

When a lane finds a task over its cost cap, or a run is cut off by the cap, the factory runs one bounded headless split run that splits it into smaller tasks, so its dependency chain is not stalled until a human splits it.

## Context

Evidence: the 2026-10-21 20:04 shift ended with BL-2970, BL-2925, BL-2834, BL-2881 and BL-2966 all capped. An interactive session split them (commit d1aa8a5a3): BL-3011..BL-3023 were filed, each original's stash was re-stored under the new IDs with `git stash store`, the originals went to Deferred, and dependents were repointed. This task automates exactly that.

Where it hooks in: `Invoke-Claim` (`DarkFactory/Claim.ps1` ~line 224-225) skips an over-cap task with the trace "its runs of the last day already spent its cost cap; not claimed"; `DarkFactory/ShiftRunner.ps1` (~lines 129-137 and 186-193) blocks a task whose run "stopped at its ... US dollar cost cap ... so split the task" (`Test-BudgetSpent`, `Get-TaskCapSpentWhy` in `TaskBudget.ps1`); `Save-StrayChanges` and `Restore-TaskStash` live in `DarkFactory/TaskStash.ps1`. Add a function (for example `Invoke-TaskSplit` in `TaskStash.ps1`) that, once per task per day (record the attempt in the lane state or a marker file in the log folder, test-able with an injected clock), launches one `claude -p --model sonnet` run built like `Invoke-TaskRun` (`DarkFactory/TaskRun.ps1` ~line 147: `New-ClaudeRunStartInfo`, `--max-budget-usd` small such as 1, `--max-turns` small, the same `--disallowedTools`) with a prompt from `DarkFactory/Prompts.ps1` that tells it to: file the smaller tasks with `task-board.ps1 new`, re-store the original's stash under each new ID with `git stash store`, move the original to Deferred with the reason, repoint dependents' `depends-on`, and touch nothing else. The commit and push of those Tasks changes run under the integrate lock (`Enter-Lock`, `DarkFactory/Integrate.ps1`/`Claim.ps1`) like other lane pushes. If the split run fails (non-zero exit, no new tasks filed, or push fails), the task is moved to Blocked with the reason. Never use the stash with `pop` (stashes are shared across lane worktrees; apply by hash).

BL-3090 (dependency) adds the shared claimable-now predicate this reuses to detect capped tasks.

Write an ADR under `Documentation/Planning/Decisions/` ("Decided by Claude under Stewart's delegation") for the once-a-day limit and the budget chosen.

## Acceptance criteria

- [x] A split function and its prompt exist (`DarkFactory/TaskStash.ps1`, `DarkFactory/Prompts.ps1`), and both `Claim.ps1` (over-cap skip) and `ShiftRunner.ps1` (run stopped at the cap) call it.
- [x] `RunDarkFactory.ps1 -TestTaskStash` (extended) passes with injected-clock cases: first capped sighting of a task today runs the split; a second the same day does not; the next day does again.
- [x] The same self-test shows the launch command line uses `--model sonnet`, a `--max-budget-usd` of at most 2 and a `--max-turns` cap, and the prompt names `git stash store`, Deferred and `depends-on` repointing.
- [x] The same self-test shows a failed split run (injected result) leads to a Blocked move whose reason names the failure; a successful one does not Block.
- [x] The commit and push of the split run happen inside `Enter-Lock` (visible in the code and a self-test of the call order).
- [x] No `stash pop` is used anywhere in the new code.
- [x] An ADR in `Documentation/Planning/Decisions/` records the decision.
- [x] All `RunDarkFactory.ps1 -Test*` switches that passed before still pass.

## Notes

- Delivered directly rather than through the full /feature stages: the change is four DarkFactory scripts and their self-test, and the run's cost cap is 3 US dollars.
- Invoke-TaskSplit, Invoke-TaskSplitRun, Register-TaskSplitAttempt, Get-TaskSplitArguments and Get-TaskStashSha (factored out of Restore-TaskStash) are in TaskStash.ps1; SplitPrompt and SplitForbidden in Prompts.ps1 (ADR-0758).
- Invoke-Claim queues an over-cap task (cost cap, not interactive only) and splits it in its finally block after the lock is released, so the minutes-long split run never holds every lane's claim lock; Invoke-TaskSplit takes the lock again only to commit and push.
- Default taken: in ShiftRunner a refused push leaves the split commit for the shift's integration rather than pulling, since the lane may hold its own commits there; from a claim it pulls with rebase and pushes again.
- Default taken: 1 US dollar, 30 turns, 20 minutes; one marker file per task per day under <log>/splits.
- -TestTaskStash: 27 PASS; all 23 RunDarkFactory.ps1 -Test* switches exit 0.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: eb81d285 b1cf7e4a dab410e4 3adf2d41 406b27f7 04e7bc94 de7e55ae 7193fd91.
- 2026-10-22: Doing -> Done. A task over its cost cap is split by one bounded Sonnet run once a day, from the claim's over-cap skip and a run stopped at the cap
