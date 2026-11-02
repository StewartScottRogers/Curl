---
id: BL-4575
title: Fix Mqtt5 conformance: close the connection when no PINGRESP answers a PINGREQ (MQTT5-3.1.2.10-1)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientFlow.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4575 — Fix Mqtt5 conformance: close the connection when no PINGRESP answers a PINGREQ (MQTT5-3.1.2.10-1)

## Goal

A subscribing `mqtt5://` curl whose PINGREQ is unanswered when the next one falls due sends DISCONNECT 0x8D Keep Alive timeout and ends with exit 8.

## Context

Requirement Mqtt5:MQTT5-3.1.2.10-1 (OASIS MQTT 5.0 section 3.1.2.10, a SHOULD). Today curl keeps pinging until `-m` ends the run with exit 28. Found by BL-4485's conformance cases (`Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/`); the expected
behaviour is decided in ADR-1197. The change lands in `Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientFlow.cs`.

## Acceptance criteria

- [ ] `KeepAliveCases.Subscribe_NoPingrespWithinTheInterval_ClosesTheConnectionWithDisconnect0x8D` passes as a plain case (its `KnownFailures.txt` lines for BL-4575 are removed).
- [ ] A focused unit test in `Curl.Extended.Protocol.Mqtt5.UnitTests` pins the fix (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and `Curl.Extended.Protocol.Mqtt5.UnitLibrary` stays at 100% line and branch coverage (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-02: Created by BL-4485.
