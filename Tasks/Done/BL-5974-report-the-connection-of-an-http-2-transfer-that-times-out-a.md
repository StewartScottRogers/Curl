---
id: BL-5974
title: Report the connection of an HTTP/2 transfer that times out as left intact, as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionEnd.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptOutcome.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitTests/Http2StreamAbortTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5974 — Report the connection of an HTTP/2 transfer that times out as left intact, as curl does

## Goal

An HTTP/2 transfer that ends by `-m` reports its connection in `-v` as curl does.

## Context

Found by BL-5970. curl 8.18.0 nghttp2 build over `--http2-prior-knowledge -m 3` against a stream the server never ends writes `Connection #0 to host ... left intact`; Curl writes `closing connection #0`. See `HttpProtocolHandler.ConnectionEnd.cs`.

## Acceptance criteria

- [x] For a timed-out HTTP/2 transfer Curl writes the measured connection line, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.18.0 (nghttp2, WinGet), `-Http2Origin prior-knowledge -Http2Frames`, `-v -m 2`: after a stalled `200` or a lone `100`, `Connection #0 to host ... left intact`, exit 28, `RST_STREAM` 5 then GOAWAY. Curl, run against the same recorder, now writes the same connection line.
- Decision (ADR-1405): HTTP/2 only; the connection is reported left intact but still closed. A second URL reuses it in curl, which Curl does not yet: filed BL-5976. HTTP/3 unmeasured, unchanged.
- touches widened to AttemptOutcome.cs and ConnectionExchange.cs (the outcome must know it ran on an HTTP/2 stream), and the tests went to Http2StreamAbortTests.cs, the `-m` abort tests, in place of Http2ContinueWaitStatusTests.cs. No other task was in Doing on origin/work/dark-factory.
- Fast tests green; `Measure-CodeQuality.ps1` for the Http library: 0 failing members. Tests are platform-neutral: in-memory connections and a fake clock.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 8f351810 3d836d5e.
- 2026-11-25: Doing -> Done. Over HTTP/2 a transfer -m ends now reports its connection left intact, as curl does.
