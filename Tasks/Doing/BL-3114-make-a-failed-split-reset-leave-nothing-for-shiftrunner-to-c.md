---
id: BL-3114
title: Make a failed split reset leave nothing for ShiftRunner to commit on top of the split
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskStash.ps1, DarkFactory/ShiftRunner.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3114 — Make a failed split reset leave nothing for ShiftRunner to commit on top of the split

## Goal

When `Invoke-TaskSplit` cannot reset away its split commit, neither it nor its caller in `DarkFactory/ShiftRunner.ps1` commits or pushes anything on top of that commit, and the task does not stay in Doing.

## Context

Found by the shift-end review (ADR-0710) of 7fb0a59c7...18cf553c9. silent-failure-hunter and code-reviewer each found it. BL-3113 introduced it.

1. **The caller undoes BL-3113's guarantee.** `DarkFactory/TaskStash.ps1:299-302` (failure path) and `:325-331` (catch block) trace "its reset failed, so nothing more is committed or pushed on top of the split" and return `'failed'`.
   - `DarkFactory/ShiftRunner.ps1:196-201` treats `'failed'` like `'skipped'`. It moves the task to Blocked, runs `git add -A Tasks` and `git commit` on top of the split commit, and runs `git push` when not in a lane. This happens outside the integrate lock.
   - In a lane, `Invoke-Integrate` (`ShiftRunner.ps1:208`) pushes the block commit and the split commit together.
   - Result: the split's new tasks reach origin while their parent is Blocked with a "Stewart:" reason. The trace is false, and BL-3113's Notes ("only in the lane, unpushed") are wrong.
2. **A failed unstaging reset now skips the restore.** When the split commit itself failed, the reset is `reset -q -- Tasks`, which only unstages files: no split commit exists. Returning `'failed'` there skips `checkout -- Tasks` and `clean`. ShiftRunner's `git add -A Tasks` then commits the split's new task files together with the block.
3. **The header comment is malformed.** `TaskStash.ps1:237` has a literal `\n    #` inside one comment line, where two comment lines were meant.
4. **One check is untested.** The "board moves that all throw" test reaches only the catch block's state check. The failure path's check (`TaskStash.ps1:313`, `if ((& $TaskState $Id) -ne 'Blocked') ... return 'failed'`) has no test.

## Acceptance criteria

- [x] When a `reset --hard` of the split commit fails, `Invoke-TaskSplit` returns a result (for example `'reset-failed'`) for which `ShiftRunner.ps1` makes no commit and no push on top of the split commit. The task still does not stay in Doing, and the trace says what was left and why.
- [x] A failed `reset -q -- Tasks`, where no split commit exists, no longer returns early. The restore of Tasks (checkout and clean) still runs.
- [x] Every trace and comment about these paths is true of the code, the header comment of `Invoke-TaskSplit` included, which is two real `#` lines.
- [x] A `-TestTaskStash` case covers the failure path: the board stub returns normally while `-TaskState` stays `'Deferred'`, and `Invoke-TaskSplit` returns `'failed'`.
- [x] A `-TestTaskStash` case covers the reset-failed result: the caller's handling of it makes no `commit` and no `push`.
- [x] `RunDarkFactory.ps1 -TestTaskStash` passes.

## Notes

- Decision: a new result `reset-failed` (split commit stays, task stays Deferred as in it); `Test-TaskSplitLeavesBlockToCaller` is the caller's gate (blocks only skipped and failed). A failed unstaging reset (no commit) falls through to the restore.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: d04e8347 491dceb3 7cf0e0fa b5b58323 dc9fd233 95ad9451.
