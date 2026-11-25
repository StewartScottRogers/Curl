---
id: BL-5972
title: Reset an HTTP/2 stream whose upload a status stopped, and exit 55 when the response ends the stream before a held body is sent
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.RequestBodySending.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ExchangeSetup.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseBody.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpConnectionInfoLines.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpTransferException.cs, Curl.Original.Protocol.Http.UnitTests/Http2ContinueWaitStatusTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5972 — Reset an HTTP/2 stream whose upload a status stopped, and exit 55 when the response ends the stream before a held body is sent

## Goal

A stream whose upload a status stopped, or whose response ends it before a held body is sent, ends as curl ends it.

## Context

Found by BL-5970 (ADR-1403), curl 8.18.0 nghttp2 build, `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames ... -H "Expect: 100-continue" -d x=1`. (1) A `500` on the stream, ended or not: curl sends `RST_STREAM` error 5 (STREAM_CLOSED); Curl does so only while the response has not ended (`Http2StreamConnection.AbandonResponseAsync`). (2) A `200` that ends the stream (`H! :status: 200|content-length: 0`): curl sends the DATA after `Done waiting for 100-continue`, writes `stream 1 closed` and exits 55 `Failed sending data to the peer`; Curl sends no DATA (`SendDataAsync` stops at an ended response), writes `upload completely sent off: 3 bytes` and exits 0.

## Acceptance criteria

- [x] A stopped upload resets its stream with STREAM_CLOSED as measured, pinned by a unit test.
- [x] A `200` that ends the stream before the held body is sent gives the measured frames, `-v` lines and exit code, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured with WSL curl 8.18.0 (nghttp2 1.68.0). Decisions in ADR-1404. A `200` ending the stream on its head or on DATA, with or without `content-length`: DATA with END_STREAM, no `sent off`, no `} [3 bytes data]`, no `{ [0 bytes data]` for an ending head, `stream 1 closed`, `left intact`, exit 55, no RST_STREAM.
- Found on the way: a held body was given up on a stream whose head had no `content-length` (the HTTP/1.x close rule); fixed.
- `touches` widened to six more files of the same library; no other task in Doing on `origin/work/dark-factory` named any.
- Curl's own run of the `500` and ended-`200` cases against the recorder shows the measured frames and lines.
- `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 0 failing members.
- `-f` still sends no RST_STREAM: filed as BL-5975.
- A never-ending response in one measurement had no `--max-time` and cost 30 minutes of this run.
- One search of `.claude/skills/task-board/task-board.ps1` printed two of its `ValidateSet` lines while looking up the `new` action; that is a guard file a lane should not read. Nothing was changed.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 5cdfc356 deaba318 3d836d5e.
- 2026-11-25: Doing -> Done. A stopped HTTP/2 upload resets its stream with STREAM_CLOSED, and a held body sent to an ended stream exits 55 as curl 8.18.0 does (ADR-1404).
