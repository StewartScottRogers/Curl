---
id: BL-4561
title: Fix Mqtt5 conformance: Mqtt5:MQTT-3.2.2-15 (honour CONNACK Maximum Packet Size)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientFlow.cs, Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientMessages.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4561 — Fix Mqtt5 conformance: Mqtt5:MQTT-3.2.2-15 (honour CONNACK Maximum Packet Size)

## Goal

A mqtt5:// PUBLISH larger than the CONNACK's Maximum Packet Size is never sent: curl ends with exit 55 and "MQTT 5 PUBLISH of <n> bytes exceeds the server's Maximum Packet Size of <m>".

## Context

OASIS MQTT 5.0 MQTT-3.2.2-15: the client MUST NOT send packets exceeding the Maximum Packet Size the CONNACK returns (section 3.2.2.3.6). `Mqtt5ClientFlow.RunAsync` sends the PUBLISH without checking it; compare the framed PUBLISH length (`ToBytes().Length`) with the property. Exit code and text decided in ADR-1193 (BL-4518). Found by BL-4518's `ConnackCases`.

## Acceptance criteria

- [x] `ConnackCases.Connack_MaximumPacketSizeBelowThePublish_SendsNoPublishAndExits55` passes in `Curl.Conformance.Extended.UnitTests`.
- [x] A focused unit test in `Curl.Extended.Protocol.Mqtt5.UnitTests` pins the fix (ADR-0482).
- [x] The line for the requirement is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` and the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and `Curl.Extended.Protocol.Mqtt5.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

- The check covers the SUBSCRIBE too (MQTT-3.2.2-15 binds every packet), with the same exit 55 and text naming SUBSCRIBE; the CONNECT precedes the CONNACK and the DISCONNECT is 2 bytes, below any valid maximum. Exit code and text are ADR-1193's.
- Measure-CodeQuality.ps1 for Curl.Extended.Protocol.Mqtt5.UnitLibrary: 0 failing members. Full build clean and fast tests green on Windows; the change is platform-neutral, so CI on Linux and macOS is covered by that.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 154cba23 7b580d6c 5fe60615 2391cd16.
- 2026-11-02: Doing -> Done. PUBLISH and SUBSCRIBE over the CONNACK Maximum Packet Size end with exit 55 unsent
