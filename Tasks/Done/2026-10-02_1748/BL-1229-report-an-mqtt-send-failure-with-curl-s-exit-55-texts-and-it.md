---
id: BL-1229
title: Report an MQTT send failure with curl's exit 55 texts and its 'Error 55 sending MQTT CONNECT request' -v line
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1229 — Report an MQTT send failure with curl's exit 55 texts and its 'Error 55 sending MQTT CONNECT request' -v line

## Goal

An `mqtt://` transfer whose packet cannot be sent ends with exit 55 and curl 8.21.0's message (`Send failure: Connection was reset` for a reset, `Failed sending data to the peer` otherwise), and when the packet was the CONNECT, `-v` also shows curl's `Error 55 sending MQTT CONNECT request`.

## Context

- Today `Curl.Protocol.Mqtt.UnitLibrary/MqttSession.cs` `SendAsync` turns any `IOException` into exit 55 with `MqttTransferMessages.SendFailed`, `Failure when sending data to the peer`, which is no curl message: `curl_easy_strerror(CURLE_SEND_ERROR)` is `Failed sending data to the peer` (`lib/strerror.c` lines 177-178 at `curl-8_21_0`). `MqttTransferMessages.IsStrerrorText` lists it among the texts that get no `-v` line. The old text is pinned by `MqttProtocolHandlerTests.cs` (around lines 674 and 941).
- curl 8.21.0, `lib/mqtt.c` at `curl-8_21_0`: `mqtt_send` (lines 133-161) returns the send's error; `mqtt_do` (lines 764-781) answers a failed CONNECT with `failf(data, "Error %d sending MQTT CONNECT request", (int)result)`, so 55 gives `Error 55 sending MQTT CONNECT request`. SUBSCRIBE, PUBLISH, PINGREQ and DISCONNECT failures add no line. The socket filter's earlier `failf` (`Send failure: <error>`) is the message curl prints for a socket error.
- Copy the reset test and the `-v` order from `Curl.Protocol.Dict.UnitLibrary/DictIoFailures.cs` (BL-1125's Notes): the message unless it is the fallback text, then the protocol's own line; do not reference that library.

## Acceptance criteria

- [x] `MqttTransferMessages` holds no `Failure when sending data to the peer`, and `IsStrerrorText` names curl's real fallback text.
- [x] Tests in `Curl.Protocol.Mqtt.UnitTests` with a fake connection whose write throws pin: on the CONNECT, a reset gives exit 55 `Send failure: Connection was reset` with the `-v` lines `Send failure: Connection was reset` then `Error 55 sending MQTT CONNECT request`, and any other `IOException` gives exit 55 `Failed sending data to the peer` with the `-v` line `Error 55 sending MQTT CONNECT request`; on the SUBSCRIBE and on the PUBLISH, the same exit codes and messages with no `Error 55 ...` line.
- [x] The tests that pinned the old text are updated; every other MQTT test passes unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- `MqttSession.SendAsync` picks `Send failure: Connection was reset` when the `IOException`'s inner `SocketException` is `ConnectionReset` (the same test as Dict's), else `Failed sending data to the peer` (the `SendFailed` constant, still in `IsStrerrorText`). The CONNECT's send passes `MqttTransferMessages.ConnectNotSent`, carried on `MqttTransferException.FollowingLine`; `MqttProtocolHandler.ReportConnectionEnd` writes it after the message and before `shutting down connection #N`. SUBSCRIBE, PUBLISH, PINGREQ and DISCONNECT pass none.
- Tests: `MqttProtocolHandlerSendFailureTests` (6), with `ScriptedConnection.WriteFailure` / `WritesBeforeFailure` added to the fake; the two old-text assertions in `MqttProtocolHandlerTests` updated. 114 MQTT tests pass; Measure-CodeQuality: 100% line, 100% branch, 0 failing members.
- Decision taken as the task states: for a non-reset CONNECT failure the exit message stays the fallback text, with `Error 55 ...` only as the `-v` line, matching Dict's BL-1125 order. In real curl the socket filter always `failf`s first, so the fallback case is our stand-in for an `IOException` with no socket error.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. MQTT send failures exit 55 with curl's reset/fallback texts; a failed CONNECT adds 'Error 55 sending MQTT CONNECT request' to -v
