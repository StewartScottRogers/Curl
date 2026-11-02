---
id: BL-4517
title: Conformance: MQTT 5.0 reference broker, data types, fixed header and CONNECT for mqtt5:// (Mqtt5 part 1a)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/Mqtt5, Curl.Conformance.Extended.UnitTests/Mqtt5, Documentation/Conformance/Extended/Mqtt5.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4517 — Conformance: MQTT 5.0 reference broker, data types, fixed header and CONNECT for mqtt5:// (Mqtt5 part 1a)

## Goal

A reference MQTT 5 broker exists for mqtt5:// conformance, and Curl's CONNECT and its data representation are measured against MQTT 5.0 chapters 1.5, 2 and 3.1 through the real curl command line.

## Context

Specification: OASIS MQTT Version 5.0 (7 March 2019); use its normative IDs (`Mqtt5:MQTT-3.1.2-3`). Split from BL-4484 (Mqtt5 part 1 of 3, Deferred) for size; part 2 is BL-4485 (PUBLISH, QoS, SUBSCRIBE), part 3 BL-4486 (WebSocket, TLS).

Library under test: `Curl.Extended.Protocol.Mqtt5.UnitLibrary` (`Encoding/`, `Packets/`, `Client/Mqtt5ClientFlow`, `Mqtt5ProtocolHandler`; ADR-1049: follows curl's `mqtt://`: without `-d` subscribe, with `-d` publish). Malformed packets raise `Mqtt5MalformedPacketException` with a reason code.

Follow `Documentation/Conformance/Extended/README.md` and ADR-1186/ADR-1188: the harness, `[SpecRequirement]` IDs, the matrix format and the `KnownFailures.txt` rule. Measure against the specification, never upstream curl. The reference broker implements the wire format from the specification itself and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. A fault in the broker or cases is fixed here; a defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix Mqtt5 conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed). Every matrix row this task adds must be covered by a test or `n/a: <reason>` so the matrix check stays green after each part.

Scope: chapter 1.5 (integers, Variable Byte Integer, UTF-8 strings and the 1.5.4 rules, binary data, string pairs), chapter 2 (fixed header, packet identifier rules, properties table 2-4, reason codes table 2-6), 3.1 CONNECT. The broker is designed so BL-4518 (`Mqtt5/Connack/`), BL-4519 (`Mqtt5/DisconnectAuth/`), BL-4485 (`Mqtt5/PublishSubscribe/`) and BL-4486 (`Mqtt5/Transports/`) add behaviour in their own subfolders without editing this task's files (for example a broker taking an injected CONNACK and per-packet responders).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Mqtt5.md` exists, lists every `[MQTT-…]` statement of chapters 1.5, 2 and 3.1 that binds a client (and every MUST/SHOULD sentence there without a number, given a `Mqtt5:` ID of its own), each mapped to a test or to `n/a: <reason>`, links to `Mqtt5-PublishSubscribe.md` (BL-4485) and `Mqtt5-Transports.md` (BL-4486), and the harness's matrix check passes.
- [ ] A reference MQTT 5 broker in `Curl.Conformance.Extended.UnitLibrary/Mqtt5/` lets BL-4518, BL-4519, BL-4485 and BL-4486 add behaviour in their own subfolders without editing this task's files.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Mqtt5/` run curl in process and assert wire bytes, stdout, stderr and exit code for at least CONNECT fields and flags: clean start, keep alive, and credentials from `-u`.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix Mqtt5 conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [ ] The code this task adds under `Curl.Conformance.Extended.UnitLibrary/Mqtt5/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: efa93abc 88c0bc9e 1f997f3b 28fcee0c d8e3fb0e 975979e2 ef6c9026.
