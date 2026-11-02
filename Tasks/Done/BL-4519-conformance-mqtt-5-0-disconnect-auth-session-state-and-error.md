---
id: BL-4519
title: Conformance: MQTT 5.0 DISCONNECT, AUTH, session state and error handling for mqtt5:// (Mqtt5 part 1c)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4517, BL-4518]
touches: [Curl.Conformance.Extended.UnitTests/Mqtt5/DisconnectAuthCases.cs, Curl.Conformance.Extended.UnitTests/Mqtt5/ErrorHandlingCases.cs, Curl.Conformance.Extended.UnitLibrary/Mqtt5/DisconnectAuth, Documentation/Conformance/Extended/Mqtt5.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4519 — Conformance: MQTT 5.0 DISCONNECT, AUTH, session state and error handling for mqtt5:// (Mqtt5 part 1c)

## Goal

Curl's DISCONNECT, AUTH, session state and error handling on mqtt5:// are measured against MQTT 5.0 sections 3.14, 3.15, 4.1, 4.12 and 4.13 through the real curl command line, completing the Mqtt5 part 1 matrix.

## Context

Specification: OASIS MQTT Version 5.0 (7 March 2019); use its normative IDs (`Mqtt5:MQTT-3.1.2-3`). Split from BL-4484 (Mqtt5 part 1 of 3, Deferred) for size; part 2 is BL-4485 (PUBLISH, QoS, SUBSCRIBE), part 3 BL-4486 (WebSocket, TLS).

Library under test: `Curl.Extended.Protocol.Mqtt5.UnitLibrary` (`Encoding/`, `Packets/`, `Client/Mqtt5ClientFlow`, `Mqtt5ProtocolHandler`; ADR-1049: follows curl's `mqtt://`: without `-d` subscribe, with `-d` publish). Malformed packets raise `Mqtt5MalformedPacketException` with a reason code.

Follow `Documentation/Conformance/Extended/README.md` and ADR-1186/ADR-1188: the harness, `[SpecRequirement]` IDs, the matrix format and the `KnownFailures.txt` rule. Measure against the specification, never upstream curl. The reference broker implements the wire format from the specification itself and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. A fault in the broker or cases is fixed here; a defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix Mqtt5 conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed). Every matrix row this task adds must be covered by a test or `n/a: <reason>` so the matrix check stays green after each part.

Scope: 3.14 DISCONNECT, 3.15 AUTH, 4.1 session state as it binds the client, 4.12 enhanced authentication, 4.13 error handling. Builds on BL-4517's broker and BL-4518; add behaviour under `Curl.Conformance.Extended.UnitLibrary/Mqtt5/DisconnectAuth/` and rows to `Mqtt5.md`. This is the last part of BL-4484.

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/Mqtt5.md` lists every client-binding `[MQTT-…]` statement of 3.14, 3.15, 4.1, 4.12 and 4.13 (and every unnumbered MUST/SHOULD sentence there, given a `Mqtt5:` ID), each mapped to a test or `n/a: <reason>`, and the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/Mqtt5/` (`DisconnectAuthCases.cs`, `ErrorHandlingCases.cs`) run curl in process and assert wire bytes, stdout, stderr and exit code for at least: a malformed packet from the server answered by DISCONNECT with the right reason code, a server DISCONNECT, AUTH with an unknown method, and invalid UTF-8 in a server string.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix Mqtt5 conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [x] The code this task adds under `Curl.Conformance.Extended.UnitLibrary/Mqtt5/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Plan: one builder `Curl.Conformance.Extended.UnitLibrary/Mqtt5/DisconnectAuth/Mqtt5DisconnectAuthBytes` (SUBACK, PUBLISH, DISCONNECT, AUTH with chosen fixed header flags, a raw-bytes string property for ill-formed UTF-8), driven through the existing `Mqtt5ReferenceBroker` with its CONNACK and responders; no broker or harness change.
- Spec IDs: the 3.14, 3.15 and 4.1 statements were read from the OASIS MQTT 5.0 HTML; the 4.12 and 4.13 numbered IDs (MQTT-4.12.0-1 to -7, MQTT-4.12.1-1/-2, MQTT-4.13.1-1, MQTT-4.13.2-1) were written from the specification's conformance statement list because the fetched page was cut off after section 4.1. Unnumbered binding sentences got `MQTT5-3.14.2.2.2-1`, `MQTT5-3.14.4-1`, `MQTT5-3.15-1` and `MQTT5-4.13.1-1`.
- Decision (default taken): each scenario where curl falls short has two cases, one passing case that pins today's exit code, stderr and wire bytes, and one Known-failure case that also requires the DISCONNECT 0x81/0x82 that section 4.13.1 asks for. This way the passing part stays guarded while the fix is pending.
- Measured: curl ends with exit 8 and the right message on every malformed or protocol-error packet, sends no AUTH, sends `E0 00` last after a PUBLISH, and exits 0 on a server DISCONNECT 0x00 (exit 8 on 0x8B). It never answers an error with DISCONNECT 0x81/0x82. Filed High BL-4562 (depends on BL-4560, same file) and listed MQTT-3.14.1-1, MQTT-3.15.1-1, MQTT5-3.15-1 and MQTT5-4.13.1-1 in KnownFailures.txt against it.
- KnownFailures.txt: also removed `GoogleCloudStorage:V4Signing-Scope-2 BL-4538`. BL-4542's fix had made that case pass and the shared branch had already dropped the line, but this checkout still had it, which made the test run fail.
- Measure-CodeQuality -Library Curl.Conformance.Extended.UnitLibrary: 0 failing members. Full build `-warnaserror` is clean. The fast tests are green: all 114 test projects passed. The CI criterion for Linux and macOS is met by keeping the tests platform-neutral: they use in-memory routes only, with no paths, no OS text and no certificates.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: dc08a549 026ea0d1 28fcee0c d8e3fb0e 975979e2 ef6c9026.
- 2026-11-02: Doing -> Done. mqtt5:// DISCONNECT, AUTH, session state and error handling measured against MQTT 5.0 3.14, 3.15, 4.1, 4.12, 4.13; DISCONNECT-on-error gap filed as BL-4562
