---
id: BL-081
title: Match curl's state changes on empty MQTT packets
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-048]
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-081 — Match curl's state changes on empty MQTT packets

## Goal

The MQTT subscribe session handles a zero-length packet the way curl 8.21.0 does, so its
exit code and stdout bytes match upstream when a broker sends an empty packet.

## Context

BL-048 implemented MQTT subscribe in `MqttSubscribeSession`
(`Curl.Protocol.Mqtt.UnitLibrary/MqttSubscribeSession.cs`). Its `ReadPacketHeaderAsync`
passes over every zero-length packet except DISCONNECT and keeps waiting for the state
it was in.

curl 8.21.0's `lib/mqtt.c` (`mqtt_doing`, the `MQTT_REMAINING_LENGTH` case, around lines
917-929) does something different for any packet whose remaining length is zero:

- it calls `mqstate(MQTT_FIRST, MQTT_FIRST)`, which drops the state it was waiting for,
  so the next packet that has a body is read as a fresh header rather than as that
  body's packet;
- for a PINGRESP it sets the next state to `MQTT_PUBWAIT`, so a PINGRESP that arrives
  before CONNACK skips the CONNACK check and the SUBSCRIBE entirely.

Measured on 2026-09-26 against the local curl 8.21.0 with a loopback listener: after
CONNACK and SUBACK, sending `30 00` (an empty PUBLISH), then
`30 0C 00 05 "a/b/c" "HELLO"`, then closing the connection made curl exit 0 with nothing
on stdout. The current handler instead writes the second PUBLISH's payload and reports
exit 56. A PINGRESP `D0 00` after SUBACK is passed over by both, and a test already pins
that; keep it green.

Tests live in `Curl.Protocol.Mqtt.UnitTests/MqttProtocolHandlerTests.cs` and use the
fakes under `Curl.Protocol.Mqtt.UnitTests/Fakes/`; no test touches the network.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Mqtt.UnitTests` replays CONNACK, SUBACK, `30 00`,
      `30 0C 00 05 "a/b/c" "HELLO"` and end of stream, and asserts exit code 0
      (`CurlExitCode` for success) and zero bytes written to the output, as measured
      against curl 8.21.0.
- [ ] curl 8.21.0 has been measured with a loopback listener that sends PINGRESP
      `D0 00` before CONNACK; the bytes sent, curl's exit code and its stdout are
      recorded under `Notes` in this task, and a test in `Curl.Protocol.Mqtt.UnitTests`
      replays that exchange and asserts the same exit code and output.
- [ ] The existing test for PINGRESP after SUBACK still passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100%
      branch coverage for `Curl.Protocol.Mqtt.UnitLibrary`, and no method above
      cyclomatic complexity 10 or CRAP 30.
- [ ] `dotnet build Curl.Protocol.Mqtt.UnitLibrary -warnaserror` is clean.
- [ ] `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Returned when the 4-lane shift was stopped to repair task IDs that parallel lanes had duplicated; no lane was working it.
- 2026-09-26: Backlog -> Doing.
