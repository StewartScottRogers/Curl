---
id: BL-1434
title: Write curl's EEEE AAAAGAIN -v line when an MQTT PUBLISH payload read has to wait
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: FR-039
created: 2026-10-04
completed:
---
# BL-1434 — Write curl's EEEE AAAAGAIN -v line when an MQTT PUBLISH payload read has to wait

## Goal

Under `-v`, when an MQTT PUBLISH body has not fully arrived and the next read would have to wait for the server, Curl writes curl 8.21.0's `* EEEE AAAAGAIN` line once for each such wait, as curl does for a reply split across segments.

## Context

- Upstream (tag `curl-8_21_0`), `lib/mqtt.c` lines 720-733 (`mqtt_read_publish`, state `MQTT_PUB_REMAIN`): curl reads the rest of the PUBLISH body with `Curl_xfer_recv`; when it returns `CURLE_AGAIN` - nothing more is buffered on the socket yet - curl writes `infof(data, "EEEE AAAAGAIN")` and returns to the multi loop, which calls it again once the socket is readable. So a PUBLISH whose fixed header arrives in one segment and whose payload arrives in a later one writes the line once before the payload; a payload split into two segments with a gap between them writes it again before the second part. A body that arrives together with its header writes nothing.
- Curl today: `Curl.Protocol.Mqtt.UnitLibrary/MqttSession.cs` `WritePublishAsync` awaits `MqttPacketReader.ReadChunkAsync` until the body is whole and writes no such line. `IConnection.ReadAsync` returns a `ValueTask<int>`; a read that does not complete synchronously (`!valueTask.IsCompleted`) is the equivalent of curl's `CURLE_AGAIN`: no data was waiting. Use that as the model and say so in the member's remarks.
- Measure first with `Record-CurlExchange.ps1` (its `-Script` mode can send the CONNACK, the SUBACK, a PUBLISH fixed header, then wait `-ScriptGapMilliseconds` before the payload) against real curl `-v mqtt://127.0.0.1:<port>/t`, and record the `stderr.txt` lines and their order (relative to `Remaining length: N bytes`) in Notes. If curl writes the line more than once per gap (a spurious wake-up), note the count observed and pin one per wait.
- The tests use the library's fake connection; give it a way to hand back a read that is pending until the test releases it.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Mqtt.UnitTests` pins a PUBLISH whose payload read is pending: `* EEEE AAAAGAIN` is reported after `Remaining length: N bytes` and before the payload is written, once.
- [ ] A test pins a payload delivered in two parts with the second read pending: one line before each pending read; and a test pins that a payload already buffered with its header writes no line.
- [ ] The existing `server disconnected` and max-filesize tests pass unchanged.
- [ ] `dotnet build Curl.Protocol.Mqtt.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
