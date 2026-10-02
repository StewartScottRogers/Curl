---
id: BL-1116
title: Send curl's MQTT PINGREQ after 60 seconds idle while subscribed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1115]
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1116 — Send curl's MQTT PINGREQ after 60 seconds idle while subscribed

## Goal

While an `mqtt://` transfer waits for the next packet, it sends a PINGREQ (`C0 00`) once more than 60 seconds have passed since it last sent or received anything, and reports curl's `mqtt_ping: sent ping request.` `-v` line, as curl 8.21.0 does to keep the broker from dropping a quiet subscription; today Curl never pings, so a broker enforcing the 60-second keep-alive the CONNECT announces disconnects it.

## Context

- curl 8.21.0, `lib/mqtt.c` `mqtt_ping` (around line 798 at https://github.com/curl/curl/blob/curl-8_21_0/lib/mqtt.c), called at the top of every `mqtt_doing`: when the state is `MQTT_FIRST`, no ping is outstanding and `data->set.upkeep_interval_ms > 0`, and more than `upkeep_interval_ms` have passed since `mq->lastTime`, it sends `{ 0xC0, 0x00 }`, sets `pingsent`, and `infof(data, "mqtt_ping: sent ping request.")`. `lastTime` is set when the CONNECT is sent, when a packet's first byte arrives and when PUBLISH bytes arrive; a PINGRESP clears `pingsent` (line 925-927). The curl tool never sets `CURLOPT_UPKEEP_INTERVAL_MS`, so the default applies: `CURL_UPKEEP_INTERVAL_DEFAULT` is `60000L` (`include/curl/curl.h` line 972; `lib/url.c` line 422).
- The CONNECT Curl sends already announces a 60-second keep-alive (`Curl.Protocol.Mqtt.UnitLibrary/MqttPackets.cs` `BuildConnect`), and a PINGRESP is already handled (`MqttSession.StateAfterEmptyPacket`, `MqttTransferMessages.ReceivedPingResponse`).
- Curl today: `MqttSession.RunAsync` awaits `MqttPacketReader.ReadFixedHeaderAsync` with no timer, and `MqttSession` has no `TimeProvider`. Pass `ITransferContext.TimeProvider` in from `MqttProtocolHandler.TransferAsync` and race the first-byte read against a delay on that provider (never `Thread.Sleep` or `Task.Delay` without the provider); the read that was racing must not be lost or doubled when the timer wins.
- Tests use a hand-written manual `TimeProvider` (the BCL has none for tests and no package may be added; `Curl.Conformance.UnitTests/ManualTimeProvider.cs` is an example to copy, not reference) and a scripted connection that waits until it is given bytes.
- Out of scope, and to be written in Notes: curl's `mqtt_doing: state [0]` lines for the idle polls in between (one per second of the tool's poll loop), which this task does not reproduce.

## Acceptance criteria

- [x] New tests in `Curl.Protocol.Mqtt.UnitTests`: after SUBACK, advancing the time provider by 60 s sends nothing and by 60.001 s sends exactly `C0 00` and reports `mqtt_ping: sent ping request.`; a second PINGREQ is not sent before a PINGRESP arrives, and is sent again 60 s after the PINGRESP.
- [x] A test pins that a PUBLISH arriving at 59 s restarts the 60-second count.
- [x] A test pins that a publish (`-d`) transfer, which ends with DISCONNECT, never pings.
- [x] Every existing MQTT test passes unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered directly in the session (one library and its tests; the plan was the task's Context).
- `MqttPacketReader.WhenFirstByteReadyAsync` starts the connection read and keeps it in
  `pendingRead`; `FillAsync` takes that same task, so the read racing the timer is never
  lost or doubled. The returned task never faults; a failed read throws from
  `ReadFixedHeaderAsync` as before.
- `MqttSession` races it against `Task.Delay(60.001 s, TimeProvider)`: curl pings when the
  whole-millisecond difference exceeds 60000, so 60.001 s is the first instant it does.
  After the PINGREQ it reports `mqtt_ping: sent ping request.` then one
  `mqtt_doing: state [0]`, the order curl's `mqtt_doing` writes them in.
- Choice: the idle count starts when the wait for a first byte begins, not at a stored
  `lastTime`. Every moment curl sets `lastTime` (a send, a packet's first byte, PUBLISH
  bytes) is followed at once by that wait, so the two differ only by processing time; and
  reading the clock only when a wait begins keeps the existing diagnostic-log tests, whose
  clock steps 250 ms per read, unchanged.
- Like curl, the PINGREQ can also go out while the CONNACK or SUBACK is awaited (curl's
  state is `MQTT_FIRST` there too); a `-d` publish ends with DISCONNECT straight after the
  CONNACK and never waits, so never pings.
- Out of scope: curl's `mqtt_doing: state [0]` line for each idle poll of the tool's
  one-second loop between packets is not reproduced.
- Tests: `MqttProtocolHandlerKeepAliveTests` (4), with `Fakes/ManualTimeProvider.cs`
  (copied from Curl.Conformance.UnitTests, plus a lock and `NextTimerDueAt`) and
  `Fakes/GatedConnection.cs` (reads wait on a channel the test feeds). MQTT tests 108/108;
  Measure-CodeQuality: 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. An idle mqtt:// transfer sends PINGREQ C0 00 after 60.001 s and reports mqtt_ping: sent ping request., as curl 8.21.0 does
