---
id: BL-471
title: Match curl's connection end for an HTTP/1.0 keep-alive response with no length
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions/ADR-0109-an-http-1-0-keep-alive-body-read-to-the-close-is-reported-left-intact.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-471 — Match curl's connection end for an HTTP/1.0 keep-alive response with no length

## Goal

For an `HTTP/1.0` response with `Connection: keep-alive` and neither a Content-Length nor chunked coding, `HttpProtocolHandler` ends the connection the way curl 8.21.0 does, and reports the same `-v` connection-end line.

## Context

- Found while measuring BL-467 (2026-09-27, curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1`): `-s -v` against `HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\nhi` (server closes after the body) printed `* HTTP/1.0 connection set to keep alive` and ended with `* Connection #0 to host 127.0.0.1:18467 left intact`, not `shutting down connection #0`, and no `no chunk, no close, no size` line (curl prints that one only for HTTP/1.1).
- `HttpConnectionPersistence.KeepsAlive` returns false for it today, since the body runs to close, so the handler reports `shutting down connection #N`.
- Measure again before pinning, including what curl does with the pooled connection when a second URL follows on the same command line (it was closed by the server).

## Acceptance criteria

- [x] A test over a scripted connection pins the connection-end line for that response as measured, and whether the connection is marked reusable.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-467 (2026-09-27).
- Measured 2026-09-27, curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -s -v`, server closing after the body:
  - `HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\nhi`: `HTTP 1.0, assume close after body`, `HTTP/1.0 connection set to keep alive`, then `Connection #0 to host 127.0.0.1:P left intact`, exit 0.
  - The same with `Content-Length: 2` and `--ignore-content-length`: `left intact`.
  - `HTTP/1.0 404`, keep-alive, no length, `-f`: `closing connection #0`, exit 22 (the existing failure path).
  - `HTTP/1.1 200 OK\r\nConnection: keep-alive\r\n\r\nhi`: `shutting down connection #0` (unchanged).
  - Two URLs, same host: after `/a`'s `left intact`, curl prints `Connection 0 seems to be dead`, `shutting down connection #0`, `Hostname 127.0.0.1 was found in DNS cache`, and opens connection #1 for `/b`.
- Decision (ADR-0109, decided by Claude under Stewart's delegation): report `left intact` via the new `HttpConnectionPersistence.KeepsHttp10AliveUntilServerCloses`, but do not mark the connection reusable - it is closed, and with no liveness check in `PoolingConnector` pooling it would send `/b` into a dead socket and print the died-and-retried lines instead. The two dead-connection lines are filed as BL-477.
- Added the ADR and `Documentation/Planning/Decisions/README.md` to `touches`: the delegation rule requires an ADR, and no task in Doing names either file.
- Tests: `HttpProtocolHandlerTests.ExecuteAsync_Http10KeepAliveBodyRunsToTheClose_LeavesTheConnectionUnmarkedButReportsItLeftIntact`, `HttpConnectionPersistenceTests.KeepsHttp10AliveUntilServerCloses_Head_IsTrueForAnHttp10KeepAliveBodyThatRunsToTheClose` (7 rows). Curl.Protocol.Http.UnitTests: 1137 pass; quality audit 100% line and branch, 0 failing members.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. An HTTP/1.0 keep-alive response whose body runs to the close reports curl's 'left intact' line, and the closed connection is not pooled
