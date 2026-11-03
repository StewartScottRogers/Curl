---
id: BL-4597
title: Fix SocketIo conformance: SocketIo:RFC6455-5.5.1-1 answer a server's WebSocket Close with a Close
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary, Curl.Extended.Protocol.SocketIo.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4597 — Fix SocketIo conformance: SocketIo:RFC6455-5.5.1-1 answer a server's WebSocket Close with a Close

## Goal

Curl's socketio:// client answers a server's WebSocket Close frame with a Close frame echoing its status code before it ends.

## Context

RFC 6455 section 5.5.1: an endpoint that receives a Close frame and has not sent one must send a Close in response. WebSocketMessageChannel in Curl.Extended.Protocol.SocketIo.UnitLibrary ends on a Close run without answering it. Found by BL-4492's conformance run; the case is `SocketIoEngineIoCases.WebSocketClose_IsAnsweredWithAClose` in `Curl.Conformance.Extended.UnitTests/SocketIo/`, matrix `Documentation/Conformance/Extended/SocketIo.md`.

## Acceptance criteria

- [x] `SocketIoEngineIoCases.WebSocketClose_IsAnsweredWithAClose` passes as a plain case: the `SocketIo:RFC6455-5.5.1-1 BL-4597` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` and the known-failures check passes.
- [x] A focused unit test in `Curl.Extended.Protocol.SocketIo.UnitTests` pins the fix (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green, and the library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Delivered directly rather than through the full /feature stages: a one-class fix whose behaviour RFC 6455 section 5.5.1 fixes, so no ADR. `WebSocketMessageChannel` now answers the server's first Close with a Close carrying its first two payload bytes (the status code; empty when the server sent none) and never a second time; the reason text is not echoed (the section says an endpoint typically echoes the status code). The `KnownFailures.txt` line is removed and the conformance case passes plain.
- Unit tests: `WebSocketMessageChannelTests.ReceiveTextAsync_ACloseFrame_IsAnsweredOnceWithACloseEchoingItsStatusCode`, and the empty-close test now checks the empty Close answer. Build clean with `-warnaserror`, fast tests green, `Measure-CodeQuality.ps1` for the library: 0 failing members. Platform-neutral (no paths, OS text or certificates), so CI on Linux and macOS is covered by the green run here.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: e3f74df3 150bac4e ff8cc365.
- 2026-11-02: Doing -> Done. socketio:// answers a server's WebSocket Close with a Close echoing its status code
