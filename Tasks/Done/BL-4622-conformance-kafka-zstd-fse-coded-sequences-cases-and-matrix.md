---
id: BL-4622
title: Conformance: Kafka zstd FSE-coded sequences cases and matrix mapping for kafka://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4481, BL-4620, BL-4621]
touches: [Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/Entropy/Sequences, Curl.Conformance.Extended.UnitTests/Kafka/Compression/Entropy/Sequences, Documentation/Conformance/Extended/Kafka-Compression.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Conformance.Extended.UnitTests/Kafka/Compression/Entropy/Literals/KafkaZstdHuffmanLiteralsTests.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-03
---
# BL-4622 — Conformance: Kafka zstd FSE-coded sequences cases and matrix mapping for kafka://

## Goal

Curl's `kafka://` fetch of zstd frames whose sequences use FSE tables is measured, and the two `n/a` rows in `Kafka-Compression.md` map to cases.

## Context

The last part of BL-4572's split. Add a sequences section writer under `Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/Entropy/Sequences/` using BL-4620's FSE encoder: Predefined_Mode, FSE_Compressed_Mode (with a table description) and Repeat_Mode (reusing the previous block's table). Cases go in `Curl.Conformance.Extended.UnitTests/Kafka/Compression/Entropy/Sequences/` with `KafkaCompressionCases.FetchAsync`, following `Documentation/Conformance/Extended/README.md` and `KnownFailures.txt`. Then map `Kafka:RFC8878-3.1.1.3.1-2` (to BL-4621's literals cases, named under its Notes) and `Kafka:RFC8878-3.1.1.3.2-2` in `Documentation/Conformance/Extended/Kafka-Compression.md` (split into finer IDs if useful).

## Acceptance criteria

- [x] `Kafka:RFC8878-3.1.1.3.1-2` and `Kafka:RFC8878-3.1.1.3.2-2` in `Kafka-Compression.md` map to cases (split into finer IDs if useful), and the harness's matrix check passes.
- [x] Cases cover sequences in Predefined_Mode, FSE_Compressed_Mode and Repeat_Mode, each asserting stdout, stderr and exit code.
- [x] Every failing case is fixed in this task or listed in `KnownFailures.txt` with its filed task.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] The code added under `Kafka/Compression/Entropy/Sequences/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Filed by BL-4572's split for size (2026-11-02).
- Widened `touches` to `Literals/KafkaZstdHuffmanLiteralsTests.cs`: the matrix check maps each row to exactly one test, so mapping the literals row to BL-4621's five cases needed one ID per case; no task in Doing on origin/work/dark-factory names that file.
- Decided (sensible default): split both rows into finer IDs, one per case: `Kafka:RFC8878-3.1.1.3.1-2` to `-6` (one stream with direct weights, four streams, FSE-compressed weights, treeless reuse, treeless with no tree) and `Kafka:RFC8878-3.1.1.3.2-2` to `-6` (Predefined_Mode, FSE_Compressed_Mode, Repeat_Mode, Repeat_Mode with no earlier table, modes mixed in one block).
- Added `Entropy/Sequences/`: `KafkaReferenceZstdSequence` (literals length 0-15 and match length 3-34 as codes without extra bits, offsets written as Offset_Value above 3 so no repeat offset), `KafkaReferenceSequenceCodeTable` (mode, FSE table, description) and `KafkaReferenceSequencesSectionWriter` (raw literals plus the sequences section and backward bit stream, written in the reverse of the decoder's read order: initial LL, OF, ML states; per sequence OF extra bits, then LL, ML, OF updates).
- Measured: all ten Kafka zstd entropy cases pass against Curl's decoder, nothing added to KnownFailures.txt; Measure-CodeQuality for Curl.Conformance.Extended.UnitLibrary: 100% line, 100% branch, max complexity 10, 0 failing members. The cases are pure in-memory byte work, so they do not depend on the platform.

## Log

- 2026-11-02: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 99c0a8f4 e84b974d 0fc765d5 914c30cc 8ac986e4.
- 2026-11-03: Doing -> Done. kafka:// zstd FSE-coded sequences (Predefined, FSE_Compressed, Repeat) measured; literals and sequences matrix rows map to cases
