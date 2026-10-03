---
id: BL-1343
title: Word every rtsp socket send and receive failure with the shared CurlSocketErrorText table
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1325]
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: FR-085
created: 2026-10-03
completed:
---
# BL-1343 — Word every rtsp socket send and receive failure with the shared CurlSocketErrorText table

## Goal

An `rtsp://` request send or reply read that fails with any socket error ends with curl 8.21.0's `Send failure: <words>` (exit 55) / `Recv failure: <words>` (exit 56), the words from `CurlSocketErrorText` (BL-1325) for the platform, instead of the fallback texts for every error but a reset.

## Context

- Today `Curl.Protocol.Rtsp.UnitLibrary/RtspIoFailures.cs` keeps `SendConnectionReset` / `ReceiveConnectionReset` (lines 14 and 20, the Windows words on every platform), `SendFailedMessage` / `ReceiveFailedMessage` (lines 17 and 23), and `IsReset` (line 49) recognises only `SocketError.ConnectionReset`.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/cf-socket.c` lines 1562 and 1618: every socket error but `EAGAIN` is `failf(data, "Send failure: %s" / "Recv failure: %s", curlx_strerror(...))`, Winsock words on Windows and `strerror`'s elsewhere (BL-1325's Context has the table). BL-1230's `Failed sending RTSP request` line after a failed send stays as it is.
- Replace the reset constants and `IsReset` with `CurlSocketErrorText.SendFailure` / `ReceiveFailure`, keeping the fallback texts for an `IOException` with no `SocketException`.
- Existing tests pinning `Connection was reset` hold on Windows only after this change: mark each `[OSCondition(OperatingSystems.Windows)]` and add a non-Windows twin (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`) asserting the exception's own message.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Rtsp.UnitTests` makes the reply read throw `IOException` wrapping `SocketException(SocketError.ConnectionAborted)` and asserts exit 56 with `Recv failure: Connection was aborted` (Windows-only), plus a non-Windows twin with `Recv failure: ` + the exception's own message.
- [ ] A test makes the request send fail the same way and asserts exit 55 with `Send failure: Connection was aborted` (Windows) and its twin, still followed by the `Failed sending RTSP request` info line.
- [ ] Every existing reset test is split by platform; an `IOException` with no socket error still gives the fallback texts; `RtspIoFailures` no longer declares its own `Connection was reset` constants.
- [ ] `dotnet build Curl.Protocol.Rtsp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Rtsp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
