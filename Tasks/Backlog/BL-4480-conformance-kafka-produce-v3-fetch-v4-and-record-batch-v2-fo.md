---
id: BL-4480
title: Conformance: Kafka Produce v3, Fetch v4 and record batch v2 for kafka:// (Kafka part 2 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4501, BL-4502, BL-4503, BL-4479]
touches: [Curl.Conformance.Extended.UnitLibrary/Kafka/Records, Curl.Conformance.Extended.UnitTests/Kafka/Records, Documentation/Conformance/Extended/Kafka-Records.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4480 — Conformance: Kafka Produce v3, Fetch v4 and record batch v2 for kafka:// (Kafka part 2 of 3)

## Goal

Curl's `kafka://` produce and fetch are measured against the Kafka protocol guide's Produce v3, Fetch v4 and record
batch (magic 2) formats through the real curl command line, every client requirement is in a matrix mapped to a
test, and every failure is fixed or listed with its fix task.

## Context

Specification: the Apache Kafka protocol guide (kafka.apache.org/protocol) and its "Record Batch" and "Record"
definitions (kafka.apache.org/documentation/#recordbatch): Produce v3 (acks, timeout, partition response error
codes, base offset), Fetch v4 (max wait, min and max bytes, isolation level, high watermark, last stable offset,
aborted transactions, partial batches at the end of a response), record batch v2 (base offset, length, partition
leader epoch, magic, CRC-32C over the attributes onward, attributes, last offset delta, timestamps, producer fields,
records count; records with varint lengths, deltas, key, value, headers). Uncompressed batches here; gzip, Snappy,
LZ4 and zstd are BL-4481. Builds on the reference broker BL-4479 wrote in `Curl.Conformance.Extended.UnitLibrary/Kafka/`;
add log storage and the two APIs in `Kafka/Records/` through its extension point.

Library: `Curl.Extended.Protocol.Kafka.UnitLibrary` (`Wire/KafkaRecordBatch`, `KafkaRecord`, `KafkaRecordSet`,
`Crc32C`, `KafkaRequests`, `KafkaResponses`, `Client/KafkaClientFlow`).

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Kafka:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the specification,
never against upstream curl. The reference broker writes and checks batches from the specification itself
(its own CRC-32C) and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to
`Harness/`. This task touches only its own conformance files so protocols run in parallel: a fault in its own server
or cases is fixed here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix Kafka conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Kafka-Records.md` gives every requirement of this scope that binds a client
      a `Kafka:` ID mapped to a test or to `n/a: <reason>`; the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Kafka/Records/` run curl in process and assert wire bytes,
      stdout, stderr and exit code for at least: a produce whose batch the broker verifies field by field and by
      CRC-32C, a produce error code, a fetch of several batches to the high watermark, a fetch starting inside a
      batch (records before the offset skipped), a partial batch at the end of a response, a bad CRC, an empty
      partition, records with null keys, headers and negative timestamp deltas, and an offset out of range.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Kafka conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Kafka/Records/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
