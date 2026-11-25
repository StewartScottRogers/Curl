---
id: BL-5978
title: Measure and match curl's connection end when -m ends an HTTP/3 transfer
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionEnd.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptOutcome.cs, Curl.Original.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests/Http3StreamAbortTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5978 — Measure and match curl's connection end when -m ends an HTTP/3 transfer

## Goal

When `-m` ends an HTTP/3 transfer, Curl writes the connection line, ends the stream and treats the next URL as curl does.

## Context

BL-5974 (ADR-1405) and BL-5976 (ADR-1406) matched curl for HTTP/2: the stream is reset, `-v` says `left intact` and the next URL reuses the connection. HTTP/3 was left unchanged because its timeout was never measured; Curl still writes `closing connection`. curl treats HTTP/3 as multiplexed too. Measure with an HTTP/3 curl build and `Record-CurlExchange.ps1`, extending it if it cannot serve a stalled HTTP/3 stream.

## Acceptance criteria

- [ ] The task's Notes record what real curl writes and sends when `-m` ends an HTTP/3 transfer with a second URL to the same origin.
- [ ] Curl's connection line, stream ending and connection reuse match that measurement, pinned by unit tests.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
