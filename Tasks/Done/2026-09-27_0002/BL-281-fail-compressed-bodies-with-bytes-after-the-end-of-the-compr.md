---
id: BL-281
title: Fail --compressed bodies with bytes after the end of the compressed stream as curl does
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-177]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-281 — Fail --compressed bodies with bytes after the end of the compressed stream as curl does

## Goal

With `--compressed`, a gzip, deflate or br body that carries bytes after the end of its compressed stream ends the transfer as curl 8.21.0 does: the decoded data written, then exit 23.

## Context

- Found while delivering BL-177 (2026-09-26). `HttpContentCodingDecoder` in `Curl.Protocol.Http.UnitLibrary` decodes with the BCL streams fed through `HttpContentInput`.
- Measured on curl 8.21.0 (mingw, the Windows reference, ADR-0018) with `Record-CurlExchange.ps1`, `-s -S --compressed`, `Content-Length` covering the stream plus `41 42`: for zlib-wrapped deflate and for br, stdout is `hello` and stderr is `curl: (23) Failed writing received data to disk/application`, exit 23. gzip with the same trailing `41 42` also exits 23 with the same message.
- Today: zlib and br silently drop the trailing bytes (exit 0); gzip lets `GZipStream` read them as a second member header, which throws and gives exit 61.
- Unmeasured: a second complete gzip member after the first. Measure it before deciding.

## Acceptance criteria

- [x] For `deflate` (zlib), `br` and `gzip` bodies of `hello` followed by `41 42`, the handler writes `hello` and returns `CurlExitCode.WriteError` (23) with `Failed writing received data to disk/application`, pinned in a test fed with 1-byte, 7-byte and whole reads.
- [x] The behaviour for a second complete gzip member is measured, recorded in Notes and pinned in a test.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- 2026-09-27, measured on curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1`, `-s -S --compressed`, Content-Length covering the whole body: gzip `hello` followed by a second complete gzip member (`world`), by `1F`, by `1F 8B` or by `41 42` each write `hello` and exit 23 `Failed writing received data to disk/application`; the second member is not decoded. Raw deflate `hello` followed by `41 42` writes `hello` and exits 0.
- Probed the BCL: `GZipStream`, `ZLibStream` and `DeflateStream` read their source in blocks, never seek back, and drop bytes after their stream; `GZipStream` decodes a following member that starts `1F 8B`. The BCL has no span-based zlib decoder; it has `BrotliDecoder`.
- Decision (ADR-0070, decided by Claude under Stewart's delegation): br is decoded with `BrotliDecoder`, which reports what it consumed. The end of a gzip member or zlib stream is found from its checksum trailer: new `HttpContentChecksumTrailer` keeps a hand-rolled CRC-32 and size or Adler-32 of the decoded bytes (System.IO.Hashing is a package) and finds the first place the trailer appears in the encoded bytes. gzip is searched after each decoded piece so a second member is stopped before it writes; zlib only once the stream has finished (it stops reading `HttpContentInput`, now counting its reads), because 4 bytes could match by chance. Raw deflate keeps dropping trailing bytes, as measured.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0070 and its README index line; no task in Doing names it.
- Dropped the `InvalidOperationException` catch in `ReadDecoded`: it existed for `BrotliStream`, which is no longer used, and was uncovered.
- Tests: `Decode_BytesAfterTheEndOfTheStream_DecodesTheStreamThenThrowsExit23` (gzip, gzip+1F, second member, gzip label on zlib, zlib, br; 1, 7 and whole reads), raw deflate drop, 100000-byte bodies with and without a trailing byte, `HttpContentChecksumTrailerTests`, and the handler test `ExecuteAsync_CompressedBodyWithBytesAfterItsStream_WritesTheStreamThenReturnsExit23`.
- Gates: `dotnet build` clean, format clean, fast tests green (Http 873), `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Lanes cleaned up and cut from 6 to 3; the run had only just resumed and left no work.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --compressed gzip, zlib and br bodies with bytes after their stream (a second gzip member included) write the stream and exit 23 as curl 8.21.0 does
