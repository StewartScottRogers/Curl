---
id: BL-3146
title: Keep an exception or stray output in the merge-wait poll from aborting the shift-end merge
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/ShiftEnd.ps1, DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3146 — Keep an exception or stray output in the merge-wait poll from aborting the shift-end merge

## Goal

A `-Continuous` shift whose merge-wait board poll throws or writes to the pipeline still waits out CI and merges to master, and a failed hand-over can be tried again on the next poll.

## Context

The shift-end review of the 2026-10-22 shift (ADR-0710) found this. Two reviewers found it independently: pr-review-toolkit:code-reviewer and pr-review-toolkit:silent-failure-hunter. BL-3142 introduced it.

- `DarkFactory/ShiftEnd.ps1`, `Wait-MergeCiRun`: the line `if ($OnPoll -and (Get-Date) -ge $nextPoll) { ...; & $OnPoll }` has no try/catch, and its output is not discarded.
- `DarkFactory/ShiftCoordinator.ps1`, `$pollDuringMerge`: the poll runs `git pull`, `Get-ClaimableTaskId`, `Get-IdlePollDecision` and `& $startNextShift ... -Beside`. The last one calls `Start-Detached`, which can throw. For example, `HerdrTabs.ps1:28` pipes herdr's output to `ConvertFrom-Json`.
- What goes wrong:
  - An exception escapes `Wait-MergeCiRun` and `Invoke-MergeToMaster`, so the merge is abandoned even when CI is green. Before BL-3142 the CI wait had no such failure source.
  - The coordinator then also skips the final CI watch, the review and closing the tab.
  - `$handOver.Done = $true` is set before `$startNextShift` runs. If the start throws, no next shift exists, but no later poll tries again.
  - Anything the poll writes to the pipeline becomes part of `Wait-MergeCiRun`'s return value. `$wait.Failure` and `$wait.Run` would then read the wrong object.

## Acceptance criteria

- [ ] `Wait-MergeCiRun` runs `-OnPoll` inside a try/catch that traces the exception and goes on waiting. It discards the poll's output (`| Out-Null`).
- [ ] `$handOver.Done` becomes true only after `$startNextShift` returns, so a hand-over that throws is tried again on the next poll.
- [ ] A `-TestCiWatch` / `-TestHeartbeat` (or equivalent self-test) case covers both:
  - a throwing `-OnPoll`: the wait still returns the run;
  - an `-OnPoll` that writes output: `Wait-MergeCiRun` still returns a single hashtable whose `.Run` is the run.
- [ ] All the `RunDarkFactory.ps1 -Test*` self-tests that the change touches pass.

## Notes

## Log

- 2026-10-22: Created.
