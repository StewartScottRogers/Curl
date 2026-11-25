---
id: BL-5956
title: Report empty HTTP/2 data once, after the trailer, for a response with no length and no body
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5946, BL-5948]
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseBody.cs, Curl.Original.Protocol.Http.UnitTests/Http2EmptyResponseDataLineTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5956 — Report empty HTTP/2 data once, after the trailer, for a response with no length and no body

## Goal

`curl -v` over HTTP/2 writes one `{ [0 bytes data]` line, after the trailer, for a response with no `content-length` and no body bytes that a trailer ends.

## Context

Measured in BL-5946, curl 8.18.0 (nghttp2, WSL), `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H :status: 200','H! x-t: v'`, `--http2-prior-knowledge -sS -v`: `< ` , `< x-t: v`, `{ [0 bytes data]`. Curl writes `{ [0 bytes data]` before `< x-t: v`, from `HttpResponseBodyReader.EndAtClose`, which runs before the trailers are reported. `HttpProtocolHandler.ReportEmptyStreamData` (`HttpProtocolHandler.ResponseBody.cs`) adds none in that case so as not to report twice. For an HTTP/2 stream the reader's event should be left to that method. HTTP/1.x and HTTP/3 behaviour must not change.

## Acceptance criteria

- [x] A unit test on the transfer's recorded events shows exactly one `{` data event, after the trailer line, for the measured case.
- [x] The existing tests in `Http2EmptyResponseDataLineTests` still pass.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The handler gives the body reader the HTTP/2 stream (`HttpResponseBodyReader.Http2Stream`,
  set in `CopyBodyAsync`, which also covers the h2c-upgraded stream). The reader's
  read-to-close empty event is skipped only when that stream ended with empty data
  (`EndedWithEmptyData`), and `ReportEmptyStreamData` then reports it after the trailers.
  A flag is used rather than a type test on the reader's connection, which may be a wrapper.
- A response without a length ended by RST_STREAM NO_ERROR is not an empty-data end, so
  the reader still reports its one event there, as before.
- HTTP/1.x and HTTP/3 leave `Http2Stream` null, so their behaviour is unchanged.
- No new measurement and no ADR: the expected order is the BL-5946 measurement in Context.
- Checks: `dotnet build` 0 warnings, fast tests green (HTTP unit tests 2822 passed, 44
  skipped), `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`
  0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: bf8d2576 7b589aec 3d836d5e.
- 2026-11-25: Doing -> Done. curl -v over HTTP/2 writes one empty data line, after the trailer, for a response with no length and no body
