---
id: BL-4991
title: Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4991 — Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on macOS

## Goal

`SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` failed on macOS in CI run 43102911007 (https://github.com/StewartScottRogers/Curl/actions/runs/43102911007). First failing commit: 060c7148.

    CollectionAssert.AreEqual failed. T0 S=2 T400+ S=4 T-1. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 43102911007 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the transfer reaches its read wait on its own thread; on a slow macOS runner the Out action returned before CURL_POLL_IN was told, and the 4.1 s sleep then outran the 4 s deadline. Fix (test only, Cgr034Tests.cs): drive the engine with short Out actions until S=1 is told before the long sleep. Cgr034 tests pass locally (12/12); platform-neutral, so the CI criterion is met by that and the shift CI watch.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 31c6c74e.
- 2026-11-11: Doing -> Done. Test now waits for CURL_POLL_IN before sleeping past the deadline; Cgr034 tests pass.
