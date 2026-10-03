---
id: BL-1146
title: Report curl's Ignore response-body lines when a CONNECT 407 body is discarded
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1146 — Report curl's Ignore response-body lines when a CONNECT 407 body is discarded

## Goal

When `TcpConnector` discards a CONNECT `407`'s body to answer on the same connection, it reports curl 8.21.0's `Ignore <n> bytes of response-body` (or the chunked lines) after the reply's header lines.

## Context

Measured in BL-863 (Notes): curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -Script` serving `407` Digest with `Content-Length: 6` and body `denied` and no `Connection: close`, then `200`: after `< ` it prints `* Ignore 6 bytes of response-body` before `* Proxy auth using Digest with user 'u'`. BL-862 Notes record `Ignore chunked response-body` for a chunked `407`; curl's `cf-h1-proxy.c` also prints `CONNECT responded chunked` while reading the `Transfer-Encoding` header - measure where it lands among the `<` lines. Where: `TcpConnector.DiscardRejectedBodyAsync`; tests beside `ConnectAsync_WithProxyDigestOnAKeptOpenConnection_ReportsBothConnectsWithoutConnectingAgain` in `TcpConnectorTests.ProxyAuthVerbose.cs`.

## Acceptance criteria

- [x] The Content-Length and chunked cases are measured and pinned in Notes with the curl version.
- [x] `RecordingTransferEvents.Transcript` tests pin each line's text and position.
- [x] `dotnet build -warnaserror` clean, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 100% line and branch, no failing member.

## Notes

- Measured 2026-10-02 with curl 8.21.0 (x86_64-w64-mingw32, Schannel), `Record-CurlExchange.ps1 -Script`, `curl -v -p --proxy-digest -U u:p -x http://127.0.0.1:<port> http://example.test/`, a Digest `407` without `Connection: close`, then `200`:
  - `Content-Length: 6`, body `denied`: `< Content-Length: 6`, `< `, `* Ignore 6 bytes of response-body`, then `* Proxy auth using Digest with user 'u'` and the second CONNECT on the same connection.
  - `Content-Length: 0`: no Ignore line; `< ` is followed directly by `* Proxy auth using Digest with user 'u'`. This matches `cf-h1-proxy.c`, which writes the line only while `cl_remaining` is non-zero.
  - Chunked: BL-1144 had already measured and implemented it (`* CONNECT responded chunked` after the `Transfer-Encoding: chunked` header line and before `< `, then `* Ignore chunked response-body` and `* chunk reading DONE`), and `ConnectAsync_WithProxyDigestAfterAChunked407_ReportsRespondedChunkedIgnoreAndChunkReadingDone` pins it. This task left it unchanged.
- Change: `ConnectTunnelVerboseLines.ReportIgnoredBody` writes `Ignore <n> bytes of response-body` when the length is above 0. `TcpConnector.DiscardRejectedBodyAsync` calls it before it discards a `Content-Length` body. curl writes the line before it reads the body, so a body that ends early still gets it.
- Tests: `ConnectAsync_WithProxyDigestOnAKeptOpenConnection_ReportsBothConnectsWithoutConnectingAgain` now pins the line after `< `, and the new `ConnectAsync_WithProxyDigestAfterAnEmpty407Body_ReportsNoIgnoreLine` pins that no line is written.
- Quality gate: `ConnectTunnelVerboseLines.ReportReplyHead`, added in BL-1144, measured cyclomatic complexity 12. Its per-line work moved into `ReportReplyLine`, and the library now has 0 failing members at 100% line and branch coverage.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v writes Ignore <n> bytes of response-body after a CONNECT 407's head before its Content-Length body is discarded, none for an empty body
