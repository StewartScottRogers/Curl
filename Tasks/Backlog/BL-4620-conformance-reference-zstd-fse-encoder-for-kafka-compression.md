---
id: BL-4620
title: Conformance: reference zstd FSE encoder for Kafka compression cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4481]
touches: [Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/Entropy/Fse, Curl.Conformance.Extended.UnitTests/Kafka/Compression/Entropy/Fse]
requirement: none
created: 2026-11-02
completed:
---
# BL-4620 — Conformance: reference zstd FSE encoder for Kafka compression cases

## Goal

The conformance library has a reference zstd FSE encoder (RFC 8878 section 4.1) that the Kafka Huffman-weight and sequences cases build on.

## Context

Part of BL-4572's split. BL-4481's `KafkaReferenceZstdFrameWriter` writes raw literals and RLE sequence codes only. Add, under `Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/Entropy/Fse/`: normalised counts from symbol frequencies (accuracy log within the limits of RFC 8878 4.1.1), the FSE table description writer (4.1.1), the encoding table and a backward bitstream writer (one and two interleaved states, as Huffman weights need), and the predefined literals-length, match-length and offset distributions (3.1.1.3.2.2). Unit tests in `Curl.Conformance.Extended.UnitTests/Kafka/Compression/Entropy/Fse/` round-trip through a small decoder in the tests, or check RFC 8878's predefined tables byte for byte. No curl cases here: BL-4621 and BL-4622 add them.

## Acceptance criteria

- [ ] An FSE encoder in `Kafka/Compression/Entropy/Fse/` writes a table description and an encoded bitstream from symbols, and writes with the predefined distributions.
- [ ] Unit tests check the table description, the predefined distributions and the bitstream (one and two states).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] The code added under `Kafka/Compression/Entropy/Fse/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Filed by BL-4572's split for size (2026-11-02).

## Log

- 2026-11-02: Created.
