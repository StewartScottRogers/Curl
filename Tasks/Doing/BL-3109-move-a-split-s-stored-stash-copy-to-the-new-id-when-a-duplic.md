---
id: BL-3109
title: Move a split's stored stash copy to the new ID when a duplicate task ID is renumbered
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Claim.ps1, DarkFactory/TaskStash.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3109 — Move a split's stored stash copy to the new ID when a duplicate task ID is renumbered

## Goal

When a task filed by an automatic split is renumbered as a duplicate ID, its stored stash copy follows it to the new ID, and no stash is ever offered to a different task that kept the old ID.

## Context

Found by the shift-end review of 2026-10-22 (silent-failure-hunter, ADR-0710) in the diff 67f6ff17b...3894d9c9b.

- `Invoke-TaskSplit` (`DarkFactory/TaskStash.ps1`, new in this diff) stores the parent's stash as `darkfactory <newId> <stamp>` via `Copy-TaskStash` for every new task whose file says "Continues the stashed work of <Id>".
- The split run takes minutes outside the claim lock and takes IDs from the lane's local board. If another lane files the same `BL-n` meanwhile, the split's `pull --rebase` succeeds (different file names), the split commit lands after the other lane's, and `Invoke-RenumberDuplicates` (`DarkFactory/Claim.ps1:91-103`) renumbers the split's subtask as the later one.
- The stash copy stays under `BL-n`, so `Restore-TaskStash` later applies it to the other lane's unrelated task and tells that run to "carry on from that work", with no trace; the renumbered subtask loses its stash.

## Acceptance criteria

- [ ] When `Invoke-RenumberDuplicates` renumbers a task `BL-n` -> `BL-m`, any `darkfactory BL-n <stamp>` stash entry belonging to the renumbered task is re-stored as `darkfactory BL-m <stamp>` and the old entry is dropped by hash; or `Restore-TaskStash` applies a stash to a task only when that task's file says "Continues the stashed work of" its source (or the stash was saved by that task's own run).
- [ ] A self-test in `DarkFactory/Claim.ps1` or `DarkFactory/TaskStash.ps1` pins the duplicate-renumber case and passes.
- [ ] The trace says when a stash is moved to a renumbered ID.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 9630b876 0ec3b444 8bcd5d04.
