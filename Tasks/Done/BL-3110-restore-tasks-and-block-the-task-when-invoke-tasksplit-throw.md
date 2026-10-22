---
id: BL-3110
title: Restore Tasks and block the task when Invoke-TaskSplit throws during a claim
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Claim.ps1, DarkFactory/TaskStash.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3110 — Restore Tasks and block the task when Invoke-TaskSplit throws during a claim

## Goal

A task split that throws during a claim leaves no half-changed `Tasks` folder or unpushed split commit behind, and the task is blocked with the exception text so Stewart sees it.

## Context

Found by the shift-end review of 2026-10-22 (silent-failure-hunter, ADR-0710) in the diff 67f6ff17b...3894d9c9b.

- `DarkFactory/Claim.ps1:286-289` (new in this diff) wraps `Invoke-TaskSplit` in `try { ... } catch { Write-Trace ... 'Red' }` so a throw no longer replaces the claim result.
- `Register-TaskSplitAttempt` has already written the day's marker, so the task is not split again that day and is not blocked either: it sits in Backlog, refused at every claim, with one Red trace line.
- `Invoke-TaskSplit`'s `try` has only a `finally`: a throw after the commit or reset (for example from `$Board` or `$DropStash`) leaves uncommitted `Tasks` changes or an unpushed split commit, which the next claim's `Save-StrayChanges 'sync'` silently stashes.

## Acceptance criteria

- [x] When `Invoke-TaskSplit` throws, the catch in `DarkFactory/Claim.ps1` (or a catch inside `Invoke-TaskSplit`) removes the split's own commit if one was made, restores `Tasks` (`checkout`/`clean`), drops any stash copies it stored, and moves the task to `Blocked` with a reason naming the exception.
- [x] A self-test with a `-Board` or `-DropStash` stub that throws shows the task ends `Blocked`, `git status --short -- Tasks` is empty, and the claim result is still returned; the self-test passes.

## Notes

- Catch added inside Invoke-TaskSplit (DarkFactory/TaskStash.ps1, added to touches): each undo (reset of the split commit if unpushed, drop stash copies, checkout/clean Tasks) runs on its own, then the task is blocked with the exception text. Claim.ps1 already returns the claim result. Self-test (-TestTaskStash) passes with throwing DropStash and Board stubs.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 211f9b20 2d8c670c.
- 2026-10-22: Doing -> Done. Invoke-TaskSplit throws now restore Tasks and block the task with the exception
