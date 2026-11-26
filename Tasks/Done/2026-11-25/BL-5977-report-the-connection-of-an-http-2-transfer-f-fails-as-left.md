---
id: BL-5977
title: Report the connection of an HTTP/2 transfer -f fails as left intact
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.TransferFailures.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptFailures.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionEnd.cs, Curl.Original.Protocol.Http.UnitTests/Http2ContinueWaitStatusTests.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptOutcome.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Http.UnitTests/Http2StreamAbortTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5977 — Report the connection of an HTTP/2 transfer -f fails as left intact

## Goal

An HTTP/2 transfer that `-f` fails reports its connection in `-v` as curl does.

## Context

Found by BL-5975. curl 8.18.0 nghttp2 build: `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H! :status: 500|content-length: 0'`, `--http2-prior-knowledge -sS -v -f -H "Expect: 100-continue" -d x=1`: `Connection #0 ... left intact`. Curl writes `closing connection #0`: `FailedOutcome` keeps the connection only for `HttpTransferException.LeavesConnectionIntact`, which the `-f` failure does not set. BL-5974 (ADR-1405) did the same for `-m`, and BL-5976 covers reuse after `-m`. Measure a plain `-f` GET and a second URL to the same origin too, to see whether curl reuses the connection.

## Acceptance criteria

- [x] For the measured `-f` case Curl writes curl's connection line, pinned by a unit test.
- [x] A second URL to the same origin goes out on the connection curl uses for it, as measured, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.18.0 (nghttp2, WinGet), `-Http2Origin prior-knowledge`, `--http2-prior-knowledge -sS -v -f -m 5 <url>/a <url>/b`, `/a` answered `500`: `left intact` and HEADERS on stream 3 of the same connection. Stream 1 gets no reset when the head ends it, `RST_STREAM` 5 when left open, and `RST_STREAM` -902 when DATA ending it arrives too (ADR-1408: Curl sends 5 there).
- The built Curl, run against the same recorder, writes the same connection lines and frames for the first two cases.
- Added to `touches` (no other task in Doing on `origin/work/dark-factory`): `AttemptOutcome.cs`, `ConnectionExchange.cs` and `ResponseExchange.cs` hold the outcome, reuse and reset logic; `Http2StreamConnection.cs` the reset, renamed `ResetUnlessEndedBothWaysAsync` as it now serves `-f` too; `Http2StreamAbortTests.cs` the reuse tests; the library `CLAUDE.md` the description.
- `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 286d6895 dcf128c7 3d836d5e.
- 2026-11-25: Doing -> Done. An HTTP/2 transfer -f fails reports its connection left intact and the next URL goes out on it (ADR-1408).
