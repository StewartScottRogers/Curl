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
completed:
---
# BL-4580 — Fix Mqtt5 conformance: acknowledge received QoS 1 and QoS 2 PUBLISH in order (MQTT-4.3.2-4, MQTT-4.3.3-8, MQTT-4.3.3-11, MQTT-4.6.0-2)

## Goal

A subscribing `mqtt5://` curl answers each received QoS 1 PUBLISH with PUBACK and each QoS 2 PUBLISH with PUBREC, then its PUBREL with PUBCOMP, in the order received.

## Context

Requirements Mqtt5:MQTT-4.3.2-4, MQTT-4.6.0-2, MQTT-4.3.3-8, MQTT-4.3.3-11, MQTT-4.6.0-3 (OASIS MQTT 5.0). Today curl writes the payload and sends nothing, so the broker waits and `-m` ends the run. Found by BL-4485's conformance cases (`Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/`); the expected
behaviour is decided in ADR-1199. The change lands in `Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientFlow.cs`.

## Acceptance criteria

- [ ] `ReceivedPublishCases.Received_Qos1Messages_AreEachAcknowledgedWithAPubackInOrder and ReceivedPublishCases.Received_Qos2Message_IsAcknowledgedWithPubrecThenPubcomp` passes as a plain case (its `KnownFailures.txt` lines for BL-4573 are removed).
- [ ] A focused unit test in `Curl.Extended.Protocol.Mqtt5.UnitTests` pins the fix (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and `Curl.Extended.Protocol.Mqtt5.UnitLibrary` stays at 100% line and branch coverage (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-02: Created by BL-4485.
