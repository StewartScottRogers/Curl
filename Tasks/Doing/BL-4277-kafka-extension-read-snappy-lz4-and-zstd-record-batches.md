---
id: BL-4277
title: Kafka extension: read snappy, lz4 and zstd record batches
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4278, BL-4279, BL-4280, BL-4281]
touches: [Curl.Extension.Kafka.UnitLibrary/Wire/KafkaRecordBatch.cs, Curl.Extension.Kafka.UnitLibrary/Wire/Compression, Curl.Extension.Kafka.UnitTests/Wire/Compression, Curl.Extension.Kafka.UnitTests/Wire/KafkaRecordBatchTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4277 — Kafka extension: read snappy, lz4 and zstd record batches

## Goal

`kafka://` consumes record batches compressed with snappy (codec 2), lz4 (codec 3) and zstd (codec 4), not only none and gzip.

## Context

Follow-up of BL-4121 (ADR-1076): `KafkaRecordBatch.Decompressed` reads codecs 0 and 1 and ends any other with exit 8. The BCL has none of the three codecs, so each decoder is written by hand (decompression only), in `Wire/Compression/`: Kafka's snappy is xerial-framed ("SNAPPY" magic header) or raw, lz4 is the LZ4 frame format, zstd is RFC 8878.

## Acceptance criteria

- [ ] A batch with attributes codec 2, 3 or 4 is read and its record values written, checked against recorded batches in unit tests.
- [ ] A corrupt compressed batch ends with exit 8 and a message naming the codec.
- [ ] ADR-1076's "Left out" no longer lists the three codecs, or a new ADR supersedes that line.
- [ ] The library meets the four quality gates and the fast tests pass.

## Notes

- 2026-10-29 (lane 9): Sized before any edit: three hand-written decoders (snappy, LZ4 frame, zstd RFC 8878 with FSE and Huffman) at 100% line and branch coverage do not fit one run, and no compressor is installed to record vectors (no snappy, lz4 or zstd binary), so each needs hand-assembled vectors. Split into BL-4278 (snappy), BL-4279 (LZ4), BL-4280 (zstd entropy tables) and BL-4281 (zstd frames, after BL-4280), each in its own file under Wire/Compression. What is left here once they are Done: call them from KafkaRecordBatch.Decompressed for codecs 2, 3 and 4, map their FormatException to exit 8, the batch-level tests, and ADR-1076's "Left out" line.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5d9d18fd 855c0fa4 0dc0e836 dc514177.
- 2026-10-29: Doing -> Backlog. Waits on BL-4278, BL-4279, BL-4280 and BL-4281, the snappy, LZ4 and zstd decoders it wires in
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5d9d18fd 855c0fa4 0dc0e836 dc514177.
