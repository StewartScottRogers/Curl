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
completed: 2026-11-02
---
# BL-4620 — Conformance: reference zstd FSE encoder for Kafka compression cases

## Goal

The conformance library has a reference zstd FSE encoder (RFC 8878 section 4.1) that the Kafka Huffman-weight and sequences cases build on.

## Context

Part of BL-4572's split. BL-4481's `KafkaReferenceZstdFrameWriter` writes raw literals and RLE sequence codes only. Add, under `Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/Entropy/Fse/`: normalised counts from symbol frequencies (accuracy log within the limits of RFC 8878 4.1.1), the FSE table description writer (4.1.1), the encoding table and a backward bitstream writer (one and two interleaved states, as Huffman weights need), and the predefined literals-length, match-length and offset distributions (3.1.1.3.2.2). Unit tests in `Curl.Conformance.Extended.UnitTests/Kafka/Compression/Entropy/Fse/` round-trip through a small decoder in the tests, or check RFC 8878's predefined tables byte for byte. No curl cases here: BL-4621 and BL-4622 add them.

## Acceptance criteria

- [x] An FSE encoder in `Kafka/Compression/Entropy/Fse/` writes a table description and an encoded bitstream from symbols, and writes with the predefined distributions.
- [x] Unit tests check the table description, the predefined distributions and the bitstream (one and two states).
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] The code added under `Kafka/Compression/Entropy/Fse/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Filed by BL-4572's split for size (2026-11-02).
- Plan (direct, no architect stage: a self-contained reference encoder in one folder): `KafkaReferenceFseNormalizedCounts` (counts from frequencies, accuracy log 5 to 9; proportional, at least one per present symbol, the difference taken from or given to the largest), `KafkaReferenceFseTableDescriptionWriter` (4.1.1, zstd FSE_writeNCount's field rules), `KafkaReferenceFseEncodingTable` (the decoder's spread, predefined LL/ML/OF distributions of 3.1.1.3.2.2), `KafkaReferenceFseBackwardBitWriter`, and `KafkaReferenceFseEncoder` (one or two interleaved states, symbol i on state i % 2).
- Choice: the encoder inverts the decoding table (for a symbol before a known next state, the one state of that symbol whose baseline range holds it) rather than port zstd's compact CTable; the bits are the same and it is plainly correct for a reference encoder, at O(table size) per symbol.
- Tests: `FseTestDecoder` (written from the RFC) reads the description back and decodes the streams; a hand-worked description `[16,16]` log 5 = `10 3F`; predefined counts and the first states of Appendix A.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary, 0 failing members (FromFrequencies was split once for complexity). Build -warnaserror clean, fast tests green. Platform-neutral: pure computation, no files or paths.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: fab2f00f c5936357 914c30cc 3ead4734.
- 2026-11-02: Doing -> Done. A reference zstd FSE encoder (counts, table description, predefined distributions, one- and two-state bitstreams) under Kafka/Compression/Entropy/Fse
