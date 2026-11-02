---
id: BL-4563
title: Fix Mqtt5 conformance: Mqtt5:MQTT5-4.13.1-1 answer a Malformed Packet or Protocol Error with DISCONNECT 0x81 or 0x82
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4560]
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientFlow.cs, Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5PacketChannel.cs, Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientException.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4563 — Fix Mqtt5 conformance: Mqtt5:MQTT5-4.13.1-1 answer a Malformed Packet or Protocol Error with DISCONNECT 0x81 or 0x82

## Goal

When curl's `mqtt5://` client reads a Malformed Packet or a Protocol Error from the server, it sends a DISCONNECT carrying Reason Code 0x81 (Malformed Packet) or 0x82 (Protocol Error) before it closes the connection, as OASIS MQTT Version 5.0 section 4.13.1 says a client SHOULD, and still ends the transfer with exit 8 and today's message.

## Context

Found by BL-4519's cases (`Curl.Conformance.Extended.UnitTests/Mqtt5/DisconnectAuthCases.cs`, `ErrorHandlingCases.cs`). Today `Mqtt5PacketChannel.TryTakePacket` turns `Mqtt5MalformedPacketException` (which carries the reason code, 0x81 or 0x82) into a `Mqtt5ClientException` with exit 8 and the transfer ends without sending anything; an unexpected packet (an AUTH when CONNECT set no Authentication Method, a second CONNACK) ends it the same way through `Mqtt5ClientFlow.ReceiveAsync` / `WriteSubscribedPacketAsync`. Sections 3.14.1 (MQTT-3.14.1-1: DISCONNECT with reserved flags set is answered by DISCONNECT 0x81) and 3.15 (an AUTH when CONNECT had no Authentication Method is a Protocol Error) need the same. Once CONNACK has arrived, send `Mqtt5Disconnect { ReasonCode = <code> }` (best effort: a failed send keeps the original exit code and message), then close. Before CONNACK, a DISCONNECT is also allowed (section 3.14.0 binds only the server). Keep exit codes and stderr as they are.

The known failures, each listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with this task's ID:
- `Mqtt5:MQTT-3.14.1-1` (DisconnectAuthCases.Subscribe_DisconnectWithReservedFlagSet_IsAnsweredByDisconnect0x81)
- `Mqtt5:MQTT-3.15.1-1` (DisconnectAuthCases.Subscribe_AuthWithReservedFlagSet_IsAnsweredByDisconnect0x81)
- `Mqtt5:MQTT5-3.15-1` (DisconnectAuthCases.Connect_AnsweredByAuthWithAMethodCurlDidNotOffer_IsAProtocolErrorAnsweredByDisconnect0x82)
- `Mqtt5:MQTT5-4.13.1-1` (ErrorHandlingCases.Connack_ReasonStringNotWellFormedUtf8_IsAnsweredByDisconnect0x81, ErrorHandlingCases.Subscribe_SecondConnAck_IsAProtocolErrorAnsweredByDisconnect0x82)

Depends on BL-4560, which changes `Mqtt5ClientFlow.cs` too.

## Acceptance criteria

- [ ] The five cases above pass, and their four lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [ ] A focused unit test in `Curl.Extended.Protocol.Mqtt5.UnitTests` pins the DISCONNECT 0x81 after a Malformed Packet and the DISCONNECT 0x82 after a Protocol Error (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `Curl.Extended.Protocol.Mqtt5.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: f2f790bf bab66fef 914c30cc e5dbaa1c.
