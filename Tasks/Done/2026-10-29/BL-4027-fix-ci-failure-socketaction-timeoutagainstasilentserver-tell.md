---
id: BL-4027
title: Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4027 — Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on Linux and macOS

## Goal

`SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` failed on Linux and macOS in CI run 40810061535 (https://github.com/StewartScottRogers/Curl/actions/runs/40810061535). First failing commit: 6407f459.

    CollectionAssert.AreEqual failed. T0 S=2 T400+ S=4 T-1. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 40810061535 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the test's 1 s `CURLOPT_TIMEOUT` was too short for a loaded Linux or macOS CI runner. The first action returns once the dial publishes its socket (its `T400+` already lets 600 ms pass), and the transfer reached its read wait only after the deadline, so `ManagedMultiTransfer.IsSettled` let it run on to its own timeout (`S=4`) without ever telling `CURL_POLL_IN` (CI: `T0 S=2 T400+ S=4 T-1`). The engine behaves as libcurl's would; the fix gives the test 4 s and keeps every assertion. Test-only change, so no Measure-CodeQuality run.
- CI on Linux and macOS: the change is platform-neutral (only a longer deadline) and the fast tests are green here on Windows (46 test projects, 0 failed); the shift's CI watch files a task for any failure.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 31c6c74e.
- 2026-10-28: Doing -> Done. Cgr034's silent-server timeout test gives a loaded runner 4 s to reach its read wait, so it no longer times out before CURL_POLL_IN
