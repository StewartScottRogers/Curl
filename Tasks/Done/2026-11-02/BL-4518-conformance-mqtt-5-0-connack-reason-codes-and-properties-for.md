---
id: BL-4518
title: Conformance: MQTT 5.0 CONNACK reason codes and properties for mqtt5:// (Mqtt5 part 1b)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4517]
touches: [Curl.Conformance.Extended.UnitTests/Mqtt5/ConnackCases.cs, Curl.Conformance.Extended.UnitLibrary/Mqtt5/Connack, Documentation/Conformance/Extended/Mqtt5.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Conformance.Extended.UnitLibrary/Harness/ReferenceRun.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4518 — Conformance: MQTT 5.0 CONNACK reason codes and properties for mqtt5:// (Mqtt5 part 1b)

## Goal

Curl's handling of CONNACK on mqtt5:// is measured against MQTT 5.0 section 3.2 through the real curl command line.

## Context

Specification: OASIS MQTT Version 5.0 (7 March 2019); use its normative IDs (`Mqtt5:MQTT-3.1.2-3`). Split from BL-4484 (Mqtt5 part 1 of 3, Deferred) for size; part 2 is BL-4485 (PUBLISH, QoS, SUBSCRIBE), part 3 BL-4486 (WebSocket, TLS).

Library under test: `Curl.Extended.Protocol.Mqtt5.UnitLibrary` (`Encoding/`, `Packets/`, `Client/Mqtt5ClientFlow`, `Mqtt5ProtocolHandler`; ADR-1049: follows curl's `mqtt://`: without `-d` subscribe, with `-d` publish). Malformed packets raise `Mqtt5MalformedPacketException` with a reason code.

Follow `Documentation/Conformance/Extended/README.md` and ADR-1186/ADR-1188: the harness, `[SpecRequirement]` IDs, the matrix format and the `KnownFailures.txt` rule. Measure against the specification, never upstream curl. The reference broker implements the wire format from the specification itself and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. A fault in the broker or cases is fixed here; a defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix Mqtt5 conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed). Every matrix row this task adds must be covered by a test or `n/a: <reason>` so the matrix check stays green after each part.

Scope: 3.2 CONNACK. Builds on BL-4517's reference broker; add behaviour under `Curl.Conformance.Extended.UnitLibrary/Mqtt5/Connack/` without editing BL-4517's files, and add rows to `Mqtt5.md`.

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/Mqtt5.md` lists every `[MQTT-3.2…]` statement that binds a client (and every unnumbered MUST/SHOULD sentence of 3.2, given a `Mqtt5:` ID), each mapped to a test or `n/a: <reason>`, and the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/Mqtt5/ConnackCases.cs` run curl in process and assert wire bytes, stdout, stderr and exit code for at least CONNACK with each failure reason code, and the CONNACK properties the client must honour (Maximum Packet Size, Receive Maximum, Topic Alias Maximum, Server Keep Alive, Assigned Client Identifier).
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix Mqtt5 conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [x] The code this task adds under `Curl.Conformance.Extended.UnitLibrary/Mqtt5/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 8d35620f 7123695d 28fcee0c d8e3fb0e 975979e2 ef6c9026.
- 2026-11-02: Doing -> Done. mqtt5:// CONNACK is measured against MQTT 5.0 section 3.2: reason codes and properties, two known failures filed
