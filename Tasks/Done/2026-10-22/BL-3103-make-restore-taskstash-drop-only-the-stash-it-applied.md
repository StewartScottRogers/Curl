---
id: BL-3103
title: Make Restore-TaskStash drop only the stash it applied
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskStash.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3103 — Make Restore-TaskStash drop only the stash it applied

## Goal

`Restore-TaskStash` drops the stash entry it applied and no other, even when another lane pushes a stash between the lookup and the drop.

## Context

Found by the shift-end review (ADR-0710) of `bc792b5be...2c76ddbde`. The type-design-analyzer, the silent-failure-hunter and the code-reviewer all reported it.

`Restore-TaskStash` (`DarkFactory/TaskStash.ps1:36-40`) finds the applied stash's index with one `git stash list`. It then runs `git stash drop stash@{$index}` as a separate command, outside the integrate lock (`ShiftRunner.ps1:139`).

Every lane worktree shares the same stash list. If another lane's `Save-StrayChanges` pushes a stash between those two commands:

- every index shifts down by one;
- `stash@{$index}` now names that other lane's stash;
- the drop deletes the work that lane has just saved.

This is the hazard the "never stash pop with lanes" rule exists to prevent.

The drop's exit code is also ignored. A drop that fails without a trace brings back the AF-0327 behaviour.

## Acceptance criteria

- [x] Just before dropping, `Restore-TaskStash` checks that `git rev-parse stash@{$index}` still equals the applied hash. When it does not, it looks the index up again or skips the drop.
- [x] A failed drop is written to the trace.
- [x] A `-TestTaskStash` case pushes another stash between the lookup and the drop, for example through a hook the test can inject, and checks that the other stash survives.
- [x] `RunDarkFactory.ps1 -TestTaskStash` passes.

## Notes

- Added Find-StashIndex and a -BetweenLookupAndDrop test hook on Restore-TaskStash; the drop re-checks rev-parse stash@{i} against the applied hash, re-looks-up on mismatch, and traces a failed drop.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: e7b7d54c 51bae7b1 3243c4f5 95ad9451.
- 2026-10-22: Doing -> Done. Restore-TaskStash drops only the stash it applied, re-checking the hash and tracing a failed drop
