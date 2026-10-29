---
id: BL-4050
title: Fix CI failure SocketActionAsync_WrapperOfAManagedTransfer_IsToldTheSocketOfItsOwnSocketWait on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4050 — Fix CI failure SocketActionAsync_WrapperOfAManagedTransfer_IsToldTheSocketOfItsOwnSocketWait on macOS

## Goal

`SocketActionAsync_WrapperOfAManagedTransfer_IsToldTheSocketOfItsOwnSocketWait` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SocketActionAsync_WrapperOfAManagedTransfer_IsToldTheSocketOfItsOwnSocketWait` failed on macOS in CI run 40828309078 (https://github.com/StewartScottRogers/Curl/actions/runs/40828309078). First failing commit: 3e9937e7.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40828309078 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `SocketActionAsync_WrapperOfAManagedTransfer_IsToldTheSocketOfItsOwnSocketWait` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: CI showed (Ok, 1) after the readable action. The action waits at most `SocketWaitSettleLimit` (200 ms) for the woken wrapped transfer to run on to its end, and on the loaded macOS runner its thread-pool continuation took longer (the test ran 545 ms). Fix (test only): the test sets the limit to 30 s, as Cgr034Tests does, so it waits for the transfer to end rather than counting on Windows scheduling. The production 200 ms default stays as it is: it bounds how long an action blocks. No production code changed, so no quality run was needed.
- CI criterion: the change is platform-neutral (no paths, OS errors or certificates) and Curl.Engine.UnitTests is green here (2885 passed); ticked on that basis, as lane rule 11 says.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 6c38a7f0.
- 2026-10-28: Doing -> Done. Test waits up to 30 s for the woken transfer to end instead of the 200 ms settle default
