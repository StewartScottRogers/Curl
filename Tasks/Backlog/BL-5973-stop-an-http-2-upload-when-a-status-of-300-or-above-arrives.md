---
id: BL-5973
title: Stop an HTTP/2 upload when a status of 300 or above arrives after the 100 Continue wait ran out
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionReuse.cs, Curl.Original.Protocol.Http.UnitTests/Http2ContinueWaitStatusTests.cs, Curl.Original.Protocol.Http.UnitTests/HttpContinueWaitConnectionTests.cs, Record-CurlExchange.ps1]
requirement: none
created: 2026-11-25
completed:
---
# BL-5973 — Stop an HTTP/2 upload when a status of 300 or above arrives after the 100 Continue wait ran out

## Goal

A status of 300 or above that arrives on an HTTP/2 stream while the body is being sent stops the upload as curl does.

## Context

Found by BL-5970 (ADR-1402). `HttpContinueWaitConnection.StopsSending` is false on a stream, so a status arriving after the wait ran out lets the whole body go. `HttpProtocolHandler.KeepsAlive` treats a cut upload as ending the connection, which is HTTP/1.x behaviour. `Record-CurlExchange.ps1 -Http2Frames` answers as soon as the HEADERS arrive, so measuring this needs a delayed answer added to it.

## Acceptance criteria

- [ ] curl is measured over HTTP/2 with a `500` sent after the wait ran out while a body is still being sent, and Curl matches its DATA frames, `-v` lines and exit code, pinned by unit tests.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
