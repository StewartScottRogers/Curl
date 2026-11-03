---
id: BL-4598
title: Fix SocketIo conformance: SocketIo:EIO-Heartbeat-2 end the session when no ping arrives within pingInterval + pingTimeout
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary, Curl.Extended.Protocol.SocketIo.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4598 — Fix SocketIo conformance: SocketIo:EIO-Heartbeat-2 end the session when no ping arrives within pingInterval + pingTimeout

## Goal

Curl's socketio:// client ends the session when no Engine.IO ping arrives within the handshake's pingInterval + pingTimeout, on the injected TimeProvider.

## Context

Engine.IO v4 heartbeat: the client must consider the connection closed when the server's ping (2) does not arrive within pingInterval + pingTimeout from the open packet. SocketIoSession today ignores both fields and waits until -m. Read them from the open packet and run the timer on the transfer's TimeProvider; choose the exit code by ADR (Decided by Claude under Stewart's delegation) and keep the case's assertion (the run ends before -m, not with exit 28). Found by BL-4492's conformance run; the case is `SocketIoEngineIoCases.MissedPing_EndsTheConnectionAfterPingIntervalPlusPingTimeoutOnTheInjectedClock` in `Curl.Conformance.Extended.UnitTests/SocketIo/`, matrix `Documentation/Conformance/Extended/SocketIo.md`.

## Acceptance criteria

- [x] `SocketIoEngineIoCases.MissedPing_EndsTheConnectionAfterPingIntervalPlusPingTimeoutOnTheInjectedClock` passes as a plain case: the `SocketIo:EIO-Heartbeat-2 BL-4595` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` and the known-failures check passes.
- [x] A focused unit test in `Curl.Extended.Protocol.SocketIo.UnitTests` pins the fix (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green, and the library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Done directly (small, one-class change) rather than through the full /feature agent chain, to stay within the run's budget.
- `SocketIoSession` reads `pingInterval` + `pingTimeout` from the open packet and waits for each ping on the transfer's `TimeProvider` (a `CancellationTokenSource` on that clock, restarted by each ping); a missed ping ends with exit 56 (ADR-1210, decided by Claude under Stewart's delegation). A handshake without both fields sets no heartbeat.
- Unit tests: `SocketIoSessionTests` (ManualClock fake, `ScriptedConnection.WaitsAtEnd`). Conformance known-failure line removed; Curl.Conformance.Extended.UnitTests 778 passed. Measure-CodeQuality for the library: 0 failing members. Full `dotnet build Curl.slnx -warnaserror` clean, fast tests green. Platform-neutral (no paths, OS text or certificates).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 1a5677c6 150bac4e ff8cc365.
- 2026-11-02: Doing -> Done. socketio:// ends with exit 56 when no ping arrives within pingInterval + pingTimeout on the transfer's clock
