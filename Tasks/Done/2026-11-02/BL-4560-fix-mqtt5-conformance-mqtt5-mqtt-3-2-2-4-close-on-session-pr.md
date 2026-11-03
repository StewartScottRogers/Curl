---
id: BL-4560
title: Fix Mqtt5 conformance: Mqtt5:MQTT-3.2.2-4 (close on Session Present after Clean Start)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientFlow.cs, Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ClientMessages.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4560 — Fix Mqtt5 conformance: Mqtt5:MQTT-3.2.2-4 (close on Session Present after Clean Start)

## Goal

A CONNACK with Session Present 1 answering curl's Clean Start CONNECT ends the mqtt5:// transfer with exit 8 and "MQTT 5 CONNACK Session Present on a Clean Start", sending nothing after CONNECT.

## Context

OASIS MQTT 5.0 MQTT-3.2.2-4: a client without Session State that receives Session Present 1 MUST close the Network Connection. curl always sends Clean Start and keeps no session, so `Mqtt5ClientFlow.RunAsync` must check `Mqtt5ConnAck.SessionPresent` after the reason code. Exit code and text decided in ADR-1193 (BL-4518). Found by BL-4518's `ConnackCases`.

## Acceptance criteria

- [x] `ConnackCases.Connack_SessionPresentAfterCleanStart_ClosesTheConnectionWithExit8` passes in `Curl.Conformance.Extended.UnitTests`.
- [x] A focused unit test in `Curl.Extended.Protocol.Mqtt5.UnitTests` pins the fix (ADR-0482).
- [x] The line for the requirement is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` and the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and `Curl.Extended.Protocol.Mqtt5.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

- `Mqtt5ClientFlow.RunAsync` now checks `SessionPresent` after the CONNACK reason code (`ThrowIfSessionPresent`) and throws exit 8 with `Mqtt5ClientMessages.SessionPresentOnCleanStart`, as ADR-1193 decided; nothing is sent after CONNECT. Pipeline run directly (one check, decision already in ADR-1193). Focused test: `Mqtt5ClientFlowTests.RunAsync_ConnAckWithSessionPresent_EndsWithWeirdServerReplySendingNothingAfterConnect` (publish and subscribe). Measure-CodeQuality for Curl.Extended.Protocol.Mqtt5.UnitLibrary: 0 failing members. Platform-neutral (no paths, OS errors or certificates).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 1e0f1382 7b580d6c 5fe60615 2391cd16.
- 2026-11-02: Doing -> Done. mqtt5:// ends with exit 8 on a CONNACK Session Present after Clean Start
