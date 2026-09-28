---
id: BL-471
title: Match curl's connection end for an HTTP/1.0 keep-alive response with no length
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-471 — Match curl's connection end for an HTTP/1.0 keep-alive response with no length

## Goal

For an `HTTP/1.0` response with `Connection: keep-alive` and neither a Content-Length nor chunked coding, `HttpProtocolHandler` ends the connection the way curl 8.21.0 does, and reports the same `-v` connection-end line.

## Context

- Found while measuring BL-467 (2026-09-27, curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1`): `-s -v` against `HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\nhi` (server closes after the body) printed `* HTTP/1.0 connection set to keep alive` and ended with `* Connection #0 to host 127.0.0.1:18467 left intact`, not `shutting down connection #0`, and no `no chunk, no close, no size` line (curl prints that one only for HTTP/1.1).
- `HttpConnectionPersistence.KeepsAlive` returns false for it today, since the body runs to close, so the handler reports `shutting down connection #N`.
- Measure again before pinning, including what curl does with the pooled connection when a second URL follows on the same command line (it was closed by the server).

## Acceptance criteria

- [ ] A test over a scripted connection pins the connection-end line for that response as measured, and whether the connection is marked reusable.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-467 (2026-09-27).

## Log

- 2026-09-27: Created.
