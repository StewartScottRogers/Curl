---
id: BL-301
title: Stream base64, quoted-printable and 7bit multipart file parts instead of reading them whole
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-274]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-301 — Stream base64, quoted-printable and 7bit multipart file parts instead of reading them whole

## Goal

A multipart file part under `;encoder=base64`, `quoted-printable` or `7bit` is encoded as it is sent, never held in memory whole, with the same bytes and framing as today.

## Context

- Found while delivering BL-274 (2026-09-26): ADR-0041 reads such a file whole while building, which holds a large file in memory once. curl 8.21.0 (`lib/mime.c`, `encoder_base64_read`, `encoder_qp_read`, `encoder_7bit_read`) encodes while reading.
- A `7bit` byte above 127 would then surface while sending; keep exit 26 and `read error getting mime data`, which may need the HTTP body writer to report a read failure other than as the end of the stream.

## Acceptance criteria

- [x] Every test in `MultipartFormBodyBuilderEncoderTests` still passes unchanged, except `AnEncodedFileThatCannotBeReadIsReadError`, which now names `7bit` instead of `base64` (see Notes).
- [x] A test shows a `base64` file part is not read before the body is read (`AnEncodedFilePartIsNotReadBeforeTheBodyIsRead`).
- [x] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core.UnitLibrary`.

## Notes

- Plan and decision: ADR-0076 (supersedes ADR-0041's file-part decision). `EncodedReadStream` encodes a buffer at a time through a `MultipartDataEncoding` (`PassThroughDataEncoding`, `Base64DataEncoding`, `QuotedPrintableDataEncoding`); `MultipartPartEncoder.Encode(byte[])` now runs the same encodings, so text parts and file parts share one implementation. It seeks by encoding again from the source's start, so 307/308 resends still work.
- The first two criteria conflicted: `AnEncodedFileThatCannotBeReadIsReadError` pinned a `base64` read failure at build time, which is only possible by reading the file before the body is read. The goal wins; that test now names `7bit`, which is still read through while building, and `AStreamedEncodedFileThatCannotBeReadFailsTheBodysRead` pins the new `base64` behaviour (the failure surfaces while sending, as for a file sent as it is). Decided by Claude under Stewart's delegation.
- `FormFileSystem`'s unreadable stream now fails `Read`/`ReadAsync` as well as `CopyToAsync`, as a real unreadable device does.
- A seekable `7bit` file is read through once while building (memory flat, read twice from disk) so exit 26 `read error getting mime data` still comes before a connection. An unseekable `7bit` file is still read whole: streaming it needs `HttpRequestBodyWriter` (`Curl.Protocol.Http.UnitLibrary`) to report a failed read as a failure, filed as BL-385.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0076 and ADR-0041's status; no task in `Doing` names it.
- Review (code-reviewer): no byte-level defects. An unseekable `base64`/`quoted-printable` file now leaves the body unseekable, so a 307/308 resend cannot rewind it, the same as an unencoded pipe; kept for flat memory and recorded in ADR-0076. `ConcatenatedReadStream.MoveTo` no longer asks a segment placed at its start for its length, so a rewind does not re-encode a quoted-printable file to measure it.
- `MultipartPartEncoder.KnowsEncodedLength` became `EncodedLength(long?)`, which gives the size too, since a streamed part no longer has its encoded bytes to count.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. base64, quoted-printable and seekable 7bit multipart file parts are encoded as they are sent, never held whole, with unchanged bytes
