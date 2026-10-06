---
id: BL-1300
title: Give no output from a zstd Decompress call that fails, as libzstd's ZSTD_decompressStream does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1299]
touches: [Curl.Zstandard.UnitLibrary, Curl.Zstandard.UnitTests]
requirement: FR-073
created: 2026-10-02
completed: 2026-10-03
---
# BL-1300 — Give no output from a zstd Decompress call that fails, as libzstd's ZSTD_decompressStream does

## Goal

A `ZstandardDecoder.Decompress` call that returns `OperationStatus.InvalidData` reports `bytesWritten` 0 and `bytesConsumed` 0, whatever it decoded into the destination before it found the error, as libzstd's `ZSTD_decompressStream` leaves `output->pos` and `input->pos` unmoved on an error; so a frame whose checksum or content size is wrong writes none of that call's bytes, as curl 8.21.0 does.

## Context

- libzstd 1.5.7 `lib/decompress/zstd_decompress.c` (tag `v1.5.7`, https://github.com/facebook/zstd/blob/v1.5.7/lib/decompress/zstd_decompress.c) `ZSTD_decompressStream`: every error leaves through `FORWARD_IF_ERROR`/`RETURN_ERROR` before the function's last lines set `input->pos` and `output->pos`, so a failing call reports no progress even though it wrote into the buffer. A call also never runs past the end of a frame (it returns 0 there), as Curl's `Decompress` returns `Done` per frame.
- curl 8.21.0 `lib/content_encoding.c` (tag `curl-8_21_0`) `zstd_do_write`, lines 553-579: each call gets a fresh 16384-byte buffer (`DECOMPRESS_BUFFER_SIZE`, line 62); on `ZSTD_isError` it returns `CURLE_BAD_CONTENT_ENCODING` (exit 61) without writing `out.pos` bytes, and only a successful call's `out.pos` bytes are written.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1` serving `Content-Encoding: zstd` to `curl -s -S --compressed`:
  - `28 B5 2F FD 24 02 11 00 00 6F 6B 00 00 00 00` (a single-segment frame of `ok` with a wrong `Content_Checksum`): curl writes nothing and exits 61; Curl writes `ok` and exits 61;
  - `28 B5 2F FD 20 03 11 00 00 6F 6B` (a `Frame_Content_Size` of 3 over 2 bytes of content): curl writes nothing and exits 61; Curl writes `ok` and exits 61.
- Curl today: `Curl.Zstandard.UnitLibrary/ZstandardDecoder.cs` `Decompress` returns the `Consumed` and `Written` counts of the call that failed. Its only production caller, `HttpContentCodingDecoder.DecodeZstandard` (`Curl.Protocol.Http.UnitLibrary`), yields any written bytes before it checks the status, and so needs no change once the count is 0; its output buffer is also 16384 bytes (`OutputSize`).
- `TryDecompress` (one-shot) returns `false` on any error; its `bytesWritten` on failure follows the same rule.

## Acceptance criteria

- [x] Tests in `Curl.Zstandard.UnitTests` feed each measured frame to `Decompress` in one call with a 16384-byte destination and assert `InvalidData`, `bytesWritten == 0`, `bytesConsumed == 0`, and `LastError` `ChecksumWrong` and `CorruptionDetected` respectively.
- [x] A test feeds a two-block frame whose checksum is wrong with the first block in one call and the rest in a second: the first call reports its block's bytes and `NeedMoreData`, the second reports 0 written and `InvalidData`, pinning that only the failing call's output is withheld.
- [x] A test with a destination smaller than the frame pins that calls returning `DestinationTooSmall` before the failing call keep their counts.
- [x] The `ZstandardDecoder.Decompress` doc comment states the rule and cites `ZSTD_decompressStream`.
- [x] `dotnet build Curl.Zstandard.UnitTests -warnaserror` is clean; `dotnet test Curl.Zstandard.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Zstandard.UnitLibrary` reports no failing member.

## Notes

- Plan: `Decompress` reports `bytesConsumed` and `bytesWritten` of 0 whenever it returns `InvalidData`; every earlier call keeps its counts. No caller change: `HttpContentCodingDecoder.DecodeZstandard` writes only what the call reports, and `TryDecompress` accumulates only reported bytes.
- The existing test `Decompress_FirstByteCannotBeginAMagic_FailsAtOnceWithPrefixUnknown` expected 1 consumed; it now pins 0, as `ZSTD_decompressStream` leaves `input->pos` unmoved on an error.
- Verified: Curl.Zstandard.UnitTests 276 passed; Curl.Protocol.Http.UnitTests 1759 passed (the only production caller); `dotnet build` clean; Measure-CodeQuality on Curl.Zstandard.UnitLibrary 100% line, 100% branch, 0 failing members (coverage from the Zstandard tests alone, `-SkipTestRun`: the whole-solution run exceeded 50 minutes in Curl.Protocol.Ssh.UnitTests and its MSBuild nodes then died, with no test failing up to that point).

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. A failing zstd Decompress call reports nothing consumed or written, so curl writes none of its output, as libzstd does
