---
id: BL-4219
title: Extension SignalR: negotiate HTTP exchange and WebSocket upgrade over IConnection
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4218]
touches: [Curl.Extension.SignalR.UnitLibrary/Transport, Curl.Extension.SignalR.UnitTests/Transport, Curl.Extension.SignalR.UnitLibrary/Curl.Extension.SignalR.UnitLibrary.csproj, Curl.Extension.SignalR.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4219 — Extension SignalR: negotiate HTTP exchange and WebSocket upgrade over IConnection

## Goal

The SignalR extension can run the negotiate request and the WebSocket upgrade over an `IConnection`, and send and receive hub messages as WebSocket text frames, so BL-4108's handler only wires them to a URL.

## Context

Split from BL-4108 for size. Builds on BL-4218's codec. Specification: dotnet/aspnetcore `src/SignalR/docs/specs/TransportProtocols.md` (negotiate is `POST [endpoint-base]/negotiate?negotiateVersion=1`, then the WebSocket transport at `[endpoint-base]?id=<token>`; the client sends the handshake first; follow a negotiate redirect `url` with `accessToken` as a bearer token) and RFC 6455 section 4.1 (client handshake: `Upgrade: websocket`, `Sec-WebSocket-Key`, `Sec-WebSocket-Version: 13`, checking `101` and `Sec-WebSocket-Accept`). Write the HTTP/1.1 request and response reading (Content-Length and chunked bodies) here: an extension never references `Curl.Protocol.Http` or `Curl.Protocol.Ws` (ADR-0999). Frames come from the shared `Curl.WebSocket.UnitLibrary` (`WsFrameEncoder`, `WsFrameDecoder`, `IWebSocketRandomSource`); answer a server Ping frame with Pong and a Close frame with Close. No handler or registration here.

## Acceptance criteria

- [x] A negotiate exchange over a fake `IConnection` sends the exact request bytes the spec gives and returns the parsed negotiate response, following one redirect; non-2xx statuses and malformed bodies fail with a named error; unit tests cite TransportProtocols.md.
- [x] The WebSocket upgrade over a fake `IConnection` sends RFC 6455 section 4.1's request, accepts only `101` with the right `Sec-WebSocket-Accept`, and then sends and reads hub messages as masked text frames; unit tests cite the sections they check.
- [x] No test opens a socket.
- [x] `Curl.Extension.SignalR.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.SignalR.UnitLibrary`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan and decisions in ADR-1038.
  - `SignalRNegotiator` posts the negotiate request through an injected connection opener and follows one redirect
    with a bearer token.
  - `SignalRWebSocketUpgrade` sends RFC 6455 section 4.1's handshake and checks the `101`, `Upgrade`, `Connection`
    and `Sec-WebSocket-Accept` fields.
  - `SignalRWebSocketSession` sends records as masked text frames and reads them through `WsFrameDecoder`'s runs.
    It answers Ping with Pong and Close with Close.
  - `HttpRequestWriter`, `HttpResponseReader` and `BufferedConnectionReader` are the extension's own HTTP/1.1.
  - The library now references the shared `Curl.WebSocket.UnitLibrary`, which ADR-0999 allows.
- Added `Curl.Extension.SignalR.UnitLibrary/CLAUDE.md` to `touches`: it said the library references only
  `Curl.Extension.Abstractions`, which the new WebSocket reference made untrue. No task in Doing on
  `origin/work/dark-factory` names a SignalR file.
- Default taken: the access token goes on the WebSocket upgrade as an `Authorization: Bearer` header, not as the
  `access_token` query parameter, as the .NET client does outside a browser.
- Measured with `Measure-CodeQuality.ps1 -Library Curl.Extension.SignalR.UnitLibrary`:
  - Before the fix: `CheckAccepted` had complexity 14, so it was split.
  - After the fix: 100% line and branch coverage, no failing member.
- Every test drives the scripted `ScriptedConnection` fake, so no test opens a socket.
- The tests use only bytes and URLs, never paths, OS errors or certificates, so they are platform-neutral.
- 87 tests pass in `Curl.Extension.SignalR.UnitTests`, and the fast suite is green.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: c6a7a5c5 76c86a79 4642107f f10f2877 3d836d5e.
- 2026-10-29: Doing -> Done. SignalR extension runs negotiate (one redirect) and the RFC 6455 WebSocket upgrade over IConnection and exchanges hub records as masked text frames
