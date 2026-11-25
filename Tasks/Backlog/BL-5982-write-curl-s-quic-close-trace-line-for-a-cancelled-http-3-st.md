---
id: BL-5982
title: Write curl's quic close trace line for a cancelled HTTP/3 stream
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5981]
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/Http3StreamTrace.cs, Curl.Original.Protocol.Http.UnitLibrary/Http3Session.cs, Curl.Original.Protocol.Http.UnitTests/Http3StreamAbortTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5982 — Write curl's quic close trace line for a cancelled HTTP/3 stream

## Goal

Under `--trace-config http/3`, Curl writes `[HTTP/3] [<id>] quic close(app_error=268) -> 0` for a stream a time limit cancelled, where curl writes it.

## Context

ADR-1409 (BL-5978): curl 8.18.0 writes the line during the next transfer on the connection, once QUIC closes the cancelled stream. `Http3StreamConnection.CancelUnlessEndedBothWaysAsync` cancels it; `Http3StreamTrace` writes the other lines. Measure with `Record-CurlExchange.ps1 -Http3StalledOrigin` (BL-5981).

## Acceptance criteria

- [ ] The Notes record where curl writes the line among the next transfer's lines.
- [ ] Curl writes the line there, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
