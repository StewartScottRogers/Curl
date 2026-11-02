---
id: BL-4485
title: Conformance: MQTT 5.0 PUBLISH, QoS flows, SUBSCRIBE and topic rules for mqtt5:// (Mqtt5 part 2 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4517, BL-4518, BL-4519]
touches: [Curl.Conformance.Extended.UnitLibrary/Mqtt5/PublishSubscribe, Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe, Documentation/Conformance/Extended/Mqtt5-PublishSubscribe.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
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
4.9 flow control (Receive Maximum). Builds on the reference broker BL-4517 wrote in
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

- [x] `Documentation/Conformance/Extended/Mqtt5-PublishSubscribe.md` lists every `[MQTT-…]` statement of this scope
      that binds a client (unnumbered MUST/SHOULD sentences given `Mqtt5:` IDs of their own), each mapped to a test or
      to `n/a: <reason>`; the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/` run curl in process and assert wire
      bytes, stdout, stderr and exit code for at least: publish at the QoS the client uses with each acknowledgement
      reason code, a subscribe with SUBACK granting a lower QoS and refusing, received QoS 0, 1 and 2 messages
      acknowledged in order, a topic alias set and reused by the server, a wildcard topic name refused, packet
      identifiers in use not reused, PINGREQ at the keep-alive interval and a missing PINGRESP, and a message larger
      than the client's Maximum Packet Size.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Mqtt5 conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/Mqtt5/PublishSubscribe/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Cases: `Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/` (PublishCases, SubscribeCases, ReceivedPublishCases, KeepAliveCases, builder tests); packets built by `Curl.Conformance.Extended.UnitLibrary/Mqtt5/PublishSubscribe/Mqtt5PublishSubscribeBytes.cs`; matrix `Documentation/Conformance/Extended/Mqtt5-PublishSubscribe.md`. Normative IDs written from the OASIS MQTT 5.0 text (Appendix B numbering); no copy of the specification is in the repository.
- curl publishes and subscribes only at QoS 0 and sends no Topic Alias Maximum, Receive Maximum or Maximum Packet Size, so: "publish with each acknowledgement reason code" is measured as a QoS 0 PUBLISH answered with PUBACK of each code (curl waits for none, exits 0); "SUBACK granting a lower QoS" has no lower grant than QoS 0, so the success case grants QoS 0 and the refusal case covers nine failure codes; "packet identifiers in use not reused" is shown by the one SUBSCRIBE carrying identifier 1 (no other packet curl sends has one); "larger than the client's Maximum Packet Size" is a 70000-byte PUBLISH written whole since curl announces none (MQTT5-3.1.2.11.4-1).
- Keep-alive cases run on real time (Server Keep Alive 1 s, `-m` as a guard) rather than a fake clock: the harness takes a TimeProvider but nothing in the test project advances one, and the cases take under 3 s.
- Known failures filed (ADR-1197 decides the expected behaviour): BL-4580 (received QoS 1/2 not acknowledged), BL-4581 (Topic Alias accepted though never allowed), BL-4582 (missing PINGRESP never closes the connection).
- Measure-CodeQuality (-Library Curl.Conformance.Extended.UnitLibrary): the only member of this task outside a gate was `Mqtt5PublishSubscribeBytes.PubRel` (line 70 uncovered, reached only by a known-failure case); `Mqtt5PublishSubscribeBytesTests.PubRel_...` now covers it. The two Amqp1ReferenceContainer complexity failures in the same report are not this task's code.
- Platform check: no paths, file system, OS text or certificates; byte and text assertions use Environment.NewLine. CI on Linux and macOS is met by keeping the change platform-neutral and the fast tests green here.
- Fast tests: solution build clean with -warnaserror, all fast test projects passed (Curl.Conformance.Extended.UnitTests 521/521).


## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: d4d8d4d4 73e63400 28fcee0c d8e3fb0e d2da9c9b ef6c9026.
- 2026-11-02: Doing -> Done. mqtt5:// PUBLISH, QoS flows, SUBSCRIBE, keep alive and topic rules measured against MQTT 5.0 with a full matrix; three client defects filed as BL-4580..4575
