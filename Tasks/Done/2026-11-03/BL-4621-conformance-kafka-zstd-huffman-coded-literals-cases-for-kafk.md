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
completed: 2026-11-02
---
# BL-4621 — Conformance: Kafka zstd Huffman-coded literals cases for kafka://

## Goal

Curl's `kafka://` fetch of zstd frames whose compressed blocks carry Huffman-coded literals is measured by cases.

## Context

Part of BL-4572's split. Add a Huffman encoder (RFC 8878 4.2) and a literals section writer under `Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/Entropy/Huffman/`: a tree description with direct weights and with FSE-compressed weights (using BL-4620's FSE encoder), one stream and four streams (with the jump table), and Treeless_Literals_Block reusing the previous block's tree. Compose it into zstd frames beside `KafkaReferenceZstdFrameWriter` (without editing that file, or widen touches per the lane rules). Cases go in `Curl.Conformance.Extended.UnitTests/Kafka/Compression/Entropy/Literals/`, run curl in process via `KafkaCompressionCases.FetchAsync`, and follow `Documentation/Conformance/Extended/README.md` and `KnownFailures.txt` (a library defect is listed with a filed High `Fix Kafka conformance: <requirement>` task). BL-4622 maps the matrix rows, so record here the case names for `Kafka:RFC8878-3.1.1.3.1-2` under Notes.

## Acceptance criteria

- [x] Cases cover Huffman literals in one stream and in four streams, treeless literals and FSE-compressed weights, each asserting stdout, stderr and exit code.
- [x] Every failing case is fixed in this task or listed in `KnownFailures.txt` with its filed task.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] The code added under `Kafka/Compression/Entropy/Huffman/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Filed by BL-4572's split for size (2026-11-02).
- Delivered directly rather than through the full /feature agent stages: the work is a small reference encoder in a conformance library plus cases, and the plan was clear from RFC 8878 4.2 and BL-4620's FSE encoder; one run with no subagents kept the cost down.
- Library (`Kafka/Compression/Entropy/Huffman/`): `KafkaReferenceHuffmanTree` (weights per symbol, last non-zero implied; canonical codes laid out as the decoder fills its table, weight 1 first; `Encode` writes one backward stream, first literal read first), `KafkaReferenceHuffmanTreeDescriptionWriter` (`WriteDirect`, four-bit weights; `WriteFseCompressed`, counts normalised from weight frequencies, two interleaved states through `KafkaReferenceFseEncoder`), `KafkaReferenceLiteralsSectionWriter` (`Compressed`, `Treeless`, one stream or four behind the jump table, three-byte header so sizes below 1024; `BlockOfLiteralsOnly` appends a zero-sequence sequences section). Composed through `KafkaReferenceZstdFrameWriter.Compressed(block, decodesTo)`, so that file is unchanged.
- Choice: the cases give the tree's weights by hand (as the other zstd cases lay blocks out by hand) rather than deriving them from frequencies; a frequency-to-weights builder adds code no case needs.
- Case names for `Kafka:RFC8878-3.1.1.3.1-2` (for BL-4622's matrix row), all in `KafkaZstdHuffmanLiteralsTests`: `Fetch_HuffmanLiteralsInOneStreamWithDirectWeights_WritesTheRecord`, `Fetch_HuffmanLiteralsInFourStreams_WritesTheRecord`, `Fetch_HuffmanLiteralsWithFseCompressedWeights_WritesTheRecord`, `Fetch_TreelessLiteralsReusingThePreviousBlocksTree_WritesTheRecord`, `Fetch_TreelessLiteralsWithNoEarlierTree_FailsWithWeirdServerReply`.
- Every case passes against Curl's zstd decoder, so `KnownFailures.txt` is unchanged and no fix task was filed.
- Checks: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (74 test projects); `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary` 100% line and branch, 0 failing members, worst CRAP 10 (the first run found `CheckWeights` at complexity 12 and an untested all-zero-weights branch; both fixed). No test touches the file system, paths or OS errors, so the change is platform-neutral for CI on Linux and macOS.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: afe1d32d 0fc765d5 914c30cc 8fea9d62.
- 2026-11-02: Doing -> Done. kafka:// zstd cases cover Huffman literals in one and four streams, FSE-compressed weights and treeless literals, on a reference Huffman encoder
