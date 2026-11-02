---
id: BL-4485
title: Conformance: MQTT 5.0 PUBLISH, QoS flows, SUBSCRIBE and topic rules for mqtt5:// (Mqtt5 part 2 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4484]
touches: [Curl.Conformance.Extended.UnitLibrary/Mqtt5/PublishSubscribe, Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe, Documentation/Conformance/Extended/Mqtt5-PublishSubscribe.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4485 — Conformance: MQTT 5.0 PUBLISH, QoS flows, SUBSCRIBE and topic rules for mqtt5:// (Mqtt5 part 2 of 3)

## Goal

Curl's `mqtt5://` publish and subscribe are measured against OASIS MQTT 5.0's PUBLISH, acknowledgement,
SUBSCRIBE, UNSUBSCRIBE and PING packets, the QoS flows and the topic rules through the real curl command line, every
normative statement that binds a client is in a matrix mapped to a test, and every failure is fixed or listed with
its fix task.

## Context

Specification: OASIS MQTT Version 5.0 (7 March 2019), IDs `Mqtt5:MQTT-x.y.z-n`. Scope of this part: 3.3 PUBLISH
(flags, topic alias, payload format indicator, message expiry, response topic, correlation data, user properties,
subscription identifier), 3.4-3.7 PUBACK, PUBREC, PUBREL, PUBCOMP, 3.8-3.11 SUBSCRIBE, SUBACK, UNSUBSCRIBE,
UNSUBACK, 3.12-3.13 PINGREQ and PINGRESP and keep alive (3.1.2.10), 4.3 QoS levels and protocol flows, 4.4 message
redelivery, 4.6 ordering, 4.7 topic names and filters, 4.8 subscriptions (shared subscriptions as they bind a client),
4.9 flow control (Receive Maximum). Builds on the reference broker BL-4484 wrote in
`Curl.Conformance.Extended.UnitLibrary/Mqtt5/`; add behaviour in `Mqtt5/PublishSubscribe/` through its extension point.
Keep-alive timing runs on the injected `TimeProvider`.

Library: `Curl.Extended.Protocol.Mqtt5.UnitLibrary` (`Packets/`, `Packets/Mqtt5Topics`, `Client/Mqtt5ClientFlow`;
ADR-1049).

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs,
the matrix format and the `KnownFailures.txt` rule. Measure against the specification, never against upstream curl.
One file per case group; nothing added to `Harness/`. This task touches only its own conformance files so protocols
run in parallel: a fault in its own server or cases is fixed here, but a defect in the extension library is listed
in `KnownFailures.txt` with a filed High task `Fix Mqtt5 conformance: <requirement>` (pipeline feature; touches the
library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's
`.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Mqtt5-PublishSubscribe.md` lists every `[MQTT-…]` statement of this scope
      that binds a client (unnumbered MUST/SHOULD sentences given `Mqtt5:` IDs of their own), each mapped to a test or
      to `n/a: <reason>`; the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/` run curl in process and assert wire
      bytes, stdout, stderr and exit code for at least: publish at the QoS the client uses with each acknowledgement
      reason code, a subscribe with SUBACK granting a lower QoS and refusing, received QoS 0, 1 and 2 messages
      acknowledged in order, a topic alias set and reused by the server, a wildcard topic name refused, packet
      identifiers in use not reused, PINGREQ at the keep-alive interval and a missing PINGRESP, and a message larger
      than the client's Maximum Packet Size.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Mqtt5 conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Mqtt5/PublishSubscribe/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
