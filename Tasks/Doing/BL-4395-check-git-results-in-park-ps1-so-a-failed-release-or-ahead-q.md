---
id: BL-4395
title: Check git results in Park.ps1 so a failed release or ahead query never orphans or requeues a task
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Park.ps1]
requirement: none
created: 2026-10-31
completed:
---
# BL-4395 — Check git results in Park.ps1 so a failed release or ahead query never orphans or requeues a task

## Goal

Invoke-ReleaseClaim reports success only when the task really left Doing, and Get-LaneBranchesAhead never treats a failed git query as "no work".

## Context

Shift-end review 2026-10-31 (silent-failure-hunter). DarkFactory/Park.ps1:78-96: the board move and the git commit go to Out-Null unchecked; if the move fails, the commit fails, the push of an unchanged HEAD succeeds and the function returns an empty string (released) while the task stays orphaned in Doing - the case BL-4347 meant to prevent. Park.ps1:71-76: both git calls use 2>$null, so a missing origin/<shift branch> or a failed fetch makes every lane branch count 0 ahead, and Get-OrphanAction now answers backlog where it used to refuse, so committed work on factory/lane-N is requeued and redone.

## Acceptance criteria

- [x] Invoke-ReleaseClaim returns a failure reason when the task is still in Doing after the push (checked with Get-TaskState or the pushed tree), shown by a self-test.
- [x] Get-LaneBranchesAhead reports an unknown result when git exits non-zero, and Get-OrphanAction refuses rather than requeues on it, shown by a self-test.
- [x] The factory's -Test* self-tests pass under Windows PowerShell and pwsh.

## Notes

- Invoke-ReleaseClaim checks Get-TaskState after the move and every git step, retrying then reporting CLAIM NOT RELEASED. Get-LaneBranchesAhead returns a marker entry on any git failure; Get-OrphanAction already refuses on a non-empty list. Two new -TestPark cases; -TestPark passes under pwsh and Windows PowerShell.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 083e959f c94a812f caed2003.
