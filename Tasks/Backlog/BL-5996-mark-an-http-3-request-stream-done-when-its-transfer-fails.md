---
id: BL-5996
title: Mark an HTTP/3 request stream done when its transfer fails
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5995]
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitTests/Http3FailedTransferStreamTests.cs, Record-CurlExchange.ps1]
requirement: none
created: 2026-11-25
completed:
---
# BL-5996 — Mark an HTTP/3 request stream done when its transfer fails

## Goal

After an HTTP/3 transfer that fails without timing out, Curl treats its stream as finished the way curl does, so the next URL on the connection gets curl's `--trace-config http/3` lines.

## Context

ADR-1413 (BL-5995), ADR-0388 consequences. Only a successful transfer
(`Http3StreamConnection.ReportTransferDone`) and a timed-out one (`HttpProtocolHandler.EndTimedOutStreamAsync`)
mark a stream done with `Http3Session.EndRequestStream`. After any other failure (`-f` on a
status of 400 or above, for one) the session counts the stream as active: the next stream writes no
`peer idle timeout` line and `query conn[<n>]` over-counts the streams in use. Measure curl first;
`Record-CurlExchange.ps1 -Http3StalledOrigin` answers 200 only, so it needs a status option for `-f`.

## Acceptance criteria

- [ ] The Notes record the lines curl writes when a `-f` HTTP/3 transfer fails and during the next URL on the connection.
- [ ] Curl writes those lines there, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
