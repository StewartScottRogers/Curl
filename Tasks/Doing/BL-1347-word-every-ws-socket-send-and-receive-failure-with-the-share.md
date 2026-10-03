---
id: BL-1347
title: Word every ws socket send and receive failure with the shared CurlSocketErrorText table
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1325]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: FR-085
created: 2026-10-03
completed:
---
# BL-1347 — Word every ws socket send and receive failure with the shared CurlSocketErrorText table

## Goal

A `ws://` upgrade send, frame send or frame read that fails with any socket error ends with curl 8.21.0's `Send failure: <words>` (exit 55) / `Recv failure: <words>` (exit 56), the words from `CurlSocketErrorText` (BL-1325) for the platform, instead of the fallback texts for every error but a reset.

## Context

- Today `Curl.Protocol.Ws.UnitLibrary/WsIoFailures.cs` keeps `SendConnectionReset` / `ReceiveConnectionReset` (lines 15 and 21, the Windows words on every platform), `SendFailedMessage` / `ReceiveFailedMessage` (lines 18 and 24), and `IsReset` (line 50) recognises only `SocketError.ConnectionReset`.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/cf-socket.c` lines 1562 and 1618: every socket error but `EAGAIN` is `failf(data, "Send failure: %s" / "Recv failure: %s", curlx_strerror(...))`, Winsock words on Windows and `strerror`'s elsewhere (BL-1325's Context has the table).
- Replace the reset constants and `IsReset` with `CurlSocketErrorText.SendFailure` / `ReceiveFailure`, keeping the fallback texts for an `IOException` with no `SocketException`. A `wss://` failure that surfaces through TLS keeps the scope the reset check has today.
- Existing tests pinning `Connection was reset` hold on Windows only after this change: mark each `[OSCondition(OperatingSystems.Windows)]` and add a non-Windows twin (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`) asserting the exception's own message.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Ws.UnitTests` makes a frame read throw `IOException` wrapping `SocketException(SocketError.ConnectionAborted)` and asserts exit 56 with `Recv failure: Connection was aborted` (Windows-only), plus a non-Windows twin with `Recv failure: ` + the exception's own message.
- [ ] A test makes the upgrade request's send fail the same way and asserts exit 55 with `Send failure: Connection was aborted` (Windows) and its twin.
- [ ] Every existing reset test is split by platform; an `IOException` with no socket error still gives the fallback texts; `WsIoFailures` no longer declares its own `Connection was reset` constants.
- [ ] `dotnet build Curl.Protocol.Ws.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Ws.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
