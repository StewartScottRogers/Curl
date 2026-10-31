---
id: BL-4368
title: Fix CI failure ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7 on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4368 — Fix CI failure ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7 on Windows

## Goal

`ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7` failed on Windows in CI run 41120068809 (https://github.com/StewartScottRogers/Curl/actions/runs/41120068809). First failing commit: aa0b1331.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 41120068809 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: `ClosedLoopbackPort()` bound a socket to an ephemeral port and closed it before the connect, so under method-level parallelism another test (one calling `Listen()`) could be handed the same port and accept the connect: exit 0 instead of 7. Fix: tests hold a bound, never-listening socket (`BoundUnlistenedSocket()`) for their whole run; a connect to it is refused on Windows, Linux and macOS, and no other socket can take the port. Applied to both uses in SocketOpenerTcpDialerTests.cs and the same helper in TcpConnectorRefusedConnectTests.cs. Test-only change, so no Measure-CodeQuality run (no production code changed).
- Curl.Networking.UnitTests fast run: 3980 passed, 0 failed, 34 skipped. CI on Linux and macOS: the change is platform-neutral (a refused connect to a bound, unlistened loopback port is the same on all three); the shift's CI watch files any failure.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: b61f4010.
- 2026-10-30: Doing -> Done. Refused-port tests hold the port with a bound unlistened socket, so a parallel test can no longer take it
