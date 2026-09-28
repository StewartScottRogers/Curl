---
id: BL-407
title: Report request and response header and body events from HttpProtocolHandler
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-407 — Report request and response header and body events from HttpProtocolHandler

## Goal

`HttpProtocolHandler` reports the request head, each response header line, the body bytes sent and received, and curl's `using HTTP/1.x` and `Request completely sent off` info lines to `context.Events`, so `-v` and `--trace` show the exchange.

## Context

- ADR-0046 fixes the event kinds and boundaries: one `ReportRequestHeader` per head write, one `ReportResponseHeader` per received line (status line and final blank line included), one `ReportDataReceived` per body read, one `ReportDataSent` per body write.
- Today the handler reports only its connection-end lines (`Connection #0 to host ... left intact` and the like), so `curl -v` over this handler prints almost nothing.
- Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel): `Record-CurlExchange.ps1 -Port 18441 -Response 'HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 6\r\n\r\nhello\n' -CurlArgs @('-s','-v','http://127.0.0.1:18441/f.txt','-o','o')`. The bytes are pinned in `Curl.Console.UnitTests/CurlCommandRunnerTransferEventTests.cs` (BL-242), where a scripted handler reports the events this task makes the real one report.

## Acceptance criteria

- [ ] A test over a scripted connection records, in order, the events the measured `-s -v` exchange shows after the connect: `using HTTP/1.x`, one 84-byte request head, `Request completely sent off`, four response header lines, one 6-byte data event, then the `left intact` line.
- [ ] With `-d hi`, a 2-byte `ReportDataSent` follows the head (curl prints `} [2 bytes data]`, ADR-0046).
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`.

## Notes

- Filed from BL-242 (2026-09-27), which wired `-v` and `--trace` in `Curl.Console`.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
