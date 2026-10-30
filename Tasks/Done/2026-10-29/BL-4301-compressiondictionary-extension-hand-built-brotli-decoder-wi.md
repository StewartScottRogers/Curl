---
id: BL-4301
title: CompressionDictionary extension: hand-built Brotli decoder with a prefix dictionary for dcb
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4302]
touches: [Curl.Extension.CompressionDictionary.UnitLibrary/Brotli, Curl.Extension.CompressionDictionary.UnitTests/Brotli]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4301 — CompressionDictionary extension: hand-built Brotli decoder with a prefix dictionary for dcb

## Goal

`Curl.Extension.CompressionDictionary.UnitLibrary/Brotli` decodes a Brotli stream (RFC 7932) compressed against a prefix dictionary, as `dcb` (RFC 9842 section 4) needs.

## Context

Third part of BL-4132, split for size. The .NET 10 `BrotliDecoder` cannot attach a dictionary, so the decoder is built by hand here. It needs RFC 7932's static dictionary (122,784 bytes, RFC 7932 Appendix A) and transforms; commit the dictionary as an embedded resource and check its SHA-256 in a test. The prefix dictionary sits before the output as distance-reachable history (backward distances beyond the bytes written so far reach into it before the static dictionary), with a window of up to 16 MB (`lgwin` 24, RFC 9842 section 4). If the decoder alone is larger than one run, split it there (RFC 7932 core, then the prefix dictionary).

## Acceptance criteria

- [x] A stream compressed with a prefix dictionary decodes to the original bytes, including distances that reach into the dictionary; unit tests use recorded bytes (from the reference `brotli` tool with `-D`, committed as test data) and cite RFC 7932 sections.
- [x] Streams without a dictionary decode too, including static-dictionary references with transforms, metablock types and context modeling; a corrupt stream fails with a named exception; unit tests.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Filed by BL-4132's first run when it split the task for size.

- Plan: `BrotliDictionaryDecoder.Decode(compressed, prefixDictionary)` in `Brotli/`, one class per RFC 7932 part (bit reader, prefix codes, block switches, context maps, literal context, distances, static dictionary with transforms, compressed meta-block). ADR-1114 records the decisions.
- The static dictionary was taken from the reference libbrotlicommon 1.2.0 (its SHA-256 matches Appendix A) and is a Base64 constant in a .cs file rather than an embedded resource: that would have needed the project file, outside `touches`, and Base64 needs no resource lookup under AOT. The transform tables were read from the same library.
- Test data: streams recorded with brotli 1.2.0 (`-q 1/5/11`, `-w 10/16/18/22/24`, `-D`), held as Base64 in `BrotliRecordedStreams.cs` with the inputs' SHA-256 (no project file change for content files). The reference encoder used neither several block types nor NDIRECT, so `BrotliBlockSwitchTests` and `BrotliDistancesTests` hand-build those bits.
- Measured: a copy from the prefix dictionary enters the ring of last distances; the recorded dictionary streams decode only that way.
- Quality: Measure-CodeQuality for the library, 0 failing members, 100% line and branch. Build clean; fast tests green (89 test projects). The library's CLAUDE.md is outside `touches`; BL-4132 mentions the decoder when it wires it in.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 977d281c e7ae399a 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. BrotliDictionaryDecoder decodes Brotli streams with or without a prefix dictionary (dcb), held to the four gates
