---
id: BL-3474
title: Check the shift-end review's dedupe and renumber commit before pushing
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/ShiftEnd.ps1]
requirement: none
created: 2026-10-24
completed:
---
# BL-3474 — Check the shift-end review's dedupe and renumber commit before pushing

## Goal

A failed `task-board.ps1 dedupe`, `git add` or `git commit` in `Invoke-ReviewDedupe` is traced and stops the push of the shift-end review's commits. The review's tasks can then never reach the branch with IDs that clash with other tasks.

## Context

The shift-end review of 2026-10-24 (silent-failure-hunter, diff `808c067b4...31527f6ba`) found this. The problem is in `DarkFactory/ShiftEnd.ps1:116-134`, in `Invoke-ReviewDedupe`, which `Push-ReviewCommitsUnderLock` calls:

- The dedupe call runs with `2>&1` and keeps only the lines that match `->`. It never checks `$LASTEXITCODE`.
- So when dedupe fails (a bad `-Since` ref, or a `Get-NextId` or `Move-Item` error), `$renumbered` comes back empty. The function then returns quietly, and the review's tasks are pushed with their clashing IDs. That is the AF-0386 / BL-3363 problem this function was added to stop, and the trace shows nothing.
- `git add -A Tasks` and `git commit` are not checked either. If the commit fails, the `renum` trace lines are still written and the push still goes ahead with the HEAD that was never renumbered.
- `Push-ReviewCommitsUnderLock` treats an empty return as success.

## Acceptance criteria

- [ ] `Invoke-ReviewDedupe` checks the exit code of the dedupe run, and traces its output when it fails.
- [ ] `Invoke-ReviewDedupe` checks the exit code of `git add` and `git commit`, and writes the `renum` trace lines only after the commit succeeds.
- [ ] When the dedupe or the commit fails, `Push-ReviewCommitsUnderLock` does not push, and returns a non-empty reason saying why.
- [ ] A `-Test...` self-test case in the dark factory's existing test entry point (or an equivalent script test) covers a dedupe that fails and a commit that fails.

## Notes

## Log

- 2026-10-24: Created.
