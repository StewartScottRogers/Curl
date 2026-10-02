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
completed:
---
# BL-1146 — Report curl's Ignore response-body lines when a CONNECT 407 body is discarded

## Goal

When `TcpConnector` discards a CONNECT `407`'s body to answer on the same connection, it reports curl 8.21.0's `Ignore <n> bytes of response-body` (or the chunked lines) after the reply's header lines.

## Context

Measured in BL-863 (Notes): curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -Script` serving `407` Digest with `Content-Length: 6` and body `denied` and no `Connection: close`, then `200`: after `< ` it prints `* Ignore 6 bytes of response-body` before `* Proxy auth using Digest with user 'u'`. BL-862 Notes record `Ignore chunked response-body` for a chunked `407`; curl's `cf-h1-proxy.c` also prints `CONNECT responded chunked` while reading the `Transfer-Encoding` header - measure where it lands among the `<` lines. Where: `TcpConnector.DiscardRejectedBodyAsync`; tests beside `ConnectAsync_WithProxyDigestOnAKeptOpenConnection_ReportsBothConnectsWithoutConnectingAgain` in `TcpConnectorTests.ProxyAuthVerbose.cs`.

## Acceptance criteria

- [ ] The Content-Length and chunked cases are measured and pinned in Notes with the curl version.
- [ ] `RecordingTransferEvents.Transcript` tests pin each line's text and position.
- [ ] `dotnet build -warnaserror` clean, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 100% line and branch, no failing member.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
