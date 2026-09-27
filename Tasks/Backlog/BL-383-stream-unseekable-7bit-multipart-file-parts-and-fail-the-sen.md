---
id: BL-383
title: Stream unseekable 7bit multipart file parts and fail the send on a refused byte
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-301]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-383 — Stream unseekable 7bit multipart file parts and fail the send on a refused byte

## Goal

A `-F 'f=@<pipe>;encoder=7bit'` file part that cannot seek is streamed through `EncodedReadStream` instead of read whole, and a byte above 127 met while sending fails the transfer with exit 26 and `read error getting mime data`, as curl 8.21.0 does.

## Context

- Found while delivering BL-301 (2026-09-27). ADR-0076 streams every encoded file part except an unseekable `7bit` one, which `MultipartFormBodyBuilder.AddEncodedFileAsync` still reads whole, because it cannot be checked while building and read again.
- `EncodedReadStream` already throws `MultipartDataRefusedException` (an `IOException`, message `read error getting mime data`) when a read reaches a refused byte.
- `HttpRequestBodyWriter.ReadAsync` (`Curl.Protocol.Http.UnitLibrary`) takes every failed read as the end of the body: a chunked body is then sent short with no error, and a body of known length fails with `client mime read EOF fail`. It needs to report a failed read of a multipart body as exit 26 with curl's message; measure what curl 8.21.0 prints (`lib/mime.c`, `encoder_7bit_read` returns `STOP_FILLING`/`READ_ERROR`) before pinning it.

## Acceptance criteria

- [ ] A test in `Curl.Core.UnitTests` shows an unseekable `7bit` file part is not read while building.
- [ ] A test in `Curl.Protocol.Http.UnitTests` shows a chunked multipart body whose read throws `IOException` mid-body fails with exit 26 and the message measured from curl 8.21.0.
- [ ] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for both libraries.

## Notes

## Log

- 2026-09-27: Created.
