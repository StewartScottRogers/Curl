---
id: BL-5972
title: Reset an HTTP/2 stream whose upload a status stopped, and exit 55 when the response ends the stream before a held body is sent
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.RequestBodySending.cs, Curl.Original.Protocol.Http.UnitTests/Http2ContinueWaitStatusTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5972 — Reset an HTTP/2 stream whose upload a status stopped, and exit 55 when the response ends the stream before a held body is sent

## Goal

A stream whose upload a status stopped, or whose response ends it before a held body is sent, ends as curl ends it.

## Context

Found by BL-5970 (ADR-1403), curl 8.18.0 nghttp2 build, `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames ... -H "Expect: 100-continue" -d x=1`. (1) A `500` on the stream, ended or not: curl sends `RST_STREAM` error 5 (STREAM_CLOSED); Curl does so only while the response has not ended (`Http2StreamConnection.AbandonResponseAsync`). (2) A `200` that ends the stream (`H! :status: 200|content-length: 0`): curl sends the DATA after `Done waiting for 100-continue`, writes `stream 1 closed` and exits 55 `Failed sending data to the peer`; Curl sends no DATA (`SendDataAsync` stops at an ended response), writes `upload completely sent off: 3 bytes` and exits 0.

## Acceptance criteria

- [ ] A stopped upload resets its stream with STREAM_CLOSED as measured, pinned by a unit test.
- [ ] A `200` that ends the stream before the held body is sent gives the measured frames, `-v` lines and exit code, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
