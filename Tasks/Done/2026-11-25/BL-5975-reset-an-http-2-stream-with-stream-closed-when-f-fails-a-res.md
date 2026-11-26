---
id: BL-5975
title: Reset an HTTP/2 stream with STREAM_CLOSED when -f fails a response that ended it with the upload unsent
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.RequestBodySending.cs, Curl.Original.Protocol.Http.UnitTests/Http2ContinueWaitStatusTests.cs, Documentation/Planning/Decisions/ADR-1404-a-stopped-http-2-upload-resets-its-stream-and-a-held-body-sent-to-an-ended-stream-exits-55.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5975 — Reset an HTTP/2 stream with STREAM_CLOSED when -f fails a response that ended it with the upload unsent

## Goal

`-f` on a status that ended an HTTP/2 stream during the wait for `100 Continue` resets the stream as curl does.

## Context

Found by BL-5972 (ADR-1404). Measured, curl 8.18.0 nghttp2 build: `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H! :status: 500|content-length: 0'`, `--http2-prior-knowledge -sS -v -f -H "Expect: 100-continue" -d x=1`: exit 22, `Connection #0 ... left intact`, then `RST_STREAM` stream 1 error 5 (STREAM_CLOSED) before GOAWAY. Curl sends no RST_STREAM: `-f` fails the exchange before `FinishSendingBodyAsync` resets the stream, and `Http2Session.ResetUnfinishedStreamsAsync` resets only streams whose response is unfinished.

## Acceptance criteria

- [x] The measured `-f` case sends one `RST_STREAM` STREAM_CLOSED on stream 1 before GOAWAY, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- A failed exchange resets its stream at once when the response has ended and the request has not (`Http2StreamConnection.ResetUnendedRequestOfEndedResponseAsync`), not at shutdown, so the reset precedes any later stream's HEADERS and covers any failure in that state, as curl resets any stream nghttp2 has not closed. A stream whose response is unfinished is still left to `Http2Session.ShutDownAsync`. A failed write of the reset is ignored, so the transfer keeps its own exit code. Recorded in ADR-1404, which said the reset was missing.
- Built Curl against the recorder with the measured case: exit 22, `RST_STREAM stream 1 error 5`, then GOAWAY.
- Curl still writes `closing connection #0` there where curl writes `left intact`: filed BL-5977.
- `HttpProtocolHandler.ResponseExchange.cs` and `.RequestBodySending.cs` and ADR-1404 added to touches (the failure path is in the handler); `Http2Session.cs` needed no change. No other task was in Doing on origin/work/dark-factory.
- Build clean. Fast tests: every project passed in the one full run except a new test's extra connection-line assertion (BL-5977's gap); with it removed the Http project passes (2866 passed, 44 skipped). `Measure-CodeQuality.ps1` for the Http library: 0 failing members. Tests use in-memory connections and a fake clock, so are platform-neutral.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 6b938ef5 3d836d5e.
- 2026-11-25: Doing -> Done. A failed HTTP/2 exchange whose response ended with the upload unsent resets its stream with STREAM_CLOSED, as curl does under -f
