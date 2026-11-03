---
id: BL-4622
title: Conformance: Kafka zstd FSE-coded sequences cases and matrix mapping for kafka://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4481, BL-4620, BL-4621]
touches: [Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/Entropy/Sequences, Curl.Conformance.Extended.UnitTests/Kafka/Compression/Entropy/Sequences, Documentation/Conformance/Extended/Kafka-Compression.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4622 — Conformance: Kafka zstd FSE-coded sequences cases and matrix mapping for kafka://

## Goal

Curl's `kafka://` fetch of zstd frames whose sequences use FSE tables is measured, and the two `n/a` rows in `Kafka-Compression.md` map to cases.

## Context

The last part of BL-4572's split. Add a sequences section writer under `Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/Entropy/Sequences/` using BL-4620's FSE encoder: Predefined_Mode, FSE_Compressed_Mode (with a table description) and Repeat_Mode (reusing the previous block's table). Cases go in `Curl.Conformance.Extended.UnitTests/Kafka/Compression/Entropy/Sequences/` with `KafkaCompressionCases.FetchAsync`, following `Documentation/Conformance/Extended/README.md` and `KnownFailures.txt`. Then map `Kafka:RFC8878-3.1.1.3.1-2` (to BL-4621's literals cases, named under its Notes) and `Kafka:RFC8878-3.1.1.3.2-2` in `Documentation/Conformance/Extended/Kafka-Compression.md` (split into finer IDs if useful).

## Acceptance criteria

- [ ] `Kafka:RFC8878-3.1.1.3.1-2` and `Kafka:RFC8878-3.1.1.3.2-2` in `Kafka-Compression.md` map to cases (split into finer IDs if useful), and the harness's matrix check passes.
- [ ] Cases cover sequences in Predefined_Mode, FSE_Compressed_Mode and Repeat_Mode, each asserting stdout, stderr and exit code.
- [ ] Every failing case is fixed in this task or listed in `KnownFailures.txt` with its filed task.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] The code added under `Kafka/Compression/Entropy/Sequences/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Filed by BL-4572's split for size (2026-11-02).

## Log

- 2026-11-02: Created.
