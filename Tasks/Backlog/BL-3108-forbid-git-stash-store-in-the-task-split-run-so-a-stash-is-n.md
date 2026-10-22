---
id: BL-3108
title: Forbid git stash store in the task-split run so a stash is never stored twice
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Prompts.ps1, DarkFactory/TaskStash.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3108 — Forbid git stash store in the task-split run so a stash is never stored twice

## Goal

The task-split run cannot run `git stash store`, so the shift's `Copy-TaskStash` is the only thing that ever stores a split's stash under a new task's ID.

## Context

Found by the shift-end review of 2026-10-22 (code-reviewer, ADR-0710) in the diff 67f6ff17b...3894d9c9b.

- `DarkFactory/Prompts.ps1:434-435`: `$SplitForbidden` lists `git stash push/save/apply/pop/drop/clear/create/branch` but not `git stash store`. The split prompt (`Prompts.ps1:450-452`) says "Run no git stash subcommand" yet still shows the hash ("the shift then stores the stash ({SHA})"), so a run following the old step 3 can still run `git stash store -m "darkfactory BL-x <stamp>" <sha>`.
- `Get-TaskStashSha` (`DarkFactory/TaskStash.ps1:91`) now matches the bare-subject entry `git stash store` writes, so the run's entry and `Copy-TaskStash`'s entry both name the new task. `Restore-TaskStash` applies and drops only the newest, so the next claim re-applies the same work and is told it "did not apply cleanly" (AF-0327 again); and on a failed split `Remove-TaskStashEntries` drops only `$stored`, leaving the run's entry behind.
- The self-test around `TaskStash.ps1:509` was weakened to the list without `store`.

## Acceptance criteria

- [ ] `$SplitForbidden` in `DarkFactory/Prompts.ps1` contains `'git stash store'`.
- [ ] The `-TestTaskStash` (or prompts) self-test asserts that `git stash store` is in `$SplitForbidden`, and the self-test passes.
- [ ] The split prompt no longer invites the run to store the stash itself (it says the shift stores it and the run runs no `git stash` subcommand).

## Notes

## Log

- 2026-10-22: Created.
