---
id: BL-3115
title: Make the claim path block a capped task whose split returns failed
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Claim.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3115 — Make the claim path block a capped task whose split returns failed

## Goal

When the claim path's split of a capped task returns `'failed'` or `'reset-failed'`, the claim acts on that result as `ShiftRunner.ps1` does, rather than throwing it away.

## Context

Found by the shift-end review (ADR-0710) of 5619f1500...466ddef0f. silent-failure-hunter found it; the code-reviewer noted the same line.

- BL-3113 and BL-3114 (this diff) gave `Invoke-TaskSplit` two new results:
  - `'failed'`: the split failed and the task is not in Blocked, so the caller blocks it itself.
  - `'reset-failed'`: the split commit could not be reset, so it stays.
  - `Test-TaskSplitLeavesBlockToCaller` (`DarkFactory/TaskStash.ps1`) is the caller's gate.
- `DarkFactory/ShiftRunner.ps1:196` honours it. The other caller, the claim's `finally` block at `DarkFactory/Claim.ps1:286`, still runs `Invoke-TaskSplit $due.Id $due.Why -PullOnRefusal | Out-Null`.
- **Scenario for `'failed'`:** a Backlog task over its spent cap is split. The split run exits non-zero and the board move to Blocked throws or does not take. The task stays in Backlog, unblocked and with no "Stewart:" reason. Its split is offered again only the next day.
- **Scenario for `'reset-failed'`:** only the claim path passes `-PullOnRefusal`, so only it can reach `'reset-failed'` after a refused push. When it does, the unpushed split commit and its stash copies are left for the next `Sync-Lane` reset (`Claim.ps1:60`) to discard, and only one Red trace line records it.

## Acceptance criteria

- [x] `DarkFactory/Claim.ps1` keeps the result of each `Invoke-TaskSplit` call. When `Test-TaskSplitLeavesBlockToCaller` is true, it moves the task to Blocked with a "Stewart:" reason, commits and pushes, under the integrate lock.
- [x] A `'reset-failed'` result in the claim path is either pushed (the split commit is valid) or its stored stash copies are dropped, and is traced saying which. No stash copy is left under an ID that origin never saw.
- [x] A case in `RunDarkFactory.ps1 -TestClaimDuplicates` (or a new claim self-test) covers both results with a stubbed `Invoke-TaskSplit`.
- [x] `RunDarkFactory.ps1 -TestClaimDuplicates` and `-TestTaskStash` pass.

## Notes

- Added `Complete-ClaimSplit` to Claim.ps1: it acts on each split result under the integrate lock. 'skipped'/'failed' move the task to Blocked (Stewart: reason), commit, push (one pull --rebase retry). 'reset-failed' is pushed if it can be (the split commit is valid); otherwise the stash copies stored under the new IDs (read from the commit subject) are dropped. Both are traced. Decision: the split commit is pushed first because it is a valid board change; dropping is the fallback.
- The claim loop could list one capped task twice in `splitDue` after a retry (renumber); it now lists it once.
- Found: Invoke-TaskSplit's `[scriptblock]$Board` parameter shadows the board script path Invoke-Board reads, so its default board call cannot work; filed BL-3116. Complete-ClaimSplit calls Invoke-Board directly for that reason.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 3a2803c4 389e394e 56ec3e3b 7779796a.
- 2026-10-22: Doing -> Done. the claim path blocks a task whose split failed and pushes or drops a reset-failed split's stash copies
