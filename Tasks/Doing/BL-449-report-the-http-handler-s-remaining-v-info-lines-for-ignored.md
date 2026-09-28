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
completed:
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

- [ ] A test over a scripted connection pins each of the four cases above: the line's text and its position among the header and data events, as measured.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-407 (2026-09-27).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
