---
id: BL-4563
title: Fix Mqtt5 conformance: Mqtt5:MQTT5-4.13.1-1 answer a Malformed Packet or Protocol Error with DISCONNECT 0x81 or 0x82
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4560]
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientFlow.cs, Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5PacketChannel.cs, Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientException.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Conformance.Extended.UnitTests/Mqtt5/ConnackCases.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
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

- [x] The five cases above pass, and their four lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [x] A focused unit test in `Curl.Extended.Protocol.Mqtt5.UnitTests` pins the DISCONNECT 0x81 after a Malformed Packet and the DISCONNECT 0x82 after a Protocol Error (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] `Curl.Extended.Protocol.Mqtt5.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).

## Notes

- Plan (direct, no architect: one seam): `Mqtt5ClientException` carries an optional DISCONNECT reason code; `Mqtt5PacketChannel` passes on `Mqtt5MalformedPacketException.ReasonCode`, `Mqtt5ClientFlow` gives an unexpected packet Protocol Error 0x82, and `RunAsync` sends DISCONNECT with that code (best effort) before rethrowing. Recorded in ADR-1201.
- Widened `touches` with `Curl.Conformance.Extended.UnitTests/Mqtt5/ConnackCases.cs`: two of its cases (`Connack_ReservedAcknowledgeFlagSet_IsAMalformedPacketWithExit8`, `Connack_PropertyTwice_IsRefusedWithExit8`) pinned "sent only CONNECT" for a malformed CONNACK, which this task's goal replaces with CONNECT then DISCONNECT 0x81 (reserved flags) or 0x82 (a property twice, a Protocol Error by section 3.2.2.3). No task in Doing on origin/work/dark-factory named that file.
- The KnownFailures lines carried BL-4562, not BL-4563; the four `Mqtt5:` lines were removed as the criterion says.
- Default taken: a refusal by reason code (failing CONNACK, SUBACK, DISCONNECT) and Session Present send no DISCONNECT, since they are not malformed packets or protocol errors in the stream.
- Linux/macOS: the change touches no paths, OS errors or certificates; tests are byte-level fakes and loopback cases, so it is platform-neutral.
- Verified: the five cases pass (Curl.Conformance.Extended.UnitTests 475/475, known-failures check included); `dotnet build Curl.slnx -warnaserror` clean; fast tests 53270 passed, 0 failed; Measure-CodeQuality for Curl.Extended.Protocol.Mqtt5.UnitLibrary 100% line, 100% branch, 0 failing members, worst CRAP 10. The await moved out of the catch block in `RunAsync` because the compiler's rethrow code left a branch uncovered.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: f2f790bf bab66fef 914c30cc e5dbaa1c.
- 2026-11-02: Doing -> Done. mqtt5:// answers a Malformed Packet or Protocol Error with DISCONNECT 0x81/0x82 before closing, exit 8 unchanged
