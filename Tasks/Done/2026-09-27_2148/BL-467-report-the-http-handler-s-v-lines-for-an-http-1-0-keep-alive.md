---
id: BL-467
title: Report the HTTP handler's -v lines for an HTTP/1.0 keep-alive and a body with no length
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-467 — Report the HTTP handler's -v lines for an HTTP/1.0 keep-alive and a body with no length

## Goal

`HttpProtocolHandler` reports through `context.Events.ReportInfo` two more `-v` lines curl 8.21.0 prints while it reads a response head: `HTTP/1.0 connection set to keep alive` and `no chunk, no close, no size. Assume close to signal end`.

## Context

- BL-449 added `HTTP 1.0, assume close after body`, `Ignoring the response-body`, `setting size while ignoring`, `Done waiting for 100-continue` and `Recv failure: Connection was reset`; its Notes hold the measurements below.
- Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1`:
  - `-s -v` against `HTTP/1.0 200 OK`, `Connection: keep-alive`, `Content-Length: 2`: `* HTTP 1.0, assume close after body`, `< HTTP/1.0 200 OK`, then `* HTTP/1.0 connection set to keep alive` **before** `< Connection: keep-alive`.
  - `-s -L -v` against `HTTP/1.1 302 Found`, `Location: /b` and no length: after `< Location: /b` came `* no chunk, no close, no size. Assume close to signal end`, then `< ` (ADR-0050's table has this line too).
- The line before its header line means the handler must report it as the header is accepted, before `ReportResponseHeader` for that line (`HttpResponseHeadReader`); the second one belongs before the head's held empty line (`HttpResponseHeadReader.ReportHeldEmptyLine`).
- Measure again (including a 1.1 response without a length, not followed) before pinning.

## Acceptance criteria

- [x] A test over a scripted connection pins each line's text and its position among the header events, as measured.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-449 (2026-09-27).
- Measured 2026-09-27, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Port 18467 -CurlArgs -s,-v,...`:
  - `HTTP/1.0 connection set to keep alive` comes before each HTTP/1.0 header line that is a `Connection` header naming `keep-alive` (any case, among other options, `Connection:keep-alive` with no blank too); two such headers print it twice. Not when the same line also names `close` (`keep-alive, close`), and never for HTTP/1.1.
  - `no chunk, no close, no size. Assume close to signal end` comes after the last header line and before `< ` for an HTTP/1.1 head with a body and no end-of-message indicator: no length (`-L` 302, a 100 Continue then 200, `-0`, `--tr-encoding` with no Transfer-Encoding, `-H "Connection: close"` on the request, `Connection: keep-alive` in the response all print it), or a Content-Length under `--ignore-content-length`. With `-f` on a 404 it comes before `The requested URL returned error: 404`.
  - Not printed for: HTTP/1.0 (with or without keep-alive), `Connection: close` or `close, keep-alive`, chunked (with `--raw` too, and `--raw` with a Content-Length and chunked), `--tr-encoding` with `Transfer-Encoding: gzip` (curl closes), 204, 304, `-I`, or a head the server closed before its empty line.
- Design: the keep-alive line is decided per header line in `HttpResponseHeadReader` (`HttpConnectionPersistence.KeepsHttp10Alive`), since curl prints it before the header it came from. A folded continuation line is not looked at (not measured; a folded Connection header is rare enough to leave). The end-of-message line is decided in the handler once the head is accepted and no header is refused (`HttpConnectionPersistence.LacksEndOfMessageIndicator`), just before the `-f` check, and only when the head ended at its empty line (`HttpResponseHeadReader.EndedAtEmptyLine`). Any Transfer-Encoding header rules it out: chunked gives the body an end, and without `--tr-encoding` any other coding is refused before this point (exit 61), while with it curl closes the connection.
- Found: an HTTP/1.0 keep-alive response with no length makes curl print `left intact`, where the handler shuts the connection down; filed as BL-471.
- Tests: `HttpProtocolHandlerTests.HeadInfoLines.cs` (positions), `HttpConnectionPersistenceTests` (both predicates); `ConnectionReuse` and `RemainingInfoLines` expectations updated to the measured lines. Curl.Protocol.Http.UnitTests 1117 passed.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -v now reports 'HTTP/1.0 connection set to keep alive' and 'no chunk, no close, no size. Assume close to signal end' where curl 8.21.0 does
