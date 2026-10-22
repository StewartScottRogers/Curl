---
id: BL-3105
title: Make Remove-TaskStashEntries drop by hash so a failed split never drops another lane's stash
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskStash.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3105 — Make Remove-TaskStashEntries drop by hash so a failed split never drops another lane's stash

## Goal

`Remove-TaskStashEntries` drops only the stash entries whose hashes it was given, even when another lane pushes or drops a stash while it runs.

## Context

Found by the shift-end review (ADR-0710) of 1c1f9cb05...ea67815c6, by silent-failure-hunter, type-design-analyzer and code-reviewer independently.

`DarkFactory/TaskStash.ps1:109` (`Remove-TaskStashEntries`, new with BL-3095/BL-3101) reads `git stash list --format=%H` once and then runs `git stash drop stash@{$i}` by the indexes of that snapshot, with the output sent to `Out-Null`. The stash list is shared by every lane worktree, and `Save-StrayChanges` (`ShiftRunner.ps1:177`, `:191`, `:205`; `Claim.ps1:57`) pushes outside the integrate lock, so holding the lock does not keep other lanes out. If another lane stashes between the list and the drop, every index moves by one, and `Invoke-TaskSplit`'s failure path (`TaskStash.ps1`, `if ($stored.Count) { & $DropStash $stored }`) drops another lane's only copy of its unfinished work and leaves its own copies behind, with nothing traced. BL-3103 fixed this same race in `Restore-TaskStash` in the same shift (`Find-StashIndex` plus a `rev-parse` check).

## Acceptance criteria

- [x] `Remove-TaskStashEntries` finds each hash's index afresh (`Find-StashIndex`) and checks with `git rev-parse stash@{<i>}` that it still names that hash immediately before each drop, looking again when it does not.
- [x] A failed `git stash drop` is traced in red with git's message, not sent to `Out-Null`.
- [x] A `-TestTaskStash` case with real git pushes another stash between the lookup and the drop (an injected hook, as BL-3103's `-BetweenLookupAndDrop` does) and proves the other stash survives and only the listed hashes are gone.
- [x] `RunDarkFactory.ps1 -TestTaskStash` passes.
## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 86040f37 5946f3c8 f32dbf2b 95ad9451.
- 2026-10-22: Doing -> Done. Remove-TaskStashEntries looks each hash up afresh and verifies before dropping; -TestTaskStash passes
