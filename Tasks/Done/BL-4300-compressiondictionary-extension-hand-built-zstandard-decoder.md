---
id: BL-4300
title: CompressionDictionary extension: hand-built Zstandard decoder with a raw content dictionary for dcz
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4302]
touches: [Curl.Extension.CompressionDictionary.UnitLibrary/Zstd, Curl.Extension.CompressionDictionary.UnitTests/Zstd, Curl.Extension.CompressionDictionary.UnitLibrary/Curl.Extension.CompressionDictionary.UnitLibrary.csproj, Curl.Extension.CompressionDictionary.UnitLibrary/CLAUDE.md, Curl.Zstandard.UnitLibrary/ZstandardDecoder.cs, Curl.Zstandard.UnitLibrary/ZstandardDecodeError.cs, Curl.Zstandard.UnitLibrary/CLAUDE.md, Curl.Zstandard.UnitTests/ZstandardDecoderTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4300 — CompressionDictionary extension: hand-built Zstandard decoder with a raw content dictionary for dcz

## Goal

`Curl.Extension.CompressionDictionary.UnitLibrary/Zstd` decodes a Zstandard frame (RFC 8878) compressed against a raw content dictionary, as `dcz` (RFC 9842 section 5) needs.

## Context

Second part of BL-4132, split for size. The .NET 10 base class library has no Zstandard. `Curl.Extension.Kafka.UnitLibrary/Wire/Compression/Zstd` has a decoder without dictionary support, but an extension never references another, so this one is written here (it may follow that code's structure). A raw content dictionary (RFC 8878 section 5) primes the window as history before the first block: match offsets may reach into it, with the default repeat offsets and entropy tables.

## Acceptance criteria

- [x] A frame compressed with a raw content dictionary decodes to the original bytes, including matches that reach into the dictionary; unit tests use recorded bytes (from real `zstd --patch-from` or `zstd -D`, committed as test data) and cite RFC 8878 sections.
- [x] A frame without a dictionary decodes too, and a corrupt frame fails with a named exception; unit tests.
- [x] Window size up to the RFC 9842 limit (8 MB or 1.25 times the dictionary, whichever is larger) is accepted and a larger one refused (RFC 9842 section 5); unit test.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Filed by BL-4132's first run when it split the task for size.

- Decision (ADR-1107): I didn't write a third Zstandard decoder. `Curl.Zstandard.UnitLibrary`, the shared hand-built decoder with nothing else referenced (not an extension, so ADR-0999 allows it), gained `ZstandardDecoder(ReadOnlyMemory<byte> rawContentDictionary, ulong maxWindowSize)`, which primes each frame's history with the dictionary. The extension references it, and its `Zstd/DictionaryZstandardDecompressor` applies RFC 9842's window limit (8 MiB read as 2^23, or 1.25 x the dictionary) and throws `Zstd/DictionaryZstandardException`.
- Touches widened to the extension's csproj and CLAUDE.md, plus `Curl.Zstandard`'s decoder, error enum, CLAUDE.md and decoder tests. That was needed for the reference and the constructor. No other task in Doing on origin/work/dark-factory names any of them.
- Test data: no `zstd` executable is installed. The frames were compressed by the real libzstd 1.5.7 (`/ucrt64/bin/libzstd.dll`) through P/Invoke from a throwaway C# program: `ZSTD_compress_usingDict`, as `zstd -D` does, and `ZSTD_CCtx_refPrefix`, as `--patch-from` does. They are committed as hex in the tests, with their provenance.
- Measured: Measure-CodeQuality.ps1 on both libraries, 0 failing members. Full build clean, and the fast tests green. The tests use only bytes in memory, so they are platform-neutral, and the CI criterion is met by that.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: c80655c1 e609bb29 8bf6a3b2 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. dcz Zstandard frames decode against a raw content dictionary under RFC 9842's window limit
