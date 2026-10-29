---
id: BL-4274
title: Kafka extension: read snappy, lz4 and zstd record batches
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Kafka.UnitLibrary/Wire/KafkaRecordBatch.cs, Curl.Extension.Kafka.UnitLibrary/Wire/Compression, Curl.Extension.Kafka.UnitTests/Wire/Compression, Curl.Extension.Kafka.UnitTests/Wire/KafkaRecordBatchTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4274 — Kafka extension: read snappy, lz4 and zstd record batches

## Goal

`kafka://` consumes record batches compressed with snappy (codec 2), lz4 (codec 3) and zstd (codec 4), not only none and gzip.

## Context

Follow-up of BL-4121 (ADR-1075): `KafkaRecordBatch.Decompressed` reads codecs 0 and 1 and ends any other with exit 8. The BCL has none of the three codecs, so each decoder is written by hand (decompression only), in `Wire/Compression/`: Kafka's snappy is xerial-framed ("SNAPPY" magic header) or raw, lz4 is the LZ4 frame format, zstd is RFC 8878.

## Acceptance criteria

- [ ] A batch with attributes codec 2, 3 or 4 is read and its record values written, checked against recorded batches in unit tests.
- [ ] A corrupt compressed batch ends with exit 8 and a message naming the codec.
- [ ] ADR-1075's "Left out" no longer lists the three codecs, or a new ADR supersedes that line.
- [ ] The library meets the four quality gates and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
