---
id: BL-4484
title: Conformance: MQTT 5.0 data types, CONNECT, CONNACK, DISCONNECT and AUTH for mqtt5:// (Mqtt5 part 1 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/Mqtt5, Curl.Conformance.Extended.UnitTests/Mqtt5, Documentation/Conformance/Extended/Mqtt5.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4484 — Conformance: MQTT 5.0 data types, CONNECT, CONNACK, DISCONNECT and AUTH for mqtt5:// (Mqtt5 part 1 of 3)

## Goal

Curl's `mqtt5://` client is measured against OASIS MQTT 5.0's data representation, packet format, properties and
connection packets through the real curl command line, every normative statement of those parts that binds a
client is in a matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Specification: OASIS MQTT Version 5.0 (7 March 2019). It numbers its normative statements (`[MQTT-x.y.z-n]`); use
them as the IDs (`Mqtt5:MQTT-3.1.2-3`). Scope of this part: chapter 1.5 (data representation: integers, Variable Byte
Integer, UTF-8 strings and their 1.5.4 rules, binary data, string pairs), chapter 2 (fixed header, packet identifier
rules, properties table 2-4, reason codes table 2-6), 3.1 CONNECT, 3.2 CONNACK, 3.14 DISCONNECT, 3.15 AUTH, 4.1
session state as it binds the client, 4.12 enhanced authentication, 4.13 error handling. Part 2 (BL-4485) covers
PUBLISH, the QoS flows, SUBSCRIBE and topics; part 3 (BL-4486) covers WebSocket (chapter 6) and TLS.

Library: `Curl.Extended.Protocol.Mqtt5.UnitLibrary` (`CLAUDE.md`: `Encoding/`, `Packets/`, `Client/Mqtt5ClientFlow`,
`Mqtt5ProtocolHandler`; ADR-1049 says it follows curl's `mqtt://` behaviour: without `-d` subscribe, with `-d`
publish). Malformed packets raise `Mqtt5MalformedPacketException` with a reason code.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs,
the matrix format and the `KnownFailures.txt` rule. Measure against the specification, never against upstream curl.
The reference server implements the wire format from the specification itself and references no
`Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. This task touches only its
own conformance files so protocols run in parallel: a fault in its own server or cases is fixed here, but a defect
in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix Mqtt5 conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Mqtt5.md` lists every `[MQTT-…]` statement of this scope that binds a client
      (and every MUST/SHOULD sentence there without a number, given a `Mqtt5:` ID of its own), each mapped to a test
      or to `n/a: <reason>`, links to `Mqtt5-PublishSubscribe.md` (BL-4485) and `Mqtt5-Transports.md` (BL-4486), and
      the harness's matrix check passes.
- [ ] A reference MQTT 5 broker in `Curl.Conformance.Extended.UnitLibrary/Mqtt5/` lets BL-4485 and BL-4486 add
      behaviour in `Mqtt5/PublishSubscribe/` and `Mqtt5/Transports/` without editing this task's files.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Mqtt5/` run curl in process and assert wire bytes, stdout,
      stderr and exit code for at least: CONNECT fields and flags (clean start, keep alive, credentials from `-u`),
      CONNACK with each failure reason code, CONNACK properties the client must honour (Maximum Packet Size, Receive
      Maximum, Topic Alias Maximum, Server Keep Alive, Assigned Client Identifier), a malformed packet from the
      server answered by DISCONNECT with the right reason code, a server DISCONNECT, AUTH with an unknown method,
      and invalid UTF-8 in a server string.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Mqtt5 conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Mqtt5/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
