---
id: BL-5973
title: Stop an HTTP/2 upload when a status of 300 or above arrives after the 100 Continue wait ran out
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionReuse.cs, Curl.Original.Protocol.Http.UnitTests/Http2ContinueWaitStatusTests.cs, Curl.Original.Protocol.Http.UnitTests/HttpContinueWaitConnectionTests.cs, Record-CurlExchange.ps1, Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.RequestBodySending.cs, Documentation/Planning/Decisions/ADR-1403-an-http-2-or-http-3-request-waits-for-100-continue-when-h-asks-for-it.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5973 — Stop an HTTP/2 upload when a status of 300 or above arrives after the 100 Continue wait ran out

## Goal

A status of 300 or above that arrives on an HTTP/2 stream while the body is being sent stops the upload as curl does.

## Context

Found by BL-5970 (ADR-1403). `HttpContinueWaitConnection.StopsSending` is false on a stream, so a status arriving after the wait ran out lets the whole body go. `HttpProtocolHandler.KeepsAlive` treats a cut upload as ending the connection, which is HTTP/1.x behaviour. `Record-CurlExchange.ps1 -Http2Frames` answers as soon as the HEADERS arrive, so measuring this needs a delayed answer added to it.

## Acceptance criteria

- [x] curl is measured over HTTP/2 with a `500` sent after the wait ran out while a body is still being sent, and Curl matches its DATA frames, `-v` lines and exit code, pinned by unit tests.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.18.0 (nghttp2, WinGet) with the new `-RespondAfterBodyBytes` support for `-Http2Origin`: `--expect100-timeout 1 -H "Expect: 100-continue"`, 200000 bytes, answered after 65535 DATA bytes with window held. A `500` ending the stream (with or without DATA): exit 0, no more DATA, `HTTP error before end of send, stop sending`, `abort upload after having sent 65536 bytes`, RST_STREAM 5, left intact. A `500` leaving the stream open: the 1 byte already read goes out when window opens, then the same; exit 28 under `-m` because the response never ends.
- Curl run against the same three cases gave the same DATA and RST frames, `-v` lines (but for the version in `User-Agent`) and exit codes.
- Design (ADR-1409): the stream stops waiting for window and withholds the rest; the status read and body write take frames one at a time, which also keeps either from waiting for a frame the other took. HTTP/3 left to BL-5979.
- touches widened to Http2StreamConnection.cs, RequestBodySending.cs and ADR-1403; BL-5978, the only other task in Doing, names none of them.
- `Measure-CodeQuality.ps1` for the Http library found one member over the complexity gate (`KeepsAlive`, 14), full coverage otherwise; it was split (`PersistsOverHttp1`) and the library's tests rerun. Not re-measured.
- The tests use in-memory connections and a fake clock, so they are platform-neutral.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: f09fb5ef 3d836d5e.
- 2026-11-25: Doing -> Done. A 300+ status after the wait ran out now stops an HTTP/2 upload as curl 8.18.0 does; build clean, fast tests green.
