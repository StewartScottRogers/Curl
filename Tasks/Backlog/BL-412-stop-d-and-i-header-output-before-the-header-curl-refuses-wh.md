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
completed:
---
# BL-412 — Stop -D and -i header output before the header curl refuses while reading the head

## Goal

When a response header is refused while curl reads the head (more than five Content-Encoding codings under `--compressed`, an invalid Content-Length), `-D -` and `-i` write only the head lines before that header, as curl 8.21.0 does, instead of the whole head.

## Context

- Measured in BL-364 (2026-09-27), curl 8.21.0 mingw with `Record-CurlExchange.ps1`: `-sS --compressed -D -` (and `-I`) against `HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Encoding: identity x6\r\n\r\nhello` writes `HTTP/1.1 200 OK\r\nContent-Length: 5\r\n` to stdout, then exit 61. With `Content-Length: x` before the Content-Encoding and `-I`, stdout is `HTTP/1.1 200 OK\r\n` only, exit 8.
- Curl's `HttpProtocolHandler.ExchangeAsync` writes `exchange.Head.HeadBytes` whole before `HttpResponseBodyReader.ThrowIfTooManyContentCodings` (BL-364) and before the body framing checks Content-Length and Transfer-Encoding, so the refused header and those after it are written too.
- Measure first whether the Transfer-Encoding refusals (BL-171, BL-315) stop header output the same way; the blank line ending the head is presumably not written either.

## Acceptance criteria

- [ ] The two cases above are pinned in `Curl.Protocol.Http.UnitTests` with 1-byte reads, header output byte for byte, and the Transfer-Encoding refusals are measured, recorded in Notes and pinned.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`.

## Notes

## Log

- 2026-09-27: Created.
