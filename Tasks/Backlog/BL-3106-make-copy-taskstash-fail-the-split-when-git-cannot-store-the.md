---
id: BL-3106
title: Make Copy-TaskStash fail the split when git cannot store the copy, and trace a stash no new task continues
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskStash.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3106 — Make Copy-TaskStash fail the split when git cannot store the copy, and trace a stash no new task continues

## Goal

A split whose stash copy git did not store is not reported as `split`, and a split that leaves a stash no new task continues says so in the trace.

## Context

Found by the shift-end review (ADR-0710) of 1c1f9cb05...ea67815c6, by silent-failure-hunter (also raised by type-design-analyzer).

`DarkFactory/TaskStash.ps1:97` (`Copy-TaskStash`) never checks `$LASTEXITCODE` after `git rev-list`, `git commit-tree` or `git stash store` (the last sent to `Out-Null`). If `stash store` fails (for example another lane holds the stash ref lock) or `commit-tree` returns nothing, it still returns a hash or '', `Invoke-TaskSplit` (`TaskStash.ps1:204`) adds it to `$stored`, commits and returns `split`, and defers the original. The new task's Notes say it continues the stashed work, but no `darkfactory <new id>` stash exists, so `Restore-TaskStash` finds nothing and the work stays only under the Deferred ID that no lane claims.

The same strands the work silently when the split run writes no "Continues the stashed work of {ID}." line (`$ContinuesStash` finds none): `$sha` is set, `$stored` is empty, and nothing is traced.

## Acceptance criteria

- [ ] `Copy-TaskStash` throws, or returns '' that `Invoke-TaskSplit` treats as a failure, when any of its git commands exits non-zero; the split then goes down its failure path (drops what it stored, blocks the task) with a reason naming the store failure.
- [ ] When the task had a stash and no new task continues it, `Invoke-TaskSplit` writes a red trace naming the stash hash and the original ID.
- [ ] `-TestTaskStash` cases cover a failing store (injected) and a split with a stash and no continuing task.
- [ ] `RunDarkFactory.ps1 -TestTaskStash` passes.
## Notes

## Log

- 2026-10-22: Created.
