---
id: BL-1115
title: Fail an MQTT PUBLISH larger than --max-filesize with curl's exit 63
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1115 — Fail an MQTT PUBLISH larger than --max-filesize with curl's exit 63

## Goal

A subscribed `mqtt://` transfer that receives a PUBLISH whose remaining length is larger than `--max-filesize` ends with exit 63 `Maximum file size exceeded` and writes none of that PUBLISH, as curl 8.21.0 does; today `--max-filesize` is ignored for MQTT.

## Context

- curl 8.21.0, `lib/mqtt.c` `mqtt_doing`, `MQTT_REMAINING_LENGTH` case (around line 710 at https://github.com/curl/curl/blob/curl-8_21_0/lib/mqtt.c): `infof(data, "Remaining length: %zu bytes", remlen); if(data->set.max_filesize && (curl_off_t)remlen > data->set.max_filesize) { failf(data, "Maximum file size exceeded"); result = CURLE_FILESIZE_EXCEEDED; }`. The limit is per PUBLISH (its whole body, topic length and topic included), and a limit of 0 means none.
- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response '\x20\x02\x00\x00\x90\x03\x00\x01\x00\x30\x0a\x00\x03t/xhello'`: `-v --max-filesize 9 mqtt://127.0.0.1:<port>/t/x` exits 63 with stdout empty, and stderr ends `* mqtt_doing: state [5]`, `* Remaining length: 10 bytes`, `* Maximum file size exceeded`, `* shutting down connection #0`, `curl: (63) Maximum file size exceeded`. With `--max-filesize 10` the 10 bytes are written (and the transfer then ends with exit 56 when the canned server closes).
- Curl today: `Curl.Protocol.Mqtt.UnitLibrary/MqttProtocolHandler.cs` `TransferAsync` builds `MqttSession` without `ITransferContext.MaxFileSize`; `MqttSession.WritePublishAsync` reports `MqttTransferMessages.RemainingLength` and reads the body unconditionally. Pass the limit in and check it right after that line, throwing `MqttTransferException(CurlExitCode.FilesizeExceeded, ...)`; add the message to `MqttTransferMessages`.
- Check where the `shutting down connection #N` line comes from for this failure (the handler's existing end-of-connection reporting) and pin it as measured.

## Acceptance criteria

- [ ] New tests in `Curl.Protocol.Mqtt.UnitTests/MqttProtocolHandlerTests.cs`: with `MaxFileSize = 9` and the exchange above the result is exit 63 `Maximum file size exceeded` with 0 bytes written; with `MaxFileSize = 10`, and with `MaxFileSize` null or 0, the PUBLISH is written.
- [ ] A test in `MqttProtocolHandlerTransferEventsTests.cs` pins the measured `-v` info lines in order: `Remaining length: 10 bytes`, `Maximum file size exceeded`, `shutting down connection #0`.
- [ ] A test pins that the limit applies to each PUBLISH on its own: two PUBLISHes of 10 bytes each under `MaxFileSize = 10` are both written.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
