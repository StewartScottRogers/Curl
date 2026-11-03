---
id: BL-4621
title: Conformance: Kafka zstd Huffman-coded literals cases for kafka://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4481, BL-4620]
touches: [Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/Entropy/Huffman, Curl.Conformance.Extended.UnitTests/Kafka/Compression/Entropy/Literals, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4621 — Conformance: Kafka zstd Huffman-coded literals cases for kafka://

## Goal

Curl's `kafka://` fetch of zstd frames whose compressed blocks carry Huffman-coded literals is measured by cases.

## Context

Part of BL-4572's split. Add a Huffman encoder (RFC 8878 4.2) and a literals section writer under `Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/Entropy/Huffman/`: a tree description with direct weights and with FSE-compressed weights (using BL-4620's FSE encoder), one stream and four streams (with the jump table), and Treeless_Literals_Block reusing the previous block's tree. Compose it into zstd frames beside `KafkaReferenceZstdFrameWriter` (without editing that file, or widen touches per the lane rules). Cases go in `Curl.Conformance.Extended.UnitTests/Kafka/Compression/Entropy/Literals/`, run curl in process via `KafkaCompressionCases.FetchAsync`, and follow `Documentation/Conformance/Extended/README.md` and `KnownFailures.txt` (a library defect is listed with a filed High `Fix Kafka conformance: <requirement>` task). BL-4622 maps the matrix rows, so record here the case names for `Kafka:RFC8878-3.1.1.3.1-2` under Notes.

## Acceptance criteria

- [ ] Cases cover Huffman literals in one stream and in four streams, treeless literals and FSE-compressed weights, each asserting stdout, stderr and exit code.
- [ ] Every failing case is fixed in this task or listed in `KnownFailures.txt` with its filed task.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] The code added under `Kafka/Compression/Entropy/Huffman/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Filed by BL-4572's split for size (2026-11-02).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: afe1d32d 0fc765d5 914c30cc 8fea9d62.
