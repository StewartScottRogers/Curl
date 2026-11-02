---
id: BL-4479
title: Conformance: Kafka wire protocol framing, Metadata and SASL PLAIN for kafka:// (Kafka part 1 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/Kafka, Curl.Conformance.Extended.UnitTests/Kafka, Documentation/Conformance/Extended/Kafka.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4479 — Conformance: Kafka wire protocol framing, Metadata and SASL PLAIN for kafka:// (Kafka part 1 of 3)

## Goal

Curl's `kafka://` and `kafkas://` client is measured against the Kafka protocol guide's framing, primitive types,
Metadata and SASL exchange through the real curl command line, every client requirement is in a matrix mapped to a
test, and every failure is fixed or listed with its fix task.

## Context

Specification: the Apache Kafka protocol guide (kafka.apache.org/protocol) with the request schemas of the versions
the library sends: the size-prefixed request and response framing, request header v1 (api key, api version,
correlation id, client id), the primitive types (INT8 to INT64, VARINT, VARLONG, STRING, NULLABLE_STRING, BYTES,
ARRAY), Metadata v4 (brokers, topic and partition leaders, error codes, a leader on another broker), SaslHandshake v1
and SaslAuthenticate v0 with the RFC 4616 PLAIN message, and the error codes table. The guide has few RFC-style
keywords: treat each "must", "should" and each schema field the client writes or relies on as a requirement. Part 2
(BL-4480) covers Produce, Fetch and record batches; part 3 (BL-4481) covers compression.

Library: `Curl.Extended.Protocol.Kafka.UnitLibrary` (`CLAUDE.md`: `Wire/KafkaWriter`, `KafkaReader`,
`KafkaRequests`, `KafkaMetadataResponse`, `KafkaResponseReader`, `Wire/KafkaSasl/`, `Client/`; ADR-1076, ADR-1080,
ADR-1082). Ports 9092 and 9093 (TLS). Example: `curl kafka://host/topic/0?offset=0`, `-d` to produce, `-u` for SASL.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Kafka:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the specification,
never against upstream curl. The reference broker implements the wire format from the specification itself and
references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. This task
touches only its own conformance files so protocols run in parallel: a fault in its own server or cases is fixed
here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix Kafka conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/Kafka.md` gives every requirement of this scope that binds a client a
      `Kafka:` ID mapped to a test or to `n/a: <reason>`, links to `Kafka-Records.md` (BL-4480) and
      `Kafka-Compression.md` (BL-4481), and the harness's matrix check passes.
- [x] A reference broker in `Curl.Conformance.Extended.UnitLibrary/Kafka/` (one or several broker ids) lets BL-4480
      and BL-4481 add behaviour in `Kafka/Records/` and `Kafka/Compression/` without editing this task's files.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/Kafka/` run curl in process and assert wire bytes, stdout, stderr
      and exit code for at least: request header fields and correlation ids, a response with a wrong correlation id,
      a truncated response, Metadata with the leader on this broker and on another one (reconnect), a topic or
      partition error code, SASL handshake with PLAIN offered and not offered, a failed SaslAuthenticate (exit 67),
      and `kafkas://` over the TLS route.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Kafka conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/Kafka/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Plan, done directly (no subagents, to keep the run inside its cost): a reference broker in
  `Curl.Conformance.Extended.UnitLibrary/Kafka/` with its own codec (`KafkaWireWriter`, `KafkaWireReader`, sharing
  nothing with the client), `KafkaReferenceBroker` (size-prefixed requests, request header v1 parsed into
  `KafkaReferenceRequest`, every request recorded in `Requests`), and one `IKafkaReferenceApi` per API:
  `KafkaMetadataApi` (v4; a topic the cluster lacks answers UNKNOWN_TOPIC_OR_PARTITION), `KafkaSaslHandshakeApi`
  (v1; UNSUPPORTED_SASL_MECHANISM when PLAIN is not enabled), `KafkaSaslPlainAuthenticateApi` (v0; parses the RFC 4616
  message itself, refuses with SASL_AUTHENTICATION_FAILED). BL-4480 and BL-4481 add Produce, Fetch and compression as
  new `IKafkaReferenceApi` types in `Kafka/Records/` and `Kafka/Compression/` and pass them to the broker's
  constructor, without editing these files.
- Decision (sensible default): the broker closes the connection on an api_key it does not serve, as a Kafka broker
  does. In this part it serves no Fetch, so the Metadata-leader and SASL-success cases assert the Fetch request the
  client sends next (api key, correlation id, which broker) and then exit 56 "Connection disconnected".
- Wrong correlation id and truncated response use `ScriptedReferenceServer` with bytes built by `KafkaWireWriter`.
- curl ends its stderr line with `Environment.NewLine` (measured: CRLF on Windows), so the stderr assertions append
  `Environment.NewLine`; every test is platform-neutral (no paths, `localhost` only). CI on Linux and macOS is met by
  that and the fast tests green here.
- Results: 13 Kafka cases (12 curl runs + broker edges) pass; no failing case, so `KnownFailures.txt` is unchanged and
  the known-failures and matrix checks pass. `dotnet build Curl.slnx -warnaserror` clean, fast tests green (115 test
  assemblies passed, none failed).
- `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary` reported one member, the unused
  `KafkaWireReader.Int8()` (uncovered); it was removed, leaving every Kafka member at 100% line and branch, complexity
  at most 10, CRAP at most 30.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 6da75e7a 204c1fd3 f77f21ba 6873c13e d8e3fb0e d1f51b8e ef6c9026.
- 2026-11-02: Doing -> Done. kafka:// framing, Metadata leader and reconnect, error codes, SASL PLAIN and kafkas:// are measured against a reference broker, mapped in Kafka.md
