---
id: BL-4219
title: Extension SignalR: negotiate HTTP exchange and WebSocket upgrade over IConnection
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4218]
touches: [Curl.Extension.SignalR.UnitLibrary/Transport, Curl.Extension.SignalR.UnitTests/Transport, Curl.Extension.SignalR.UnitLibrary/Curl.Extension.SignalR.UnitLibrary.csproj]
requirement: none
created: 2026-10-29
completed:
---
# BL-4219 — Extension SignalR: negotiate HTTP exchange and WebSocket upgrade over IConnection

## Goal

The SignalR extension can run the negotiate request and the WebSocket upgrade over an `IConnection`, and send and receive hub messages as WebSocket text frames, so BL-4108's handler only wires them to a URL.

## Context

Split from BL-4108 for size. Builds on BL-4218's codec. Specification: dotnet/aspnetcore `src/SignalR/docs/specs/TransportProtocols.md` (negotiate is `POST [endpoint-base]/negotiate?negotiateVersion=1`, then the WebSocket transport at `[endpoint-base]?id=<token>`; the client sends the handshake first; follow a negotiate redirect `url` with `accessToken` as a bearer token) and RFC 6455 section 4.1 (client handshake: `Upgrade: websocket`, `Sec-WebSocket-Key`, `Sec-WebSocket-Version: 13`, checking `101` and `Sec-WebSocket-Accept`). Write the HTTP/1.1 request and response reading (Content-Length and chunked bodies) here: an extension never references `Curl.Protocol.Http` or `Curl.Protocol.Ws` (ADR-0999). Frames come from the shared `Curl.WebSocket.UnitLibrary` (`WsFrameEncoder`, `WsFrameDecoder`, `IWebSocketRandomSource`); answer a server Ping frame with Pong and a Close frame with Close. No handler or registration here.

## Acceptance criteria

- [ ] A negotiate exchange over a fake `IConnection` sends the exact request bytes the spec gives and returns the parsed negotiate response, following one redirect; non-2xx statuses and malformed bodies fail with a named error; unit tests cite TransportProtocols.md.
- [ ] The WebSocket upgrade over a fake `IConnection` sends RFC 6455 section 4.1's request, accepts only `101` with the right `Sec-WebSocket-Accept`, and then sends and reads hub messages as masked text frames; unit tests cite the sections they check.
- [ ] No test opens a socket.
- [ ] `Curl.Extension.SignalR.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.SignalR.UnitLibrary`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
