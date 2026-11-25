---
id: BL-5977
title: Report the connection of an HTTP/2 transfer -f fails as left intact
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.TransferFailures.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptFailures.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionEnd.cs, Curl.Original.Protocol.Http.UnitTests/Http2ContinueWaitStatusTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5977 — Report the connection of an HTTP/2 transfer -f fails as left intact

## Goal

An HTTP/2 transfer that `-f` fails reports its connection in `-v` as curl does.

## Context

Found by BL-5975. curl 8.18.0 nghttp2 build: `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H! :status: 500|content-length: 0'`, `--http2-prior-knowledge -sS -v -f -H "Expect: 100-continue" -d x=1`: `Connection #0 ... left intact`. Curl writes `closing connection #0`: `FailedOutcome` keeps the connection only for `HttpTransferException.LeavesConnectionIntact`, which the `-f` failure does not set. BL-5974 (ADR-1405) did the same for `-m`, and BL-5976 covers reuse after `-m`. Measure a plain `-f` GET and a second URL to the same origin too, to see whether curl reuses the connection.

## Acceptance criteria

- [ ] For the measured `-f` case Curl writes curl's connection line, pinned by a unit test.
- [ ] A second URL to the same origin goes out on the connection curl uses for it, as measured, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 286d6895 dcf128c7 3d836d5e.
