---
id: BL-4590
title: Fix SignalR conformance: close the WebSocket with a Close frame when the session ends
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Client, Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/SignalRWebSocketSession.cs, Curl.Extended.Protocol.SignalR.UnitTests/Client, Curl.Extended.Protocol.SignalR.UnitTests/Transport, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4590 — Fix SignalR conformance: close the WebSocket with a Close frame when the session ends

## Goal

When a SignalR session ends at the call's Completion or the hub's Close message, curl sends a WebSocket Close frame (RFC 6455 section 1.4 and 7.1.2) before closing the TCP connection.

## Context

Found by BL-4491: `SignalRWebSocketCases.CallCompleted_ClosesTheWebSocketWithACloseFrame` (`SignalR:RFC6455-1.4-1`) sees curl drop the connection with no Close frame. `SignalRWebSocketSession` answers the server's Close but never starts one; the session end in `SignalRHubSession` or `SignalRProtocolHandler` should send Close (status 1000) unless the server already closed.

## Acceptance criteria

- [x] The case carrying `SignalR:RFC6455-1.4-1` in `Curl.Conformance.Extended.UnitTests/SignalR/` passes.
- [x] A focused unit test in `Curl.Extended.Protocol.SignalR.UnitTests` pins the fix (ADR-0482).
- [x] The `SignalR:RFC6455-1.4-1` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` and the known-failures check passes.
- [x] `dotnet build` is clean, the fast tests are green and the library stays at 100% line and branch coverage (`Measure-CodeQuality.ps1`).

## Notes

- `SignalRWebSocketSession.CloseAsync` sends a masked Close with status 1000 unless the server already closed (its Close was answered); `SignalRHubSession.RunAsync` calls it when the session ends at the Completion, a Close message or the WebSocket close. On a failure (exit 22, transport or wire-format error) no Close is sent: the run is ending in error and the connection is disposed, kept simple.
- The client does not wait for the server's answering Close after sending its own: the transfer is over, and the connection is disposed at once (RFC 6455 section 7.1.1 lets the client close TCP after the handshake starts). Default chosen for simplicity.
- Checked: SignalR unit tests 119/119, Curl.Conformance.Extended.UnitTests 704/704 (the RFC6455-1.4-1 case passes and the known-failures check passes), full build 0 warnings, fast tests 115 projects passed, Measure-CodeQuality for the SignalR library: 0 failing members. Tests are platform-neutral (bytes only).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 6b0d8889 95af5e93 d48c076d 724857c1.
- 2026-11-02: Doing -> Done. curl sends a WebSocket Close (1000) when a SignalR session ends at Completion or a hub Close
