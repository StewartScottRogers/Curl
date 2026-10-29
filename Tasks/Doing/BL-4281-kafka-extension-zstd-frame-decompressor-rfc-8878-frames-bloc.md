---
id: BL-4281
title: Kafka extension: zstd frame decompressor (RFC 8878 frames, blocks, literals, sequences)
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4280]
touches: [Curl.Extension.Kafka.UnitLibrary/Wire/Compression/Zstd/ZstdFrameDecompressor.cs, Curl.Extension.Kafka.UnitLibrary/Wire/Compression/Zstd/ZstdLiterals.cs, Curl.Extension.Kafka.UnitLibrary/Wire/Compression/Zstd/ZstdSequences.cs, Curl.Extension.Kafka.UnitTests/Wire/Compression/Zstd/ZstdFrameDecompressorTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4281 — Kafka extension: zstd frame decompressor (RFC 8878 frames, blocks, literals, sequences)

## Goal

Curl.Extension.Kafka.UnitLibrary decompresses zstd frames (RFC 8878), as Kafka's zstd codec 4 sends them.

## Context

Split from BL-4277 (which wires the decoders into Curl.Extension.Kafka.UnitLibrary/Wire/KafkaRecordBatch.cs; see ADR-1076). The BCL has no such codec, so the decoder is written by hand, decompression only, BCL only, as an internal static class in Wire/Compression that takes a ReadOnlySpan<byte> and returns the decompressed bytes, throwing FormatException with a message naming the codec on corrupt or truncated input. No compressor is installed on the lane machines (no snappy, lz4 or zstd binary): hand-assemble test vectors from the format specification byte by byte, with a comment per field, or write a C# file-based app; never Python. Builds on BL-4280's entropy tables. Frame header (window descriptor, dictionary ID, content size, single-segment flag; skippable frames skipped; content checksum may be skipped, not verified - record the choice), raw, RLE and compressed blocks, literals section (raw, RLE, Huffman with 1 or 4 streams, treeless), sequences section (predefined, RLE, FSE and repeat modes), sequence execution with repeat offsets.

## Acceptance criteria

- [ ] ZstdFrameDecompressor decompresses frames with raw, RLE and compressed blocks covering every literals and sequences mode, checked by unit tests against hand-assembled vectors.
- [ ] A wrong magic number, a dictionary ID other than 0, truncated input and an offset before the start of the output throw FormatException naming zstd.
- [ ] The library meets the four quality gates and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b8e6bd36 8adc22a9 dc514177.
