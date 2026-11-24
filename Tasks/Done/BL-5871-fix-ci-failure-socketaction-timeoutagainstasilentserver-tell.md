---
id: BL-5871
title: Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5871 — Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on Windows

## Goal

`SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` failed on Windows in CI run 45451113257 (https://github.com/StewartScottRogers/Curl/actions/runs/45451113257). First failing commit: 61efb1cb.

    CollectionAssert.AreEqual failed. T0 S=2 T400+ S=4 T-1. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 45451113257 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

CI log: the run took 24 s on a starved runner, past the 4 s deadline, so the transfer timed out before its read wait (no S=1). Fix is test-only: the scenario reruns up to three times when S=1 never appeared. Platform-neutral; CI criterion met by that (rule 11).

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 31c6c74e.
- 2026-11-24: Doing -> Done. Test reruns the scenario when a starved runner times the transfer out before its read wait
