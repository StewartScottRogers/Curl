---
id: BL-1144
title: Write curl's -v lines for a chunked 407 to CONNECT
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-862]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1144 — Write curl's -v lines for a chunked 407 to CONNECT

## Goal

With `-v`, a CONNECT `407` whose body is chunked writes curl 8.21.0's `* CONNECT responded chunked` (after the last header line, before `< ` blank), `* Ignore chunked response-body` and, once the body is read, `* chunk reading DONE`, and a malformed body writes the chunk parser's message line before `* closing connection #0`.

## Context

BL-862 (ADR-0333) made `TcpConnector` read a chunked `407` through `HttpProxyTunnel.DiscardChunkedBodyAsync` and `HttpProxyTunnelChunkedBody` (`Curl.Networking.UnitLibrary`) and reuse the connection, but writes none of these lines. The measured `-v` stderr is in BL-862's Notes (curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -Connections 2 -HoldOpenMilliseconds 3000 -AnswerHeldRequests 1`). Diagnostic lines go through `NetworkDiagnosticLog`. BL-863 covers the retry's other `-v` lines; keep the two consistent.

## Acceptance criteria

- [ ] A `TcpConnectorTests` case with a chunked `407` then `200` and a recording diagnostic log shows the three lines in the measured order.
- [ ] A malformed chunked `407` writes `* chunk hex-length char not a hex digit: 0x7a` as measured.
- [ ] `dotnet build` clean, `dotnet test --filter "TestCategory!=Integration"` green, and `Measure-CodeQuality.ps1` shows 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
