---
id: BL-365
title: Match curl 8.21.0 on bytes after the end of a gzip stream in a decoded body
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-315]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-365 — Match curl 8.21.0 on bytes after the end of a gzip stream in a decoded body

## Goal

A decoded gzip body (Content-Encoding under `--compressed`, or Transfer-Encoding under `--tr-encoding`) with bytes after the end of its gzip stream gives the output, exit code and message curl 8.21.0 gives.

## Context

- Found while measuring BL-315 (2026-09-27). curl 8.21.0 (mingw, `Record-CurlExchange.ps1 -Port 18180`) with `-sS --tr-encoding` against `HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip\r\n\r\n` followed by the 25-byte gzip stream of `hello` and then `XYZ`, closing: stdout `hello`, stderr `curl: (23) Failed writing received data to disk/application`, exit 23.
- `HttpContentCodingDecoder` reads with the BCL's `GZipStream`, which treats trailing bytes as a further gzip member and so fails differently (probably exit 61 `Unrecognized or bad HTTP Content or Transfer-Encoding`; not yet checked).
- Still to measure: the same body as `Content-Encoding: gzip` with `--compressed`, trailing bytes after a `deflate` (zlib) stream, and a second complete gzip member after the first.

## Acceptance criteria

- [ ] The case above and each case still to measure are measured on curl 8.21.0, recorded in Notes, and pinned in `Curl.Protocol.Http.UnitTests` tests with 1-byte reads.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`.

## Notes

## Log

- 2026-09-27: Created.
