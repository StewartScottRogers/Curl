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
completed:
---
# BL-4590 — Fix SignalR conformance: close the WebSocket with a Close frame when the session ends

## Goal

When a SignalR session ends at the call's Completion or the hub's Close message, curl sends a WebSocket Close frame (RFC 6455 section 1.4 and 7.1.2) before closing the TCP connection.

## Context

Found by BL-4491: `SignalRWebSocketCases.CallCompleted_ClosesTheWebSocketWithACloseFrame` (`SignalR:RFC6455-1.4-1`) sees curl drop the connection with no Close frame. `SignalRWebSocketSession` answers the server's Close but never starts one; the session end in `SignalRHubSession` or `SignalRProtocolHandler` should send Close (status 1000) unless the server already closed.

## Acceptance criteria

- [ ] The case carrying `SignalR:RFC6455-1.4-1` in `Curl.Conformance.Extended.UnitTests/SignalR/` passes.
- [ ] A focused unit test in `Curl.Extended.Protocol.SignalR.UnitTests` pins the fix (ADR-0482).
- [ ] The `SignalR:RFC6455-1.4-1` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` and the known-failures check passes.
- [ ] `dotnet build` is clean, the fast tests are green and the library stays at 100% line and branch coverage (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 6b0d8889 95af5e93 d48c076d 724857c1.
