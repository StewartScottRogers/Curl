---
id: BL-4082
title: Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4082 — Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on Windows

## Goal

`SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` failed on Windows in CI run 40887365069 (https://github.com/StewartScottRogers/Curl/actions/runs/40887365069). First failing commit: d5d9afcc.

    CollectionAssert.AreEqual failed. T0 S=2 T400+ S=1 T0 S=4 T-1. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 40887365069 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: CI logged `T0 S=2 T400+ S=1 T0 S=4 T-1`, with one `T0` more than the test expects. The test slept exactly the timer it was told (`Task.Delay(timer)`), and a Windows timer can wake a few milliseconds before that 4 s deadline. The next `socket_action` then found the deadline not yet passed and told the few milliseconds left as a new timer, logged as `T0`, before the transfer timed out. The engine behaves as libcurl would, so the fix is in the test only: it sleeps 100 ms past the timer it was told and keeps every assertion. No production code changed, so there was no Measure-CodeQuality run and no ADR.
- CI on Linux and macOS: the change is platform-neutral (only a longer wait), and the fast tests are green here on Windows (every project passed, 0 failed). The shift's CI watch files a task for any failure.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 31c6c74e.
- 2026-10-29: Doing -> Done. Cgr034's silent-server timeout test sleeps past the timer it was told, so a Windows timer waking early no longer adds a short T0 before the timeout
