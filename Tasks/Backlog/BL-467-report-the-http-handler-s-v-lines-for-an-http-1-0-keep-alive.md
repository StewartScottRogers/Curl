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
completed:
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

- [ ] A test over a scripted connection pins each line's text and its position among the header events, as measured.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-449 (2026-09-27).

## Log

- 2026-09-27: Created.
