---
id: BL-1341
title: Word every mqtt socket send and receive failure with the shared CurlSocketErrorText table
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1325]
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: FR-085
created: 2026-10-03
completed:
---
# BL-1341 — Word every mqtt socket send and receive failure with the shared CurlSocketErrorText table

## Goal

An `mqtt://` packet send or read that fails with a socket error ends with curl 8.21.0's `Send failure: <words>` (exit 55) / `Recv failure: <words>` (exit 56), the words from `CurlSocketErrorText` (BL-1325) for the platform, instead of `Failed sending data to the peer` / `Failure when receiving data from the peer` for every error but a send reset.

## Context

- Today `Curl.Protocol.Mqtt.UnitLibrary/MqttSession.cs` (lines 498-511) gives a failed send `MqttTransferMessages.SendConnectionReset` (line 38, the Windows words on every platform) when `IsReset` (line 509) sees `SocketError.ConnectionReset`, else `SendFailed` (line 44); reads that fail end with `MqttTransferMessages.ReceiveFailed` (line 33; `MqttPacketReader.cs` line 195, `MqttSession.cs` lines 335 and 364) whatever the cause.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/cf-socket.c` lines 1562 and 1618: every socket error but `EAGAIN` is `failf(data, "Send failure: %s" / "Recv failure: %s", curlx_strerror(...))`, Winsock words on Windows and `strerror`'s elsewhere (BL-1325's Context has the table).
- Use `CurlSocketErrorText.SendFailure` / `ReceiveFailure` where an `IOException` from the connection is caught. A read that ends because the connection closed (no exception, a short packet) is not a socket error and keeps `ReceiveFailed`; so does an `IOException` with no `SocketException`. `MqttTransferMessages` line 94 (texts without a `-v` line of their own) keeps meaning only the fallback texts.
- Existing tests pinning `Connection was reset` hold on Windows only after this change: mark each `[OSCondition(OperatingSystems.Windows)]` and add a non-Windows twin (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`) asserting the exception's own message.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Mqtt.UnitTests` makes the CONNACK read throw `IOException` wrapping `SocketException(SocketError.ConnectionAborted)` and asserts exit 56 with `Recv failure: Connection was aborted` (Windows-only), plus a non-Windows twin with `Recv failure: ` + the exception's own message.
- [ ] A test makes the CONNECT send fail the same way and asserts exit 55 with `Send failure: Connection was aborted` (Windows) and its twin.
- [ ] A test pins that a connection closed mid-packet still gives `Failure when receiving data from the peer`.
- [ ] Every existing reset test is split by platform; `MqttTransferMessages` no longer declares its own `Connection was reset` constant.
- [ ] `dotnet build Curl.Protocol.Mqtt.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
