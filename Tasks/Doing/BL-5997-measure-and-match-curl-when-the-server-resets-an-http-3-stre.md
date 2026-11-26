---
id: BL-5997
title: Measure and match curl when the server resets an HTTP/3 stream
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5996]
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptOutcome.cs, Curl.Original.Protocol.Http.UnitTests/Http3ResetStreamConnectionTests.cs, Record-CurlExchange.ps1]
requirement: none
created: 2026-11-25
completed:
---
# BL-5997 — Measure and match curl when the server resets an HTTP/3 stream

## Goal

When the server resets an HTTP/3 request stream, Curl's connection line, connection reuse and
`--trace-config http/3` lines for that transfer and the next URL match curl's.

## Context

ADR-1415 (BL-5996): over HTTP/3 only a transfer that succeeds, times out, or that `-f`,
`--max-filesize` or a failed write ends marks its stream done and keeps its connection
(`HttpProtocolHandler.FailsOnlyItsStream`). A stream the server resets (exit 95, or 18 after
body bytes) was not measured: `Record-CurlExchange.ps1 -Http3StalledOrigin` cannot reset a
stream, so it needs an option that sends RESET_STREAM after the answer. Measure curl 8.18.0's
ngtcp2 build with two URLs to the origin.

## Acceptance criteria

- [ ] The Notes record what curl writes and sends when the origin resets the first of two URLs' streams, before and after body bytes.
- [ ] Curl's connection line, connection reuse and `--trace-config http/3` stream-done lines match that measurement, pinned by unit tests.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: cfb57401 11f8926c 3d836d5e.
