---
id: BL-5979
title: Stop an HTTP/3 upload when a status of 300 or above arrives after the 100 Continue wait ran out
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.RequestBodySending.cs, Curl.Original.Protocol.Http.UnitTests/Http3ContinueWaitStatusTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5979 — Stop an HTTP/3 upload when a status of 300 or above arrives after the 100 Continue wait ran out

## Goal

A status of 300 or above that arrives on an HTTP/3 stream while the body is being sent stops the upload, as HTTP/2 does.

## Context

Found by BL-5973 (ADR-1409), which stops an HTTP/2 upload there and leaves HTTP/3 sending the whole body, though ADR-1403 has HTTP/3 follow HTTP/2. `HttpContinueWaitConnection.StopsSending` is false when `Http2Stream` is null and the wait is on a stream. Measure an HTTP/3 curl if one is at hand; otherwise follow the HTTP/2 measurement in ADR-1409.

## Acceptance criteria

- [ ] A `500` on an HTTP/3 stream after the wait ran out, while the body is being sent, stops the body, writes ADR-1409's two stop lines, resets the stream and keeps the connection, pinned by unit tests.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 6ba8ef1f 3d836d5e.
