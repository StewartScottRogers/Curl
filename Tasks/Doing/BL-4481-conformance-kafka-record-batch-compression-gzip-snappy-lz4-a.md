---
id: BL-4481
title: Conformance: Kafka record batch compression - gzip, Snappy, LZ4 and zstd - for kafka:// (Kafka part 3 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4479]
touches: [Curl.Conformance.Extended.UnitLibrary/Kafka/Compression, Curl.Conformance.Extended.UnitTests/Kafka/Compression, Documentation/Conformance/Extended/Kafka-Compression.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4481 — Conformance: Kafka record batch compression - gzip, Snappy, LZ4 and zstd - for kafka:// (Kafka part 3 of 3)

## Goal

Curl's `kafka://` fetch of compressed record batches is measured against the compression formats Kafka uses through
the real curl command line, every decoder requirement is in a matrix mapped to a test, and every failure is fixed or
listed with its fix task.

## Context

Specifications: the record batch attributes' compression codec bits (kafka.apache.org/documentation/#recordbatch),
gzip (RFC 1952, via the BCL), Snappy as Kafka frames it (the xerial `snappy-java` stream header and blocks, and raw
Snappy per google/snappy `format_description.txt`), LZ4 (lz4/lz4 `doc/lz4_Frame_format.md` and
`lz4_Block_format.md`), and zstd (RFC 8878: frames, blocks, literals, sequences, FSE and Huffman tables, checksum).
The library decompresses all four on fetch (`Wire/Compression/`, gzip in `KafkaRecordBatch`); its produce
path sends uncompressed batches (check `Client/KafkaClientFlow`), so a compressed produce is `n/a` unless it does. The reference broker
must serve batches compressed by an encoder the test owns: gzip through the BCL; Snappy, LZ4 and zstd from fixed
vectors (the formats' published test files where they exist, or byte strings written out by hand from the format
documents, documented in the case file) since the BCL has no encoder for them. Builds on the reference broker BL-4479
wrote in `Curl.Conformance.Extended.UnitLibrary/Kafka/`; work in `Kafka/Compression/`. Does not need BL-4480: if a
fetch case needs record storage beyond what BL-4479 left, keep it in `Kafka/Compression/`.

Library: `Curl.Extended.Protocol.Kafka.UnitLibrary` (`Wire/Compression/Lz4FrameDecompressor`,
`SnappyDecompressor`, `Zstd/`, and gzip in `KafkaRecordBatch`).

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Kafka:RFC8878-<section>-<n>`, `Kafka:LZ4-<section>-<n>` and the like), the matrix format and the
`KnownFailures.txt` rule. One file per case group; nothing added to `Harness/`. This task touches only its own
conformance files so protocols run in parallel: a fault in its own server or cases is fixed here, but a defect in
the extension library is listed in `KnownFailures.txt` with a filed High task `Fix Kafka conformance: <requirement>`
(pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a
focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Kafka-Compression.md` gives every requirement of the four formats that binds
      a decoder a `Kafka:` ID mapped to a test or to `n/a: <reason>`; the harness's
      matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Kafka/Compression/` run curl in process and assert stdout, stderr
      and exit code for at least: a fetch of each codec, zstd with raw, RLE
      and compressed blocks, repeat offsets, a content checksum and a dictionary id the client refuses, LZ4 with block
      and content checksums and dependent blocks, Snappy with copies of each offset width, and a corrupt stream of
      each codec.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Kafka conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: ab06262f 5b192b2e 6873c13e d8e3fb0e 700f62ad ef6c9026.
