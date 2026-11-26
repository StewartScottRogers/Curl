---
id: BL-5970
title: Read the status of an HTTP/2 or HTTP/3 response during the 100 Continue wait, not an assumed 200
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs, Curl.Original.Protocol.Http.UnitTests/HttpContinueWaitConnectionTests.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.RequestBodySending.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.UploadReports.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpRequestFraming.cs, Curl.Original.Protocol.Http.UnitTests/Http2ContinueWaitStatusTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5970 — Read the status of an HTTP/2 or HTTP/3 response during the 100 Continue wait, not an assumed 200

## Goal

The wait for `100 Continue` on an HTTP/2 or HTTP/3 stream acts on the response's real status, as curl does.

## Context

Found by BL-5960. `HttpContinueWaitConnection.StatusCodeOf` parses the first line with `HttpStatusLine.Parse`, which gives an assumed 200 for the `HTTP/2 NNN ` and `HTTP/3 NNN ` lines the stream connections write (`HttpStatusLine.ParseHttp2OrHttp3` is the parser for those). So on a stream every status, a `100` or a `500` too, is waited out and the body is then sent. Measure curl 8.22.0 over HTTP/2 with `-H "Expect: 100-continue"` for a `100`, a `200` and a `500` (`Record-CurlExchange.ps1 -Http2`) before pinning anything. `HttpContinueWaitConnection.IsMultiplexedStream` marks the stream case.

## Acceptance criteria

- [x] For a `100`, a `200` and a `500` that arrive on an HTTP/2 stream during the wait, Curl sends or withholds the body and writes the `-v` lines as curl 8.22.0 does, pinned by unit tests.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.18.0 (nghttp2 1.68.0, WinGet), the only HTTP/2 curl here: the Schannel 8.22.0 reference has none, and no 8.22.0 nghttp2 build is at hand (as BL-3650). `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames`, `-v --expect100-timeout 1 -H "Expect: 100-continue" -d x=1`. `100`: head, body at once, `upload completely sent off`. `200` (stream open): head, `Done waiting for 100-continue`, body. `500`: head fields, `HTTP error before end of send, stop sending`, `abort upload`, empty line, no DATA, `RST_STREAM` 5, left intact.
- Curl was run against the same recorder: its `-v` lines for the `100`, open `200` and `500` match, apart from the `-m` timeout line (BL-5974).
- The Context was incomplete: `ForHttp2OrHttp3` turned the wait off, so Curl never waited on a stream and `IsMultiplexedStream` was never true in the handler. Fixed there (ADR-1403); a stream now holds the body after a final 2xx as HTTP/1.x does, in place of waiting the time out before the head is read. HTTP/3 follows HTTP/2, unmeasured.
- touches widened to RequestBodySending.cs, UploadReports.cs, ResponseExchange.cs, HttpRequestFraming.cs and Http2ContinueWaitStatusTests.cs; no other task in Doing on origin/work/dark-factory named them.
- Left for BL-5972 (stream reset; a `200` that ends the stream before the body goes: curl exits 55), BL-5973 (a 300+ status after the wait ran out) and BL-5974 (timeout connection line).
- The Linux and macOS side is platform-neutral: in-memory connections and a fake clock. `Measure-CodeQuality.ps1` for the Http library: 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: fe280dd3 3d836d5e.
- 2026-11-25: Doing -> Done. Over HTTP/2 and HTTP/3 Curl now waits for 100 Continue when -H asks and acts on the response's real status: 100 sends at once, 2xx sends after the wait, 300+ leaves the body unsent.
