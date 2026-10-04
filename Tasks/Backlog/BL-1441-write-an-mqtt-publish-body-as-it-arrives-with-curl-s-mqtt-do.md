---
id: BL-1441
title: Write an MQTT PUBLISH body as it arrives, with curl's mqtt_doing state [6] lines around each wait
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1434]
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: FR-039
created: 2026-10-04
completed:
---
# BL-1441 — Write an MQTT PUBLISH body as it arrives, with curl's mqtt_doing state [6] lines around each wait

## Goal

Under `-v`/`--trace`, a PUBLISH body that arrives in parts is reported and written part by part, each part as it arrives, with curl 8.21.0's `mqtt_doing: state [6]` lines where curl writes them.

## Context

- Measured with `Record-CurlExchange.ps1 -Script` (its `pause <ms>` step, BL-1434), curl 8.21.0 Schannel, `-v -s mqtt://127.0.0.1:<port>/t`: server sends `30 0b`, pauses 1500 ms, `00 01 74 68 65 6c`, pauses 1500 ms, `6c 6f 77 6f 72`. curl's stderr, after `* Remaining length: 11 bytes`:
  `* EEEE AAAAGAIN`, `* mqtt_doing: state [6]`, `{ [6 bytes data]`, `* mqtt_doing: state [6]`, `* EEEE AAAAGAIN`, `* mqtt_doing: state [6]`, `{ [5 bytes data]`, `* mqtt_doing: state [0]`.
- Curl today: `Curl.Protocol.Mqtt.UnitLibrary/MqttSession.cs` `WritePublishAsync` collects the whole body in a `MemoryStream` and then writes it (`WriteOutputAsync`), so it reports one data block and none of those `state [6]` lines. BL-1434 added the `EEEE AAAAGAIN` line (`ReportReadMustWait`).
- Keep the existing `server disconnected`, max-filesize and 4096-byte slice tests passing, re-measuring them if the change moves a line.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Mqtt.UnitTests` (using `ScriptedConnection.HeldReads`) pins the measured line order above for the two-part payload, data reported as two blocks.
- [ ] Output bytes are unchanged for every existing MQTT test.
- [ ] `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
