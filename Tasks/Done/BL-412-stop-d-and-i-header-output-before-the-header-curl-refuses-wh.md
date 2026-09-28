---
id: BL-412
title: Stop -D and -i header output before the header curl refuses while reading the head
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-364]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-412 — Stop -D and -i header output before the header curl refuses while reading the head

## Goal

When a response header is refused while curl reads the head (more than five Content-Encoding codings under `--compressed`, an invalid Content-Length), `-D -` and `-i` write only the head lines before that header, as curl 8.21.0 does, instead of the whole head.

## Context

- Measured in BL-364 (2026-09-27), curl 8.21.0 mingw with `Record-CurlExchange.ps1`: `-sS --compressed -D -` (and `-I`) against `HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Encoding: identity x6\r\n\r\nhello` writes `HTTP/1.1 200 OK\r\nContent-Length: 5\r\n` to stdout, then exit 61. With `Content-Length: x` before the Content-Encoding and `-I`, stdout is `HTTP/1.1 200 OK\r\n` only, exit 8.
- Curl's `HttpProtocolHandler.ExchangeAsync` writes `exchange.Head.HeadBytes` whole before `HttpResponseBodyReader.ThrowIfTooManyContentCodings` (BL-364) and before the body framing checks Content-Length and Transfer-Encoding, so the refused header and those after it are written too.
- Measure first whether the Transfer-Encoding refusals (BL-171, BL-315) stop header output the same way; the blank line ending the head is presumably not written either.

## Acceptance criteria

- [x] The two cases above are pinned in `Curl.Protocol.Http.UnitTests` with 1-byte reads, header output byte for byte, and the Transfer-Encoding refusals are measured, recorded in Notes and pinned.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`.

## Notes

- Measured 2026-09-27, curl 8.21.0 mingw, `Record-CurlExchange.ps1`, `-sS -D -` unless stated. Refused: header output is the head lines before the refused header, no blank line, no body.
  - `--compressed`, `Content-Length: 5` then six `Content-Encoding: identity` headers: the first five are written, exit 61. One header with six codings: `HTTP/1.1 200 OK\r\nContent-Length: 5\r\n`. Same for `-i`.
  - `--compressed -I`, `Content-Length: x` before six codings: `HTTP/1.1 200 OK\r\n`, exit 8.
  - `Content-Length: x` after `X-Before: 1`: `HTTP/1.1 200 OK\r\nX-Before: 1\r\n`, exit 8, with `-D -`, `-i`, `-I` (so `-I` checks the Content-Length) and `-X HEAD`. `Content-Length: 5`, `X-Mid`, `Content-Length: 6`: up to `X-Mid`. A 404 with `-f`: exit 8, not 22. A 302 with `-L`: exit 8, not followed. After a `100` head: the 100 head whole, then the final lines before it. After a folded header: the folded line `X-Fold: a b\r\n`. After `Transfer-Encoding: chunked`, with or without `--raw`: exit 8 at it.
  - `Transfer-Encoding: foo` (exit 61 Unsolicited, also with `-X HEAD`), `chunked, identity` (61 listed after chunked), `--tr-encoding` with `chunked, gzip` (61 chunked not last) and with two headers of three `identity` (61 limit of 5 transfer encodings; the first header written): all stop before the refused header.
  - `-w "%{size_header} %{num_headers} %header{x-b2}|%header{content-length}"` with two headers before `Content-Length: x`: `39 2 2|`, so the report covers only the head before it.
  - Accepted, exit 0, whole head written: every Transfer-Encoding refusal with `-I` (with and without `--tr-encoding`); `Transfer-Encoding: foo` or `Content-Length: x` in a 204 or 304 (also `-I`); `Content-Length: x` with `--ignore-content-length` (also `-I`); `Transfer-Encoding: foo` with `--raw`.
- Found while measuring: under `--tr-encoding`, `Transfer-Encoding: chunked` then `Content-Length: x` is exit 8 on curl, but Curl accepts it (`HttpResponseBodyFraming.OfRequested` reads only the Content-Length before the first Transfer-Encoding, per BL-315). Out of scope here; filed as BL-453.
- Implementation: `HttpResponseBodyReader.FindHeadRefusal` (replacing `ThrowIfTooManyContentCodings`) returns the first refused header and its failure: it checks the framing headers (`HttpResponseBodyFraming.Of`, or the Content-Length alone for `-I`) and the Content-Encoding limit, and finds the refused header by binary search over runs of headers (each check fails at the first header it refuses, so refusal only grows with the run; a linear scan would be quadratic on a long head). `HttpResponseHeader.LineStart` records where each header line starts in the head bytes; `HttpResponseHead.Before` cuts the head there. `HttpProtocolHandler.ExchangeAsync` finds the refusal right after reading the head, then stores cookies, finds the redirect, writes header output and reports from the cut head, and throws before `-f`, retries and redirects. This also makes `-I` refuse an invalid Content-Length and makes the refusal win over `-f` and `-L` (both measured above); the framing check in `CopyAsync` stays and no longer fails.
- No design choice was open: every behaviour pinned is measured, so no ADR.
- Pinned in `HttpProtocolHandlerTests.RefusedHeader.cs` (32 cases, 1-byte and 65536-byte reads).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -D, -i and -I write only the head lines before a header curl refuses while reading the head (invalid Content-Length, refused Transfer-Encoding, too many Content-Encoding codings), and the refusal wins over -f and -L
