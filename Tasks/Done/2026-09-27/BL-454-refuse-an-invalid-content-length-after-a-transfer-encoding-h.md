---
id: BL-454
title: Refuse an invalid Content-Length after a Transfer-Encoding header under --tr-encoding
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-454 — Refuse an invalid Content-Length after a Transfer-Encoding header under --tr-encoding

## Goal

Under `--tr-encoding`, a response whose invalid Content-Length comes after a Transfer-Encoding header fails with exit 8 `Invalid Content-Length: value`, as curl 8.21.0 does, instead of being accepted.

## Context

- Measured in BL-412 (2026-09-27), curl 8.21.0 mingw with `Record-CurlExchange.ps1`: `-sS --tr-encoding -D -` against `HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: x\r\nX-After: 1\r\n\r\n0\r\n\r\n` gives exit 8 `Invalid Content-Length: value`, header output `HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n`. Without `--tr-encoding`, and with `--raw`, it is the same, and Curl already matches those.
- Curl's `HttpResponseBodyFraming.OfRequested` checks only the Content-Length headers before the first Transfer-Encoding header (BL-315 Notes: "one after it is not read"), so it accepts this response with exit 0. BL-315 presumably measured a valid but disagreeing value, which curl does not trust; an invalid one is still parsed and refused (curl sets its ignore-Content-Length flag, but still parses the value).
- Measure first: a valid Content-Length after the Transfer-Encoding (expected accepted), two disagreeing valid ones after it, and `--ignore-content-length --tr-encoding`.
- `HttpResponseBodyReader.FindHeadRefusal` (BL-412) finds the refused header by checking runs of headers with `HttpResponseBodyFraming.Of`, so the fix there also cuts header output at the right place.

## Acceptance criteria

- [x] The case above and the cases still to measure are measured, recorded in Notes and pinned in `Curl.Protocol.Http.UnitTests` with 1-byte reads, header output byte for byte.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`.

## Notes

- Measured 2026-09-27, curl 8.21.0 Schannel (mingw), `Record-CurlExchange.ps1`, `-sS -D -` plus the options named. Every response has `X-After: 1` after the Content-Length.
  - `--tr-encoding`, `Transfer-Encoding: chunked` then `Content-Length: x`: exit 8 `Invalid Content-Length: value`, header output `HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n`. Same with `--tr-encoding --raw`, and with `Transfer-Encoding: identity` then `Content-Length: 5,x` (up to the identity line).
  - `--tr-encoding`, chunked then `Content-Length: 3` then `Content-Length: 4`: exit 8, header output up to `Content-Length: 3\r\n`. `Content-Length: 5`, chunked, `Content-Length: 6`: exit 8, up to the Transfer-Encoding line. So disagreement after the Transfer-Encoding is refused too, which contradicts BL-315's "one after it is not read".
  - Accepted, exit 0, whole head then `hello` from a `5\r\nhello\r\n0\r\n\r\n` body: chunked then `Content-Length: 3` (not trusted, the chunked body wins), chunked then `Content-Length: 99999999999999999999`, and `--tr-encoding --ignore-content-length` with `Content-Length: x`.
  - `--tr-encoding -I` with chunked then `Content-Length: x`: exit 8; Curl already matched this, since `-I` checks the Content-Length alone (BL-412).
- Fix: `HttpResponseBodyFraming.OfRequested` now checks every Content-Length header (`HttpContentLength.Find(headers)`) and still frames by the Transfer-Encoding when there is one. `HttpResponseBodyReader.FindHeadRefusal` then cuts header output at the refused header with no change of its own.
- No design choice was open: every pinned behaviour was measured, so no ADR.
- Pinned in `HttpProtocolHandlerTests.RefusedHeader.cs` (5 refused and 3 accepted cases, 1-byte and 65536-byte reads) and `HttpResponseBodyFramingTests.cs`. HTTP tests: 1088 passing. `Measure-CodeQuality.ps1`: `Curl.Protocol.Http.UnitLibrary` 100/100, no failing member. Its two `Curl.Console` failures are not from this change and are already filed (BL-432, BL-455, BL-462).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Under --tr-encoding an invalid or disagreeing Content-Length after Transfer-Encoding is exit 8, header output cut before it, as curl 8.21.0
