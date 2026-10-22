---
id: BL-3099
title: Keep Invoke-Claim's claim result when a split after the claim throws
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Claim.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3099 — Keep Invoke-Claim's claim result when a split after the claim throws

## Goal

When a split that runs after a successful claim throws an exception, the exception is written to the trace and `Invoke-Claim` still returns the task it claimed.

## Context

Found by the shift-end review (ADR-0710) of `bc792b5be...2c76ddbde`. The silent-failure-hunter reported it.

`Invoke-Claim` runs `Invoke-TaskSplit` for each due split inside its `finally` block (`DarkFactory/Claim.ps1:284`), with no try/catch. In PowerShell, an exception thrown in a `finally` block replaces the try block's return value.

For example, `Process.Start` can fail because `claude` is not on PATH, or a git stub can throw. When that happens:

- the claim has already been pushed to Doing, but its result never reaches the lane;
- the lane dies before it runs the task;
- `Register-TaskSplitAttempt` has already written its marker (`TaskStash.ps1:145`), so no split of that task is tried again that day.

## Acceptance criteria

- [ ] Each `Invoke-TaskSplit` call in `Invoke-Claim`'s `finally` block is wrapped so that an exception is written to the trace and the next due split still runs.
- [ ] `Invoke-Claim` returns its claim result when a split throws, and a factory self-test with a split stub that throws shows this.
- [ ] The factory self-test that covers `Claim.ps1` passes.

## Notes

## Log

- 2026-10-22: Created.
