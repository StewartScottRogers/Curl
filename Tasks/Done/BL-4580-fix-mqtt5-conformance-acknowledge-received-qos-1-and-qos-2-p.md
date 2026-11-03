---
id: BL-4580
title: Fix Mqtt5 conformance: acknowledge received QoS 1 and QoS 2 PUBLISH in order (MQTT-4.3.2-4, MQTT-4.3.3-8, MQTT-4.3.3-11, MQTT-4.6.0-2)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientFlow.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4580 — Fix Mqtt5 conformance: acknowledge received QoS 1 and QoS 2 PUBLISH in order (MQTT-4.3.2-4, MQTT-4.3.3-8, MQTT-4.3.3-11, MQTT-4.6.0-2)

## Goal

A subscribing `mqtt5://` curl answers each received QoS 1 PUBLISH with PUBACK and each QoS 2 PUBLISH with PUBREC, then its PUBREL with PUBCOMP, in the order received.

## Context

Requirements Mqtt5:MQTT-4.3.2-4, MQTT-4.6.0-2, MQTT-4.3.3-8, MQTT-4.3.3-11, MQTT-4.6.0-3 (OASIS MQTT 5.0). Today curl writes the payload and sends nothing, so the broker waits and `-m` ends the run. Found by BL-4485's conformance cases (`Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/`); the expected
behaviour is decided in ADR-1199. The change lands in `Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientFlow.cs`.

## Acceptance criteria

- [x] `ReceivedPublishCases.Received_Qos1Messages_AreEachAcknowledgedWithAPubackInOrder and ReceivedPublishCases.Received_Qos2Message_IsAcknowledgedWithPubrecThenPubcomp` passes as a plain case (its `KnownFailures.txt` lines for BL-4573 are removed).
- [x] A focused unit test in `Curl.Extended.Protocol.Mqtt5.UnitTests` pins the fix (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and `Curl.Extended.Protocol.Mqtt5.UnitLibrary` stays at 100% line and branch coverage (`Measure-CodeQuality.ps1`).

## Notes

- Delivered directly rather than through the full `/feature` stages: ADR-1199 already decides the behaviour and the change is one method in `Mqtt5ClientFlow`, so a separate plan would add nothing.
- `Mqtt5ClientFlow.WriteSubscribedPacketAsync` writes a PUBLISH and then answers it by QoS (`AcknowledgeAsync`: PUBACK for QoS 1, PUBREC for QoS 2, nothing for QoS 0, all in the short form). A new method, `AnswerReleaseOrAcceptPingResponseAsync`, answers a PUBREL with PUBCOMP, accepts a PINGRESP and treats anything else as a Protocol Error. The split keeps cyclomatic complexity at the 10 gate: the first quality run measured 12.
- Default taken: the client keeps no QoS 2 state. A duplicate QoS 2 PUBLISH is written again, and a PUBREL is answered with PUBCOMP Success whatever its Packet Identifier. ADR-1199 asks only for answers in order, and curl subscribes at QoS 0, so per-identifier tracking would add state with no case that needs it.
- Unit tests: `RunAsync_ReceivedQos1Publishes_AreEachAcknowledgedWithAPubackInOrder`, `RunAsync_ReceivedQos2Publish_IsAcknowledgedWithPubrecAndItsPubrelWithPubcomp` and `RunAsync_PubackWhileSubscribed_EndsWithWeirdServerReply`. Removed the five BL-4573 lines from `KnownFailures.txt`. Curl.Conformance.Extended.UnitTests: 532 passed, 0 skipped.
- Measured: Mqtt5 at 100% line and 100% branch coverage, 0 failing members, worst CRAP 10. `dotnet build Curl.slnx -warnaserror` reports 0 warnings and 0 errors, and the fast tests pass in all 115 test assemblies. The tests are byte-level fakes with no paths or OS text, so the result does not depend on the platform.

## Log

- 2026-11-02: Created by BL-4485.
- 2026-11-02: Backlog -> Doing. Criteria at claim: e66bc520 7b580d6c 73c2678e.
- 2026-11-02: Doing -> Done. A subscribing mqtt5:// curl acknowledges QoS 1 PUBLISH with PUBACK and QoS 2 with PUBREC then PUBCOMP, in order
