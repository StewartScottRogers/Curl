---
id: BL-4261
title: Extension Mqtt5: honour a broker's Server Keep Alive with PINGREQ while subscribed
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4243]
touches: [Curl.Extension.Mqtt5.UnitLibrary/Client/Mqtt5ClientFlow.cs, Curl.Extension.Mqtt5.UnitLibrary/Client/Mqtt5ProtocolHandler.cs, Curl.Extension.Mqtt5.UnitTests/Client/Mqtt5ClientFlowTests.cs, Documentation/Planning/Decisions/ADR-1049-the-mqtt5-client-follows-curls-mqtt-flow-and-ends-a-failure-reason-code-as-a-weird-server-reply.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4261 — Extension Mqtt5: honour a broker's Server Keep Alive with PINGREQ while subscribed

## Goal

When a broker returns Server Keep Alive in CONNACK, the `mqtt5://` subscriber sends PINGREQ (and expects PINGRESP) within that interval while it waits for messages, so the broker does not drop it.

## Context

Found by BL-4243. ADR-1049: CONNECT asks for Keep Alive 0, but OASIS MQTT 5.0 section 3.2.2.3.14 says the client MUST use a Server Keep Alive the server returns. `Mqtt5ClientFlow.WritePublishedUntilDisconnectAsync` today treats any packet but PUBLISH and DISCONNECT as a weird server reply, so PINGRESP must be accepted there. Pace with an injected `TimeProvider` (curl's `mqtt://` pings after idle in `MqttSession.PingIfIdleBeforeAsync`, a model).

## Acceptance criteria

- [x] With Server Keep Alive N in CONNACK, an idle subscription sends PINGREQ before N seconds pass and accepts PINGRESP; unit tests with a fake `TimeProvider` and a fake `IConnection`.
- [x] ADR-1049's Keep Alive bullet says what the client now does.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4243.
- Added `Curl.Extension.Mqtt5.UnitLibrary/Client/Mqtt5ProtocolHandler.cs` to touches: it constructs the flow and must pass `context.TimeProvider`; no task in Doing on work/dark-factory names it.
- Decided: PINGREQ every half of Server Keep Alive (margin for the network inside the 1.5 N broker limit), timed from the last PINGREQ whatever the server sends, since Keep Alive counts client sends only; a missing PINGRESP is not timed out, the broker closing ends the transfer with a receive error. Recorded in ADR-1049. A server PINGREQ or any other packet still ends with a weird server reply.
- Measure-CodeQuality.ps1 -Library Curl.Extension.Mqtt5.UnitLibrary: 100% line, 100% branch, max complexity 10, 0 failing members. Full `dotnet build` clean (0 warnings); full fast run green. Change is platform-neutral (fake TimeProvider and IConnection, no paths or OS text).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d9d5fa78 18146616 14de81cc.
- 2026-10-29: Doing -> Done. mqtt5:// subscriber sends PINGREQ every half of a broker's Server Keep Alive and accepts PINGRESP
