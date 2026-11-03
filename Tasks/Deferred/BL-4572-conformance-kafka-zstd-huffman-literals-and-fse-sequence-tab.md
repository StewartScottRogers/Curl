---
id: BL-4572
title: Conformance: Kafka zstd Huffman literals and FSE sequence tables for kafka://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4481]
touches: [Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/Entropy, Curl.Conformance.Extended.UnitTests/Kafka/Compression/Entropy, Documentation/Conformance/Extended/Kafka-Compression.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4572 — Conformance: Kafka zstd Huffman literals and FSE sequence tables for kafka://

## Goal

Curl's `kafka://` fetch of zstd frames whose compressed blocks use Huffman-coded literals and FSE-coded sequences is
measured, and the two `n/a` rows BL-4481 left in `Documentation/Conformance/Extended/Kafka-Compression.md`
(`Kafka:RFC8878-3.1.1.3.1-2`, `Kafka:RFC8878-3.1.1.3.2-2`) are mapped to cases.

## Context

BL-4481 measured gzip, Snappy, LZ4 and zstd fetches with encoders the tests own in
`Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/`; its zstd writer (`KafkaReferenceZstdFrameWriter`) writes
raw literals and RLE sequence codes only, because the reference had no Huffman or FSE encoder. This task adds them, in
`Kafka/Compression/Entropy/` (RFC 8878 sections 3.1.1.3.1.1 to 3.1.1.3.2.2 and 4.1 to 4.2): a Huffman encoder writing a
tree description (direct weights, and FSE-compressed weights) and one or four streams; Treeless literals reusing the
previous block's tree; an FSE encoder for Predefined_Mode, FSE_Compressed_Mode (with a table description) and
Repeat_Mode. Cases go in `Curl.Conformance.Extended.UnitTests/Kafka/Compression/Entropy/`, run curl in process via
`KafkaCompressionCases.FetchAsync`, and follow the harness rules (`Documentation/Conformance/Extended/README.md`,
`KnownFailures.txt`: a library defect is listed with a filed High `Fix Kafka conformance: <requirement>` task).

## Acceptance criteria

- [ ] `Kafka:RFC8878-3.1.1.3.1-2` and `Kafka:RFC8878-3.1.1.3.2-2` in `Kafka-Compression.md` map to cases (split into
      finer IDs if useful), and the harness's matrix check passes.
- [ ] Cases cover Huffman literals in one stream and in four streams, treeless literals, FSE-compressed weights, and
      sequences in Predefined_Mode, FSE_Compressed_Mode and Repeat_Mode, each asserting stdout, stderr and exit code.
- [ ] Every failing case is fixed in this task or listed in `KnownFailures.txt` with its filed task.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] The code added under `Kafka/Compression/Entropy/` is at 100% line and branch coverage, complexity at most 10
      and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Split for size before any code (2026-11-02): a hand-built FSE encoder, a Huffman encoder and two sets of curl cases, each held to 100% coverage, do not fit one run. BL-4611 (FSE encoder), BL-4612 (Huffman literals cases, depends on BL-4611) and BL-4613 (FSE sequences cases and the matrix mapping, the rest, depends on both) carry every acceptance criterion between them. No other task names BL-4572 in depends-on.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 99c0a8f4 d822d055 0fc765d5 914c30cc c1434b50.
- 2026-11-02: Doing -> Deferred. Split into BL-4611, BL-4612, BL-4613
