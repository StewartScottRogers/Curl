---
id: BL-449
title: Report the HTTP handler's remaining -v info lines for ignored bodies, the 100-continue wait and receive failures
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-449 — Report the HTTP handler's remaining -v info lines for ignored bodies, the 100-continue wait and receive failures

## Goal

`HttpProtocolHandler` reports through `context.Events.ReportInfo` the `-v` lines curl 8.21.0 prints around a request that BL-407 left out: `Ignoring the response-body` and `setting size while ignoring` for a body `-L` follows past, `Done waiting for 100-continue` when the one-second wait runs out, `Recv failure: <reason>` when a read fails, and `HTTP 1.0, assume close after body` for an HTTP/1.0 response without keep-alive.

## Context

- ADR-0046 fixes the event kinds; BL-407 made the handler report the request head, header lines, body data, `using HTTP/1.x`, `Request completely sent off` and `upload completely sent off: N bytes`.
- Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1` (BL-407 Notes):
  - `-s -L -v http://127.0.0.1:18472/a`, the 302 carrying `Content-Length: 4` and `move`: after `< Content-Length: 4` came `* Ignoring the response-body` and `* setting size while ignoring`, then `< ` (the blank line), then the connection-end line.
  - `printf abcde | curl -s --trace-ascii - -T - http://127.0.0.1:18475/u`: `* Done waiting for 100-continue` between the head and the first `Send data`.
  - Two URLs on one connection whose server closed after the first: `* Recv failure: Connection was reset` before `* Connection died, retrying a fresh connect (retry count: 1)`.
  - ADR-0046's Context: an HTTP/1.0 response printed `* HTTP 1.0, assume close after body` before its status line.
- Measure the exact placement of each line again before pinning it; the placement between header events matters for `-v`.

## Acceptance criteria

- [x] A test over a scripted connection pins each of the four cases above: the line's text and its position among the header and data events, as measured.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-407 (2026-09-27).
- Measured again 2026-09-27, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`:
  - `-s -L -v http://127.0.0.1:18601/a`, 302 with `Content-Length: 4` and `move`, then `ok` on a fresh connection: `< Content-Length: 4`, `* Ignoring the response-body`, `* setting size while ignoring`, `< `, `* Connection #0 ... left intact`; on the reused connection for `/b` the server had closed: `* Request completely sent off`, `* Recv failure: Connection was reset`, `* Connection died, retrying a fresh connect (retry count: 1)`, `* shutting down connection #0`, `* Issue another request ...`.
  - Chunked 302: `* Ignoring the response-body` only, no size line. `-I -L` 302 with `Content-Length: 4`: both lines. 302 with `Connection: close`, or with no length (`* no chunk, no close, no size. Assume close to signal end`): neither line.
  - `-f` against a 404: `< Content-Length: 4`, `* The requested URL returned error: 404`, `< `.
  - `-s -v -T -` with stdin `abcde`, server silent: `> ` head, `* Done waiting for 100-continue`, `} [10 bytes data]`, `* upload completely sent off: 15 bytes`.
  - HTTP/1.0 200, with or without `Connection: keep-alive`: `* Request completely sent off`, `* HTTP 1.0, assume close after body`, `< HTTP/1.0 200 OK`; with keep-alive, `* HTTP/1.0 connection set to keep alive` came before `< Connection: keep-alive` (filed as BL-467).
  - `-c -` with `Set-Cookie: a=1`: `* Added cookie ...` came before `< Set-Cookie: a=1` (filed as BL-468).
- Plan and what landed: `HttpResponseHeadReader` holds the final head's empty line until the handler calls `ReportHeldEmptyLine`, after it has decided whether the body is discarded (and also in the failure path, so `-f` and the other head failures still report `< `); 1xx heads' empty lines are reported at once. It reports `HTTP 1.0, assume close after body` before any `HTTP/1.0` status line. `HttpProtocolHandler.ReportIgnoredBody` reports the two ignoring lines when the body is discarded (redirect followed, or a retry) and the connection persists (`HttpConnectionPersistence.KeepsAlive`), the size line when the body's Content-Length frames it (for `-I`, a Content-Length unless chunked or `--ignore-content-length`). `HttpContinueWaitConnection.WaitRanOut` drives `Done waiting for 100-continue`. The failure path reports `Recv failure: Connection was reset`.
- Decisions (Claude, under Stewart's delegation; each follows the measurement, so no ADR was written - they choose no behaviour curl does not show, and ADR numbers collide across parallel lanes):
  - `Recv failure: <reason>` is reported only for a read the peer reset, the one reason the handler knows (`HttpTransferMessages.ConnectionReset`); any other failed read has no curl reason text to print, and failure messages are not generally repeated as info lines (ADR-0100 item 6).
  - Cookie lines now land after the last header line and before `< ` instead of after `< ` - nearer curl, not yet exact (BL-468).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. HTTP -v now reports Ignoring the response-body, setting size while ignoring, Done waiting for 100-continue, Recv failure: Connection was reset and HTTP 1.0, assume close after body where curl 8.21.0 does
