---
id: BL-4280
title: Kafka extension: zstd entropy decoding (backward bit reader, FSE and Huffman tables)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Kafka.UnitLibrary/Wire/Compression/Zstd/ZstdBackwardBitReader.cs, Curl.Extension.Kafka.UnitLibrary/Wire/Compression/Zstd/ZstdFiniteStateEntropyTable.cs, Curl.Extension.Kafka.UnitLibrary/Wire/Compression/Zstd/ZstdHuffmanTable.cs, Curl.Extension.Kafka.UnitTests/Wire/Compression/Zstd/ZstdBackwardBitReaderTests.cs, Curl.Extension.Kafka.UnitTests/Wire/Compression/Zstd/ZstdFiniteStateEntropyTableTests.cs, Curl.Extension.Kafka.UnitTests/Wire/Compression/Zstd/ZstdHuffmanTableTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4280 — Kafka extension: zstd entropy decoding (backward bit reader, FSE and Huffman tables)

## Goal

Curl.Extension.Kafka.UnitLibrary has the entropy decoding building blocks of zstd (RFC 8878): a backward bit reader, FSE table decoding (normalised counts from the stream, the predefined literal length, match length and offset tables, RLE) and Huffman table decoding (direct and FSE-compressed weights).

## Context

Split from BL-4277 (which wires the decoders into Curl.Extension.Kafka.UnitLibrary/Wire/KafkaRecordBatch.cs; see ADR-1076). The BCL has no such codec, so the decoder is written by hand, decompression only, BCL only, as an internal static class in Wire/Compression that takes a ReadOnlySpan<byte> and returns the decompressed bytes, throwing FormatException with a message naming the codec on corrupt or truncated input. No compressor is installed on the lane machines (no snappy, lz4 or zstd binary): hand-assemble test vectors from the format specification byte by byte, with a comment per field, or write a C# file-based app; never Python. This is the first half of the zstd decoder; BL-4281 builds frames, blocks, literals and sequences on it. RFC 8878 sections 4.1 (FSE), 4.2 (Huffman), 3.1.1.3.2.2 (predefined distributions).

## Acceptance criteria

- [x] ZstdBackwardBitReader, ZstdFiniteStateEntropyTable and ZstdHuffmanTable decode the RFC 8878 structures, checked by unit tests against hand-worked examples, including the predefined distributions' decoding tables.
- [x] Malformed table descriptions (accuracy log too large, counts not summing to the table size, weights not completing a power of two) throw FormatException naming zstd.
- [x] The library meets the four quality gates and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 0388c6d0 1219da95 dc514177.
- 2026-10-29: Doing -> Done. ZstdBackwardBitReader, ZstdFiniteStateEntropyTable and ZstdHuffmanTable done; quality gates met; fast tests green; platform-neutral
