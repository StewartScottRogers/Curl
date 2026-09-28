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
completed:
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

- [ ] A test over a scripted connection that closes after `Content-Length: 5\r\nX-Before: 1\r\n` pins exit 18, the measured message and the measured `-v` line order.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-483 (2026-09-27).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
