---
id: BL-1299
title: Refuse zstd bytes that cannot begin a frame magic as soon as they arrive, as libzstd does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Zstandard.UnitLibrary, Curl.Zstandard.UnitTests]
requirement: FR-073
created: 2026-10-02
completed: 2026-10-03
---
# BL-1299 — Refuse zstd bytes that cannot begin a frame magic as soon as they arrive, as libzstd does

## Goal

`ZstandardDecoder.Decompress` returns `OperationStatus.InvalidData` with `ZstandardDecodeError.PrefixUnknown` as soon as the 1 to 3 bytes it holds where a frame magic belongs cannot be the start of the Zstandard magic or a skippable-frame magic, as libzstd does, instead of waiting for all four bytes.

## Context

- libzstd 1.5.7 (the zstd curl 8.21.0 links on Windows, per `curl --version`: `zstd/1.5.7`), `lib/decompress/zstd_decompress.c` `ZSTD_getFrameHeader_advanced` (tag `v1.5.7`, https://github.com/facebook/zstd/blob/v1.5.7/lib/decompress/zstd_decompress.c): with fewer than `minInputSize` bytes it copies the bytes it has over the Zstandard magic (`28 B5 2F FD`) and, if that is not the magic, over the skippable start (`50 2A 4D 18`, mask `0xFFFFFFF0`); if neither matches it returns `prefix_unknown` at once. `ZSTD_decompressStream` runs it on every call while loading a frame header, so a partial magic is judged as each byte arrives.
- So after a complete frame, or at the start of the body, one byte that is not `0x28` and not `0x50`-`0x5F`, or two bytes `28 xx` with `xx` not `B5`, and so on, already fail. Bytes that are a valid prefix (`28 B5`, `28 B5 2F FD`) wait for more, and a body that ends there is not an error (measured below).
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1` serving `Content-Encoding: zstd` bodies to `curl -s -S --compressed`:
  - a 2-byte raw-block frame `28 B5 2F FD 20 02 11 00 00 6F 6B` followed by `78`: curl writes `ok` and exits 61 (`Unrecognized or bad HTTP Content or Transfer-Encoding`); Curl writes `ok` and exits 0;
  - the same frame followed by `78 78 78 78`: both exit 61;
  - the frame followed by `28 B5 2F FD`, or the body `28 B5` alone: both exit 0.
- Curl today, `Curl.Zstandard.UnitLibrary/ZstandardDecoder.cs`: `Stage.Magic` gathers `MagicLength` (4) bytes in `field` before `ReadMagic` checks them, so 1-3 trailing bytes never fail. The `HttpContentCodingDecoder` caller in `Curl.Protocol.Http.UnitLibrary` already turns `InvalidData` into exit 61; no change is needed there.

## Acceptance criteria

- [x] Tests in `Curl.Zstandard.UnitTests` (`ZstandardDecoderTests`) assert that after the measured frame, `Decompress` given `78` returns `InvalidData` with `LastError == PrefixUnknown`, and the same for `28 00`, `28 B5 00`, `28 B5 2F 00`, `40`, `50 00`, `5F 2A 4D 00`.
- [x] Tests assert that `28`, `28 B5`, `28 B5 2F`, `50`, `5A 2A`, `53 2A 4D` return `NeedMoreData` and consume their bytes, and that feeding the rest of a valid magic afterwards still decodes the next frame.
- [x] A test pins the same early refusal at the very start of the stream (first byte `78`), and one feeds a valid frame a byte at a time to show nothing else changes.
- [x] `ZstandardDecoder.TryDecompress` keeps its current results for complete inputs (its existing tests pass unchanged).
- [x] `dotnet build Curl.Zstandard.UnitTests -warnaserror` is clean; `dotnet test Curl.Zstandard.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Zstandard.UnitLibrary` reports no failing member.

## Notes

- Delivered directly rather than through every `/feature` stage: the change is one guard in one method, and the task's Context already pins libzstd's behaviour and the measured curl results.
- `ZstandardDecoder.GatherField` now asks `CanBeginMagic` whenever it holds 1 to 3 bytes of a magic: they must be a prefix of `28 B5 2F FD`, or a byte `0x50`-`0x5F` followed by a prefix of `2A 4D 18`. If neither, it fails with `PrefixUnknown` at once; the bytes it took stay consumed, as when a whole 4-byte bad magic fails.
- Measurement: `Measure-CodeQuality.ps1 -Library` still runs the whole solution's tests (over 30 minutes), so coverage came from `dotnet test Curl.Zstandard.UnitTests --collect:"Code Coverage;Format=cobertura"` and then `Measure-CodeQuality.ps1 -Library Curl.Zstandard.UnitLibrary -SkipTestRun -ResultsDirectory <that dir>`: 100% line, 100% branch, max complexity 10, 0 failing members.
- Tests: Curl.Zstandard.UnitTests 272 passed; Curl.Protocol.Http.UnitTests and Curl.Tls.UnitTests (the two libraries that reference Zstandard) and Curl.Console.UnitTests also green; `dotnet build Curl.slnx` clean.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-03: Doing -> Done. zstd bytes that cannot begin a frame magic now fail with PrefixUnknown as they arrive, as libzstd does (exit 61 in curl)
