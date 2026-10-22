---
id: BL-3112
title: Make the renumbered task's stash move happen only after the renumber is pushed, move only that task's own copy, and never end the lane when it fails
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Claim.ps1, DarkFactory/TaskStash.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3112 — Make the renumbered task's stash move happen only after the renumber is pushed, move only that task's own copy, and never end the lane when it fails

## Goal

When a duplicate task ID is renumbered, the split's stash copy moves to the new ID only once the renumber is on origin. Only the renumbered task's own copy moves. A failed move is traced and never ends the lane.

## Context

Found by the shift-end review (ADR-0710) of 585348e22...432525fe2. silent-failure-hunter and code-reviewer each found it on their own. BL-3109 introduced all three defects.

1. **The move runs before the push.** `DarkFactory/Claim.ps1:101-106` (`Invoke-RenumberDuplicates`) calls `Move-RenumberedTaskStash` after the local renumber commit but before `push`.
   - If the push is refused, the function returns false, and the next claim attempt's `Sync-Lane` hard-resets the renumber away.
   - The shared stash list is not reset, so the copy stays under the new ID, which origin never saw.
   - The duplicate is then renumbered again, possibly to a different ID. That task gets no stash. A later, unrelated task that takes the orphaned ID is offered the parent's work by `Restore-TaskStash`.
2. **A throw ends the lane.** `Copy-TaskStash` now throws on any git failure (`DarkFactory/TaskStash.ps1:103-111`); its own comment says another lane may hold the stash ref lock.
   - Nothing catches the throw: not `Move-RenumberedTaskStash`, not `Invoke-RenumberDuplicates`, not the claim loop at `Claim.ps1:269`.
   - The exception reaches the lane's catch in `ShiftRunner.ps1`, which stops the lane with a script error. The task is not in Doing, so the lane does not resume it.
   - Left behind: an unpushed renumber commit, and stashes moved for only some lines of `$renumbered`.
3. **Every copy under the old ID moves, not just this task's.** `DarkFactory/TaskStash.ps1:139-150` (`Move-RenumberedTaskStash`) moves every bare `darkfactory <old> <stamp>` copy.
   - The default `-ContinuesStash` only checks that the renumbered file has a "Continues the stashed work of" line. It never checks whose work it is.
   - Duplicate IDs arise exactly this way: two lanes split different parents at once, and each files a new task under the same ID, each with its own copy.
   - When one of the two is renumbered, both copies move. The task that kept the old ID loses its stash, and the renumbered one may get the other parent's work.

## Acceptance criteria

- [ ] `Invoke-RenumberDuplicates` calls `Move-RenumberedTaskStash` only after the renumber push has returned true. When the push is refused, no stash entry changes.
- [ ] A throw from `Move-RenumberedTaskStash` (or `Copy-TaskStash` under it) is caught in `Invoke-RenumberDuplicates`, traced in red with the IDs and git's message, and the claim carries on. The copy stays under the old ID.
- [ ] `Move-RenumberedTaskStash` moves only the copy that belongs to the renumbered task. It reads the parent ID from the task's "Continues the stashed work of BL-<n>" line, and matches the copy to that parent, for example by comparing the copy's tree with the parent stash's tree, or by putting the parent ID in the copy's subject when `Copy-TaskStash` writes it.
- [ ] A `-TestTaskStash` case with real git stores two copies under one ID from two different parents. It renumbers one task and proves that only its own copy moves and that the other task keeps its copy.
- [ ] A `-TestTaskStash` case proves that a refused renumber push leaves the stash list unchanged.
- [ ] `RunDarkFactory.ps1 -TestTaskStash` passes.

## Notes

## Log

- 2026-10-22: Created.
