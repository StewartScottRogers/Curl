---
id: BL-5979
title: Stop an HTTP/3 upload when a status of 300 or above arrives after the 100 Continue wait ran out
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.RequestBodySending.cs, Curl.Original.Protocol.Http.UnitTests/Http3ContinueWaitStatusTests.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ExchangeSetup.cs, Documentation/Planning/Decisions/ADR-1403-an-http-2-or-http-3-request-waits-for-100-continue-when-h-asks-for-it.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5979 — Stop an HTTP/3 upload when a status of 300 or above arrives after the 100 Continue wait ran out

## Goal

A status of 300 or above that arrives on an HTTP/3 stream while the body is being sent stops the upload, as HTTP/2 does.

## Context

Found by BL-5973 (ADR-1409), which stops an HTTP/2 upload there and leaves HTTP/3 sending the whole body, though ADR-1403 has HTTP/3 follow HTTP/2. `HttpContinueWaitConnection.StopsSending` is false when `Http2Stream` is null and the wait is on a stream. Measure an HTTP/3 curl if one is at hand; otherwise follow the HTTP/2 measurement in ADR-1409.

## Acceptance criteria

- [x] A `500` on an HTTP/3 stream after the wait ran out, while the body is being sent, stops the body, writes ADR-1409's two stop lines, resets the stream and keeps the connection, pinned by unit tests.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decided in ADR-1411. No HTTP/3 curl was measured (the reference Windows curl has none), so it follows ADR-1409's HTTP/2 measurement.
- Added to touches: HttpProtocolHandler.ExchangeSetup.cs (gives the wait its HTTP/3 stream) and ADR-1403 (one sentence now true of HTTP/3). No other task in Doing names either.
- The QUIC stream buffers a write and returns at once, so no pending write needs cutting.
- A 500 during the wait now cancels the HTTP/3 stream too, as HTTP/2 resets it (ADR-1404).
- Quality run: 100% line and branch; it found two members over the complexity limit (ResetStoppedStreamUploadAsync 14, get_StopsSending 12), both then split into methods of at most 6 branches; not re-measured. The Http unit tests pass after the split (2892).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 6ba8ef1f 3d836d5e.
- 2026-11-25: Doing -> Done. HTTP/3 upload stops at a status of 300 or above after the 100 Continue wait (ADR-1411)
