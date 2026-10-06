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
completed: 2026-10-02
---
# BL-1144 — Write curl's -v lines for a chunked 407 to CONNECT

## Goal

With `-v`, a CONNECT `407` whose body is chunked writes curl 8.21.0's `* CONNECT responded chunked` (after the last header line, before `< ` blank), `* Ignore chunked response-body` and, once the body is read, `* chunk reading DONE`, and a malformed body writes the chunk parser's message line before `* closing connection #0`.

## Context

BL-862 (ADR-0333) made `TcpConnector` read a chunked `407` through `HttpProxyTunnel.DiscardChunkedBodyAsync` and `HttpProxyTunnelChunkedBody` (`Curl.Networking.UnitLibrary`) and reuse the connection, but writes none of these lines. The measured `-v` stderr is in BL-862's Notes (curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -Connections 2 -HoldOpenMilliseconds 3000 -AnswerHeldRequests 1`). Diagnostic lines go through `NetworkDiagnosticLog`. BL-863 covers the retry's other `-v` lines; keep the two consistent.

## Acceptance criteria

- [x] A `TcpConnectorTests` case with a chunked `407` then `200` and a recording diagnostic log shows the three lines in the measured order.
- [x] A malformed chunked `407` writes `* chunk hex-length char not a hex digit: 0x7a` as measured.
- [x] `dotnet build` clean, `dotnet test --filter "TestCategory!=Integration"` green, and `Measure-CodeQuality.ps1` shows 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

- Measured curl 8.21.0 (mingw, Schannel) 2026-10-02, `Record-CurlExchange.ps1 -Connections 2 -HoldOpenMilliseconds 3000 -AnswerHeldRequests 1`, `curl -s -S -v -p -x http://127.0.0.1:<port> -U u:p --proxy-digest http://example.test/`, the BL-862 chunked 407:
  - Good body: `< Transfer-Encoding: chunked`, `* CONNECT responded chunked`, `< `, `* Ignore chunked response-body`, `* chunk reading DONE`, then `* Proxy auth using Digest with user 'u'` and the second CONNECT.
  - `zz`: `... * Ignore chunked response-body`, `* chunk hex-length char not a hex digit: 0x7a`, `* closing connection #0`.
  - Cut short (`5\r\nhel`, close): `* Proxy CONNECT aborted` before `* closing connection #0`.
  - `5\r\nhelloX`: no line between `* Ignore chunked response-body` and `* closing connection #0` (exit 56 `Failure when receiving data from the peer`).
- Done through the transfer's events (`ITransferEvents.ReportInfo`), as BL-863's CONNECT lines are, not `NetworkDiagnosticLog`: `ConnectTunnelVerboseLines` gains `RespondedChunked` (written after any non-2xx `Transfer-Encoding:` line naming `chunked`, as curl's `cf-h1-proxy.c` does), `IgnoreChunkedBody`, `ChunkReadingDone` and `ReportChunkedBodyEnd` (the failure message for every exit 56 except `Failure when receiving data from the peer`, which curl words with no `failf`). `TcpConnector.DiscardRejectedBodyAsync` writes them. `* closing connection #0` already comes from `HttpProtocolHandler`.
- Not done (out of scope, curl's 2xx counterpart): `Ignoring Transfer-Encoding in CONNECT 200 response` / `Ignoring Content-Length ...`.
- Results: `Curl.Networking.UnitTests` 2719 passed; full fast suite green; `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing, worst CRAP 10.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v writes CONNECT responded chunked, Ignore chunked response-body and chunk reading DONE (or the failure line) for a chunked 407 to CONNECT
