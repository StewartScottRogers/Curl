---
id: BL-485
title: Fail a head closed among headers after a short Content-Length with curl's 'transfer closed' message
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-485 — Fail a head closed among headers after a short Content-Length with curl's 'transfer closed' message

## Goal

When the peer closes a final head among its headers after a `Content-Length` header curl 8.21.0 acted on (not the last header), `HttpProtocolHandler` fails with exit 18 and curl's message `transfer closed with N bytes remaining to read`, with the `-v` lines in curl's order, instead of `end of response with N bytes missing`.

## Context

- Measured (2026-09-27, `Record-CurlExchange.ps1`) against `HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-Before: 1\r\n` then close:
  - `-sS`: exit 18, stderr `curl: (18) transfer closed with 5 bytes remaining to read`.
  - `-s -v`: `< HTTP/1.1 200 OK`, `< Content-Length: 5`, `* transfer closed with 5 bytes remaining to read`, `< X-Before: 1`, `* closing connection #0` - the failure line comes before the last header's line.
- `HttpResponseBodyReader.EndAtClose` throws `HttpTransferMessages.BodyBytesMissing` today, the message for a body cut short after its head ended.
- The header a head ends on at close is never acted on (BL-483), so a `Content-Length` as the last header gives exit 0.

## Acceptance criteria

- [x] A test over a scripted connection that closes after `Content-Length: 5\r\nX-Before: 1\r\n` pins exit 18, the measured message and the measured `-v` line order.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-483 (2026-09-27).
- Measured 2026-09-27, curl 8.21.0 Schannel, `Record-CurlExchange.ps1`, each response closed right after its last header:
  - `200 OK`, `Content-Length: 5`, `X-Before: 1` with `-s -v`: exit 18; `< HTTP/1.1 200 OK`, `< Content-Length: 5`, `* transfer closed with 5 bytes remaining to read`, `< X-Before: 1`, `* closing connection #0`.
  - Same with `-sS -D -`: exit 18, stdout holds all three head lines (no empty line), stderr `curl: (18) transfer closed with 5 bytes remaining to read`.
  - `404 Not Found` with `-s -v -f`: exit 18 and the same lines - the close fails before `-f` does.
  - `Content-Length: 0`, or `-I`, or `--ignore-content-length`: exit 0, every line reported, `Connection #0 ... left intact`.
- Plan, delivered: `HttpResponseHeadReader` now holds the last header of a head closed among its headers (still unacted on) instead of reporting it inside `ReadAsync`; `HttpProtocolHandler.ThrowIfClosedBeforeContentLength`, right after the refused-header check and before `-f`, reports the info line and throws exit 18 `HttpTransferMessages.TransferClosedWithBytesRemaining` when the head curl acted on has a body framed by a Content-Length above zero; the held header is then reported by the failure path's `ReportHeldLines`, or by `ReportHeaderHeldAtClose` on success.
- Left alone on purpose: a chunked head closed among its headers (`Transfer-Encoding: chunked` then another header) still fails in the body reader with `transfer closed with outstanding read data remaining`, its `-v` line order unmeasured; not in this task's goal.
- Gates: `dotnet build -warnaserror` clean, fast tests green (Http 1156 passed), `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` 100% line and branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A head closed among headers after a Content-Length above zero fails with exit 18 'transfer closed with N bytes remaining to read', -v lines in curl's order
