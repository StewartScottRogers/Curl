---
id: BL-1310
title: Fail an MQTT subscription with exit 8 when a PUBLISH payload arrives under -I
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1310 — Fail an MQTT subscription with exit 8 when a PUBLISH payload arrives under -I

## Goal

An `mqtt://` subscription run with `-I` (`ITransferContext.NoBody`) ends with exit 8 `Weird server reply` and writes nothing when the first PUBLISH payload arrives, as curl 8.21.0 does, instead of writing the payload and finishing.

## Context

- curl 8.21.0 (tag `curl-8_21_0`): `lib/mqtt.c` `mqtt_read_publish`, lines 709-750, passes each read of the PUBLISH body to `Curl_client_write(data, CLIENTWRITE_BODY, ...)`, which meets `cw_download_write` in `lib/sendf.c` lines 214-224: with `no_body` set and no headers received, the first body bytes close the connection ("ignoring body") and return `CURLE_WEIRD_SERVER_REPLY` (exit 8) without `failf`, so there is no `-v` line for it and the exit text is curl_easy_strerror's `Weird server reply`.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Script` serving CONNACK (`\x20\x02\x00\x00`), then SUBACK plus a PUBLISH of topic `t` and payload `hi` (`\x90\x03\x00\x01\x00\x30\x05\x00\x01thi`), and `-sv -I mqtt://127.0.0.1:PORT/t`: exit 8, stdout empty; stderr after the existing `mqtt_doing` lines ends `* Remaining length: 5 bytes`, `{ [5 bytes data]`, `* shutting down connection #0`. The same run without `-I` writes the 5 bytes and is identical in Curl today, apart from the random client ID.
- Curl today, `Curl.Protocol.Mqtt.UnitLibrary/MqttSession.cs`: `WritePublishAsync` already applies `--max-filesize` (`Maximum file size exceeded`, exit 63, as `mqtt.c` lines 712-716 do) and `WriteOutputSliceAsync` reports the slice as received data and writes it; nothing reads `NoBody`. `MqttProtocolHandler` passes the context in.
- Only the subscribe path receives a body; a publish (`-d`) under `-I` is unchanged.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Mqtt.UnitTests` runs the measured exchange through the fake connection with `NoBody = true` and asserts exit 8 (`CurlExitCode.WeirdServerReply`), message `Weird server reply`, nothing written to the output, the 5-byte received-data event still reported, `Remaining length: 5 bytes` reported, no info line for the failure, and the connection ending with `shutting down connection #0`.
- [ ] A test pins that a PUBLISH whose remaining length is over `MaxFileSize` still fails with exit 63 `Maximum file size exceeded` under `NoBody = true` (the size check comes first, as in `mqtt.c`).
- [ ] A test pins that a publish (`PostData` set) with `NoBody = true` sends its PUBLISH and ends with exit 0 as today.
- [ ] `dotnet build Curl.Protocol.Mqtt.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-02: Created.
