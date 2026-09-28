---
id: BL-364
title: Reject a response listing more than five Content-Encoding codings under --compressed, as curl 8.21.0 does
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-315]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-364 — Reject a response listing more than five Content-Encoding codings under --compressed, as curl 8.21.0 does

## Goal

With `--compressed`, a response whose Content-Encoding headers list more than five codings fails with exit 61 `Reject response exceeding limit of 5 content encodings`, as curl 8.21.0 does.

## Context

- Found while measuring BL-315 (2026-09-27). curl 8.21.0 (mingw, `Record-CurlExchange.ps1 -Port 18180`) with `-sS --compressed` against `HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Encoding: identity, identity, identity, identity, identity, identity\r\n\r\nhello`: stdout empty, stderr `curl: (61) Reject response exceeding limit of 5 content encodings`, exit 61.
- Curl's `HttpContentDecoder` (Curl.Protocol.Http.UnitLibrary) has no limit today. BL-315 added the Transfer-Encoding equivalent in `HttpTransferEncoding.Requested` (limit 5, `identity` counted); the Content-Encoding limit is counted separately (a Transfer-Encoding gzip plus five Content-Encoding gzip was not rejected for the count, BL-315 Notes).
- Still to measure: five codings (expected accepted), whether an empty body with six is still refused (the limit is checked when the headers are read, not at the first body byte), and whether `--raw --compressed` skips it.

## Acceptance criteria

- [x] The six-coding case above is pinned in a `Curl.Protocol.Http.UnitTests` test with 1-byte reads, and the cases still to measure are measured, recorded in Notes and pinned.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`.

## Notes

- Measured 2026-09-27, curl 8.21.0 mingw, `Record-CurlExchange.ps1`, `-sS --compressed` unless stated, each case exit 61 `Reject response exceeding limit of 5 content encodings` with empty stdout unless stated:
  - six codings in one header, and three plus three in two headers: refused.
  - six with `Content-Length: 0`, and six with no Content-Length (body to the close): refused, so the limit is checked while the headers are read, not at the first body byte.
  - six unrecognized (`foo`), and `gzip,br,deflate,x-gzip,IDENTITY,foo`: refused; every coding counts, whatever it is.
  - `-I`: refused; `-D -` and `-I` write the head lines before the Content-Encoding header, then fail (filed as BL-412).
  - 404 with `-f`: 61, not 22; 302 with `-L`: 61, one connection, the redirect is not followed.
  - `Content-Length: x` before the Content-Encoding: exit 8 `Invalid Content-Length: value` (also with `-I`); after it: 61. `Transfer-Encoding: foo` before it: 61 `Unsolicited Transfer-Encoding (foo) found`; after it: the limit's 61. Headers are read in order.
  - accepted, exit 0, body `hello`: five codings; five with empty items (`identity,,identity, ,identity,identity,identity,`); `--raw --compressed` with six; six without `--compressed`; 204 and 304 with six (empty output); `--tr-encoding` with one Transfer-Encoding plus five Content-Encoding codings (the two limits are separate).
- Implementation: `HttpContentDecoder.MaximumCodings` and `IndexPastCodingLimit` find the Content-Encoding header that takes the count past five; `HttpResponseBodyReader.ThrowIfTooManyContentCodings` runs `HttpResponseBodyFraming.Of` over the headers before it (so an earlier invalid Content-Length or refused Transfer-Encoding wins) and throws exit 61; `HttpProtocolHandler.ExchangeAsync` calls it right after the head is written to header output, before `-f`, retries and redirects. No design choice was open: every behaviour pinned is measured, so no ADR.
- Pinned in `HttpProtocolHandlerTests.ContentEncodingLimit.cs` with 1-byte and 65536-byte reads (20 cases).
- Known gap left: header output still carries the whole head before the failure; BL-412 filed for it.
- `Measure-CodeQuality.ps1` also reports `DiskWriteOutFileOpener.TryOpen` in `Curl.Console` uncovered; that is outside this task's `touches` and not from this change.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --compressed refuses more than five Content-Encoding codings with exit 61 while the head is read, as curl 8.21.0 does
