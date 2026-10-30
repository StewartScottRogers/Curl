---
id: BL-4369
title: Fix CI failure ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7 on macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4369 — Fix CI failure ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7 on macOS and Windows

## Goal

`ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7` failed on macOS and Windows in CI run 41128846186 (https://github.com/StewartScottRogers/Curl/actions/runs/41128846186). First failing commit: aa0b1331.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 41128846186 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `ConnectAsync_WithAnOpenerOnAClosedPort_AsksOnceTellsOnceThatTheSocketClosedAndFailsWithExit7` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause (CI run 41128846186, macOS: OperationTimedOut after 7.8 s instead of CouldntConnect): BL-4368 held the refused port with a bound, never-listening socket, but macOS (BSD) drops a SYN that reaches a socket in CLOSED state instead of answering with a reset, so the connect timed out. Only the macOS job appears in that run's failed log. Fix: `ClosedPortBelowTheEphemeralRange()` picks a random loopback port from 20000 to 31999, checks that it can be bound, and releases it. The port lies below every platform's ephemeral range (Linux 32768+, Windows and macOS 49152+), so a parallel test listening on port 0 can never be handed it (the race BL-4368 fixed stays fixed), and with nothing bound there every OS refuses the connect. Both uses in SocketOpenerTcpDialerTests.cs changed.
- Rebase onto origin/work/dark-factory: BL-4370 had already fixed the same macOS timeout with `RefusedLoopbackPort` (the port is held as the client end of a connected loopback pair, which every platform refuses, and no parallel test can be handed it). That shared fix is kept in SocketOpenerTcpDialerTests.cs, and `ClosedPortBelowTheEphemeralRange()` was dropped as a second fix for the same problem.
- TcpConnectorRefusedConnectTests.cs has the same `BoundUnlistenedSocket()` helper and fails the same way on macOS; that is BL-4370's test, so it is left to BL-4370 (the same helper fits there).
- Test-only change, so no Measure-CodeQuality run (no production code changed). Curl.Networking.UnitTests fast run: 3979 passed, 0 failed, 35 skipped. CI on Linux and macOS: the change is platform-neutral (a connect to a loopback port with nothing bound is refused on all three); the shift's CI watch files any failure.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: b61f4010.
- 2026-10-30: Doing -> Done. Closed-port tests use a port with nothing bound below every ephemeral range, so macOS refuses the connect instead of timing out
