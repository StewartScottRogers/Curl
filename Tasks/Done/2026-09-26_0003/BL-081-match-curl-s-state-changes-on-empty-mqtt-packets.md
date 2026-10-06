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
completed: 2026-09-26
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

- [x] A test in `Curl.Protocol.Mqtt.UnitTests` replays CONNACK, SUBACK, `30 00`,
      `30 0C 00 05 "a/b/c" "HELLO"` and end of stream, and asserts exit code 0
      (`CurlExitCode` for success) and zero bytes written to the output, as measured
      against curl 8.21.0.
- [x] curl 8.21.0 has been measured with a loopback listener that sends PINGRESP
      `D0 00` before CONNACK; the bytes sent, curl's exit code and its stdout are
      recorded under `Notes` in this task, and a test in `Curl.Protocol.Mqtt.UnitTests`
      replays that exchange and asserts the same exit code and output.
- [x] The existing test for PINGRESP after SUBACK still passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100%
      branch coverage for `Curl.Protocol.Mqtt.UnitLibrary`, and no method above
      cyclomatic complexity 10 or CRAP 30.
- [x] `dotnet build Curl.Protocol.Mqtt.UnitLibrary -warnaserror` is clean.
- [x] `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

- Plan: `MqttSession.RunAsync` is now one loop over a `SessionState` that mirrors curl
  8.21.0's `mqttstate`/`nextstate` in `mqtt_doing`: `AwaitingConnack` (curl's
  `MQTT_CONNACK`), `AwaitingPublishOrSuback` (`MQTT_SUBACK`/`MQTT_PUBWAIT`),
  `LeavingBodyUnread` (next state `MQTT_FIRST`: the body is not read, so its bytes are
  taken as the next fixed header), `StateNotHandled` (next state `MQTT_NOSTATE`: the next
  packet with a body ends the transfer with exit 0, as curl's `State not handled yet`
  does) and `Done`. An empty packet sets the state whatever it was: DISCONNECT -> `Done`,
  PINGRESP -> `AwaitingPublishOrSuback`, anything else -> `LeavingBodyUnread`.
- Measured 2026-09-26 against `C:/Program Files/Git/mingw64/bin/curl.exe` (curl 8.21.0)
  with a Python loopback listener that waits for curl's CONNECT before each scripted send,
  run as `curl -s -S mqtt://127.0.0.1:<port>/a/b/c`:
  - `20 02 00 00 90 03 00 01 00`, then `30 00 30 0C 00 05 "a/b/c" "HELLO"`, then close:
    exit 0, stdout empty, stderr empty; curl sent CONNECT and SUBSCRIBE.
  - `D0 00 20 02 00 00` (PINGRESP before CONNACK, then CONNACK), then close: exit 8,
    stdout empty, stderr `curl: (8) Weird server reply`; curl sent only CONNECT.
  - `D0 00 30 0C 00 05 "a/b/c" "HELLO"` (PINGRESP, then PUBLISH, no CONNACK), then close:
    exit 56, stdout `00 05 "a/b/c" "HELLO"` (12 bytes), stderr
    `curl: (56) Connection disconnected`; curl sent only CONNECT, no SUBSCRIBE.
  Pinned by `ExecuteAsync_EmptyPublishThenPublish_WritesNothingAndSucceeds`,
  `ExecuteAsync_PingResponseBeforeConnack_ConnackIsWeirdServerReplyAndSubscribesNothing`
  and `ExecuteAsync_PingResponseThenPublishWithoutConnack_WritesPublishWithoutSubscribing`.
- Choice: `MqttPackets.BuildConnect` measured cyclomatic complexity 12 in the Cobertura
  report, failing the fourth criterion, so its flag computation moved to `ConnectFlags`
  (now 8 and 4). No behaviour change; the CONNECT tests pin the bytes.
- Learned: `Measure-CodeQuality.ps1` defaults to `%TEMP%\CurlCodeQuality`, which every
  dark factory lane shares, and it merges every report it finds there, taking the maximum
  complexity. Parallel lanes therefore read each other's reports (a stale `BuildConnect`
  and a doubled member count showed up). Pass a lane-private `-ResultsDirectory` when
  measuring in a lane.
- `MqttProtocolHandlerTests.cs` was LF throughout, which `dotnet format` rejects; it is
  now CRLF like the rest of the solution.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Returned when the 4-lane shift was stopped to repair task IDs that parallel lanes had duplicated; no lane was working it.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. MQTT subscribe follows curl 8.21.0's state changes on empty packets: an empty PUBLISH leaves the next body unread and ends with exit 0, and a PINGRESP before CONNACK skips the CONNACK check and SUBSCRIBE
