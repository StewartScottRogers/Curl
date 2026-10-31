---
id: BL-4279
title: Kafka extension: LZ4 frame decompressor
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Kafka.UnitLibrary/Wire/Compression/Lz4FrameDecompressor.cs, Curl.Extension.Kafka.UnitTests/Wire/Compression/Lz4FrameDecompressorTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4279 — Kafka extension: LZ4 frame decompressor

## Goal

Curl.Extension.Kafka.UnitLibrary decompresses LZ4 frame format payloads (magic 0x184D2204), as Kafka's lz4 codec 3 sends them.

## Context

Split from BL-4277 (which wires the decoders into Curl.Extension.Kafka.UnitLibrary/Wire/KafkaRecordBatch.cs; see ADR-1076). The BCL has no such codec, so the decoder is written by hand, decompression only, BCL only, as an internal static class in Wire/Compression that takes a ReadOnlySpan<byte> and returns the decompressed bytes, throwing FormatException with a message naming the codec on corrupt or truncated input. No compressor is installed on the lane machines (no snappy, lz4 or zstd binary): hand-assemble test vectors from the format specification byte by byte, with a comment per field, or write a C# file-based app; never Python. Frame: FLG/BD descriptor (content size, block checksum, content checksum and dictionary ID flags), header checksum byte (may be skipped, not verified), data blocks (high bit = stored uncompressed), end mark 0; block format: token, literal length extension bytes, 2-byte little-endian offset, match length extension, minimum match 4, last sequence literals only. Checksums (xxHash32) may be skipped rather than verified; record the choice.

## Acceptance criteria

- [x] Lz4FrameDecompressor decompresses frames with compressed and uncompressed blocks, long literal and match lengths, overlapping matches and the optional fields present, checked by unit tests against hand-assembled vectors.
- [x] A wrong magic number, truncated input and an offset of 0 or before the start of the output throw FormatException naming lz4.
- [x] The library meets the four quality gates and the fast tests pass.

## Notes

- Decision: header, block and content checksums (xxHash32) are skipped, not verified; the record batch CRC covers integrity. Linked blocks decode (matches may reach earlier blocks). Quality run: 0 failing members.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: acacedcc 619e8ef8 dc514177.
- 2026-10-29: Doing -> Done. Lz4FrameDecompressor with hand-assembled vector tests; quality gates pass
