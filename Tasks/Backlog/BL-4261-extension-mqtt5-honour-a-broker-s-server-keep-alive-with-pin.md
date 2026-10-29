---
id: BL-4261
title: Extension Mqtt5: honour a broker's Server Keep Alive with PINGREQ while subscribed
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4243]
touches: [Curl.Extension.Mqtt5.UnitLibrary/Client/Mqtt5ClientFlow.cs, Curl.Extension.Mqtt5.UnitTests/Client/Mqtt5ClientFlowTests.cs, Documentation/Planning/Decisions/ADR-1046-the-mqtt5-client-follows-curls-mqtt-flow-and-ends-a-failure-reason-code-as-a-weird-server-reply.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4261 — Extension Mqtt5: honour a broker's Server Keep Alive with PINGREQ while subscribed

## Goal

When a broker returns Server Keep Alive in CONNACK, the `mqtt5://` subscriber sends PINGREQ (and expects PINGRESP) within that interval while it waits for messages, so the broker does not drop it.

## Context

Found by BL-4243. ADR-1046: CONNECT asks for Keep Alive 0, but OASIS MQTT 5.0 section 3.2.2.3.14 says the client MUST use a Server Keep Alive the server returns. `Mqtt5ClientFlow.WritePublishedUntilDisconnectAsync` today treats any packet but PUBLISH and DISCONNECT as a weird server reply, so PINGRESP must be accepted there. Pace with an injected `TimeProvider` (curl's `mqtt://` pings after idle in `MqttSession.PingIfIdleBeforeAsync`, a model).

## Acceptance criteria

- [ ] With Server Keep Alive N in CONNACK, an idle subscription sends PINGREQ before N seconds pass and accepts PINGRESP; unit tests with a fake `TimeProvider` and a fake `IConnection`.
- [ ] ADR-1046's Keep Alive bullet says what the client now does.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4243.

## Log

- 2026-10-29: Created.
