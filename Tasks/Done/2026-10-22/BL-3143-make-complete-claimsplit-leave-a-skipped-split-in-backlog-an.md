---
id: BL-3143
title: Make Complete-ClaimSplit leave a skipped split in Backlog and abort a failed rebase
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Claim.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3143 — Make Complete-ClaimSplit leave a skipped split in Backlog and abort a failed rebase

## Goal

When a lane's claim offers a capped task for a split, an over-the-cap task that was already offered for a split today stays in Backlog. It never moves to Blocked for Stewart. A block push whose pull fails leaves no rebase half-done in the lane.

## Context

Found by the shift-end review of the shift merged at 5a29b06be. The code-reviewer and silent-failure-hunter agents both found it, with high confidence.

**Where:** `DarkFactory/Claim.ps1`, `Complete-ClaimSplit`, lines 187-207, called from `Invoke-Claim` at line 325.

**What is wrong:**

- `Complete-ClaimSplit` blocks the task whenever `Test-TaskSplitLeavesBlockToCaller` (`DarkFactory/TaskStash.ps1:233`) is true, and that includes `'skipped'`. It moves the task to Blocked with a "Stewart:" reason, then commits and pushes.
- `Invoke-TaskSplit` returns `'skipped'` when `Register-TaskSplitAttempt` finds today's marker in `$LogDir\splits`. Every lane of a shift shares that folder.
- `Invoke-Claim` offers every capped, cost-spent ready task for a split on every claim (line 269). So `'skipped'` is the normal answer when a task is offered a second time the same day. It is not a failure.
- `ShiftRunner.ps1` can block on `'skipped'` because there the task is in Doing and only that lane holds it. In the claim path the task is still in Backlog, where every lane sees it.

**Failure scenario:**

1. Lane A's claim offers BL-X for a split, writes the marker and releases the lock. Its split run then takes minutes.
2. Lane B claims, is offered BL-X, gets `'skipped'`, and pushes BL-X to Blocked for Stewart.
3. Lane A's split commit is then refused. Its `pull --rebase` conflicts on the same task file.

The split is lost and the task waits on Stewart for nothing. Even with one lane, a split that threw or left the task in Backlog has the next claim that day block it.

**A second defect on the same line:** the block push at line 203 is `push -or (pull --rebase -and push)`. Unlike `Invoke-TaskSplit` (`TaskStash.ps1:299`), it never runs `git rebase --abort` when the pull fails. A conflicting pull leaves the lane mid-rebase, and the next `Sync-Lane` cannot check out. The trace also says "blocked" without checking that the move happened.

## Acceptance criteria

- [x] `Complete-ClaimSplit` returns `''` and leaves the task in Backlog (no move, no commit, no push) when the split result is `'skipped'`. It still blocks on `'failed'`.
- [x] When the pull before the block push fails, `Complete-ClaimSplit` runs `git rebase --abort`, so the lane checkout is left with no rebase in progress.
- [x] `Complete-ClaimSplit` traces "blocked" only when the task is in `Blocked` after the move.
- [x] `RunDarkFactory.ps1 -TestClaimDuplicates` (or the self-test that covers `Complete-ClaimSplit`) has a case for `'skipped'` that leaves the task in Backlog, and a case for a refused pull that checks the rebase was aborted. Both pass.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Direct change in Claim.ps1: skipped now returns empty and leaves Backlog alone; failed blocks only if the task is in Blocked after the move; a failed pull runs rebase --abort. Self-test -TestClaimDuplicates has two new cases (all 9 pass). Build clean, fast tests green.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 275209c1 3f47980c 72ca8f20 383635a2 a0b49a29.
- 2026-10-22: Doing -> Done. Complete-ClaimSplit leaves a skipped split in Backlog and aborts a failed rebase
