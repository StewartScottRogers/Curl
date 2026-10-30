---
id: BL-4296
title: WebSocketExtendedConnect: open and run a WebSocket over HTTP/3 (RFC 9220)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4196, BL-4194, BL-4195, BL-4298]
touches: [Curl.Extension.WebSocketExtendedConnect.UnitLibrary/Http3, Curl.Extension.WebSocketExtendedConnect.UnitTests/Http3]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4296 — WebSocketExtendedConnect: open and run a WebSocket over HTTP/3 (RFC 9220)

## Goal

A client flow in the extension opens a WebSocket on an HTTP/3 request stream with extended CONNECT and exchanges frames over it.

## Context

Third part of BL-4112. Uses the wire layer from BL-4298, the HTTP/3 client session moved into `Curl.Http3` by BL-4194, the shared WebSocket codec from BL-4196 and the scheme hook from BL-4195. Specification: RFC 9220.

## Acceptance criteria

- [x] A client flow in `Http3/` waits for the server's SETTINGS_ENABLE_CONNECT_PROTOCOL, sends the extended CONNECT on a request stream and treats a 2xx response as the open WebSocket (RFC 9220 section 3); unit tests with fake connections or recorded bytes, no network.
- [x] DATA frames on that stream carry WebSocket frames both ways, and stream FIN or reset closes the connection as RFC 9220 says; unit tests cite the sections.
- [x] A server that does not advertise the setting, or answers non-2xx, fails with a clear error; unit tests.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4112 (the dark factory's cost cap).
- Built as `Http3/Http3ExtendedConnectClient` (opens), `Http3ExtendedConnectWebSocket` (frames, FIN, reset) and `Http3ExtendedConnectException`; design recorded in ADR-1089 (decided by Claude under Stewart's delegation). Tests run over an in-memory `IMultiplexedConnection` with recorded HTTP/3 bytes, no network.
- `Measure-CodeQuality.ps1 -Library Curl.Extension.WebSocketExtendedConnect.UnitLibrary`: 0 failing members. An `await` inside a `catch` left a compiler branch uncovered, so a failed opening is awaited with `SuppressThrowing` and the session disposed before rethrowing.
- Platform-neutral: no paths, OS errors or certificates; fast tests green on Windows.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 6b11d967 8722f962 db287935 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. HTTP/3 extended CONNECT WebSocket flow built and tested; quality gates and fast tests green
