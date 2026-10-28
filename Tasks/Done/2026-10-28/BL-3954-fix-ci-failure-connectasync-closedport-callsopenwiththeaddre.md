---
id: BL-3954
title: Fix CI failure ConnectAsync_ClosedPort_CallsOpenWithTheAddressThenCloseOnceAndFailsWithCouldntConnect on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3954 — Fix CI failure ConnectAsync_ClosedPort_CallsOpenWithTheAddressThenCloseOnceAndFailsWithCouldntConnect on macOS

## Goal

`ConnectAsync_ClosedPort_CallsOpenWithTheAddressThenCloseOnceAndFailsWithCouldntConnect` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_ClosedPort_CallsOpenWithTheAddressThenCloseOnceAndFailsWithCouldntConnect` failed on macOS in CI run 40729087798 (https://github.com/StewartScottRogers/Curl/actions/runs/40729087798). First failing commit: 92f8b4f0.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40729087798 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `ConnectAsync_ClosedPort_CallsOpenWithTheAddressThenCloseOnceAndFailsWithCouldntConnect` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: macOS did not refuse a connect to a port held by a bound, never-listening socket (the reservation BL-3908 added), so the attempt timed out (`OperationTimedOut` after 7 s). The test now uses a port the system has just handed a socket and freed, as the refused-connect tests in `Curl.Networking.UnitTests` do, and those pass on macOS. Only test code changed, so no quality run was needed.
- CI criterion: the change is platform-neutral, and the build and fast tests pass here (Curl.Engine.UnitTests: 2706 passed, 7 skipped, solution fast run with no failures); the shift's CI watch files a task for any failure.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 011862b1.
- 2026-10-28: Doing -> Done. closed-port connect test is refused on macOS too: it uses a freed port, not a bound non-listening one
