---
id: BL-3113
title: Make Invoke-TaskSplit's throw handler stop when its reset fails and return blocked only when the task is Blocked
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskStash.ps1, DarkFactory/ShiftRunner.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3113 — Make Invoke-TaskSplit's throw handler stop when its reset fails and return blocked only when the task is Blocked

## Goal

When an automatic split fails or throws, `Invoke-TaskSplit` never commits or pushes on top of a split commit it could not remove. It returns `blocked` only when the task really is in Blocked.

## Context

Found by the shift-end review (ADR-0710) of 585348e22...432525fe2, by silent-failure-hunter. BL-3107 and BL-3110 introduced it.

1. **A failed reset is ignored.** `DarkFactory/TaskStash.ps1:287-296` is the new `catch` block in `Invoke-TaskSplit` (BL-3110).
   - It wraps each undo step in `$undo`, which catches throws. But `$Git` is `Invoke-Git` (`Claim.ps1:7`), which returns `$false` on failure and never throws, so a failed `reset -q --hard` (for example, `index.lock` held) goes unnoticed.
   - HEAD is then still the split commit. `checkout -- Tasks` restores the split's own board, the stash copies are dropped, and the block commit is made on top of the split. Outside `-LeaveForIntegration` both commits are pushed.
   - Result: new tasks that say "Continues the stashed work", with their stash copies gone.
   - This contradicts the block's own comment that it leaves "no unpushed split commit behind".
   - The non-throw failure path has the same unchecked result for its resets (`$rebasedSplit~1` and `$headBefore`, around line 266, BL-3107).
2. **`blocked` is returned even when the task was not blocked.** `TaskStash.ps1:292-298` traces "threw: ...; blocked" and returns `'blocked'` even when the `$Board` move to Blocked failed inside `$undo`.
   - The caller (`ShiftRunner.ps1`, around line 195) treats any result other than `'skipped'` as handled, so it never runs its own Blocked move.
   - The task is left in Doing with no reason recorded, while the trace says it was blocked.

## Acceptance criteria

- [x] In both the failure path and the `catch` block of `Invoke-TaskSplit`, a `reset` whose `$Git` call returns `$false` stops the undo: no checkout of Tasks, no block commit and no push are made on top of the split commit. The failure is traced in red.
- [x] `Invoke-TaskSplit` returns `'blocked'` only when `Get-TaskState` (or an injected equivalent) reports the task in Blocked. Otherwise it returns a value its caller in `ShiftRunner.ps1` falls back on, and that fallback blocks the task itself.
- [x] A `-TestTaskStash` case in which `$Git` returns `$false` for `reset` proves that no `commit` or `push` follows it.
- [x] A `-TestTaskStash` case in which every `$Board` move throws proves that `Invoke-TaskSplit` does not return `'blocked'`.
- [x] `RunDarkFactory.ps1 -TestTaskStash` passes.

## Notes

- Decision: a failed reset or a move that did not reach Blocked returns `failed`; ShiftRunner.ps1 treats `failed` like `skipped` and blocks the task itself (touches widened to DarkFactory/ShiftRunner.ps1 for that one line; no other Doing task names it that I checked in this checkout). After a failed reset that fallback still commits on the unreset split commit, but only in the lane, unpushed, and the task is never left in Doing.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: e260ea31 eaeb4a51 ad9d3f85 7a083912 95ad9451.
- 2026-10-22: Doing -> Done. Invoke-TaskSplit stops on a failed reset and returns blocked only when the task is Blocked
