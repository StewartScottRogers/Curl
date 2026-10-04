---
id: BL-1442
title: Write an MQTT PUBLISH body as it arrives, with curl's mqtt_doing state [6] lines around each wait
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1434]
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: FR-039
created: 2026-10-04
completed: 2026-10-04
---
# BL-1442 — Write an MQTT PUBLISH body as it arrives, with curl's mqtt_doing state [6] lines around each wait

## Goal

Under `-v`/`--trace`, a PUBLISH body that arrives in parts is reported and written part by part, each part as it arrives, with curl 8.21.0's `mqtt_doing: state [6]` lines where curl writes them.

## Context

- Measured with `Record-CurlExchange.ps1 -Script` (its `pause <ms>` step, BL-1434), curl 8.21.0 Schannel, `-v -s mqtt://127.0.0.1:<port>/t`: server sends `30 0b`, pauses 1500 ms, `00 01 74 68 65 6c`, pauses 1500 ms, `6c 6f 77 6f 72`. curl's stderr, after `* Remaining length: 11 bytes`:
  `* EEEE AAAAGAIN`, `* mqtt_doing: state [6]`, `{ [6 bytes data]`, `* mqtt_doing: state [6]`, `* EEEE AAAAGAIN`, `* mqtt_doing: state [6]`, `{ [5 bytes data]`, `* mqtt_doing: state [0]`.
- Curl today: `Curl.Protocol.Mqtt.UnitLibrary/MqttSession.cs` `WritePublishAsync` collects the whole body in a `MemoryStream` and then writes it (`WriteOutputAsync`), so it reports one data block and none of those `state [6]` lines. BL-1434 added the `EEEE AAAAGAIN` line (`ReportReadMustWait`).
- Keep the existing `server disconnected`, max-filesize and 4096-byte slice tests passing, re-measuring them if the change moves a line.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Mqtt.UnitTests` (using `ScriptedConnection.HeldReads`) pins the measured line order above for the two-part payload, data reported as two blocks.
- [x] Output bytes are unchanged for every existing MQTT test.
- [x] `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` reports no failing member.

## Notes

- Implementation: `MqttSession.WritePublishAsync` no longer gathers the body; it runs curl's `MQTT_PUB_REMAIN` state run by run (`RunPublishReadAsync`). Every run after the first writes `mqtt_doing: state [6]`; a run that finds nothing waiting writes `EEEE AAAAGAIN` and waits; any other run reads every byte readable without waiting, up to 4096 (`ReadWaitingBytesAsync`, the model of one curl socket read into its 4096-byte buffer), and writes it as one block. A run that finds the close writes `server disconnected` (exit 18), which reproduces the old cut-short line order without the separate `ReportServerDisconnected`.
- `MqttPacketReader`: `IsNextByteReady` (starts the pending read and says whether it completed at once, i.e. not `CURLE_AGAIN`); `WhenFirstByteReadyAsync` renamed `WhenNextByteReadyAsync`, since the body now uses it too; `ReadChunkAsync` lost its `whenReadMustWait` callback.
- Decision: a run that stops at a pending read records it (`publishReadPending`), and the next run reports the wait without asking again. Asking again raced the fake's held read (`Task.Yield`) and failed the test; on a real socket it only moves the moment the wait is decided from the start of the next run to the end of this one, microseconds apart.
- `ExecuteAsync_PublishPayloadReadPending_ReportsAgainOnceBeforeThePayload` now has `state [6]` after `EEEE AAAAGAIN`: BL-1434's measurement (its Notes) shows curl writes it when the wait ends, and the old test pinned Curl's transcript of the time. The two-part test is renamed `..._ReportsEachPartAsItArrivesWithTheRemainState` and pins the full measured order.
- Output bytes unchanged: the 4096-byte slice and measured write-failure tests pass untouched. Quality: `Curl.Protocol.Mqtt.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
- 2026-10-04: Doing -> Done. -v writes an MQTT PUBLISH body part by part as it arrives, with curl's mqtt_doing state [6] lines around each wait
