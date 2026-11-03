---
id: BL-4581
title: Fix Mqtt5 conformance: refuse a Topic Alias above the client's Topic Alias Maximum (MQTT5-3.3.2.3.4-1)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientFlow.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4581 — Fix Mqtt5 conformance: refuse a Topic Alias above the client's Topic Alias Maximum (MQTT5-3.3.2.3.4-1)

## Goal

A subscribing `mqtt5://` curl refuses a PUBLISH carrying a Topic Alias (it sends no Topic Alias Maximum) with DISCONNECT 0x94 and exit 8, writing nothing of it.

## Context

Requirement Mqtt5:MQTT5-3.3.2.3.4-1 (OASIS MQTT 5.0 section 3.3.2.3.4). Today curl writes the payload of both the alias-setting and the alias-reusing PUBLISH. Found by BL-4485's conformance cases (`Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/`); the expected
behaviour is decided in ADR-1199. The change lands in `Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientFlow.cs`.

## Acceptance criteria

- [ ] `ReceivedPublishCases.Received_TopicAliasSetAndReused_IsRefusedWithDisconnect0x94` passes as a plain case (its `KnownFailures.txt` lines for BL-4574 are removed).
- [ ] A focused unit test in `Curl.Extended.Protocol.Mqtt5.UnitTests` pins the fix (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and `Curl.Extended.Protocol.Mqtt5.UnitLibrary` stays at 100% line and branch coverage (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-02: Created by BL-4485.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 0fa5ee09 7b580d6c 73c2678e.
