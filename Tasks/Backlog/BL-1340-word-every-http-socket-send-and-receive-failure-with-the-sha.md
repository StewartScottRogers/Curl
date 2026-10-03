---
id: BL-1340
title: Word every http socket send and receive failure with the shared CurlSocketErrorText table
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1325, BL-1331]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-085
created: 2026-10-03
completed:
---
# BL-1340 — Word every http socket send and receive failure with the shared CurlSocketErrorText table

## Goal

An HTTP/1.x request send or response read that fails with any socket error ends with curl 8.21.0's `Send failure: <words>` (exit 55) / `Recv failure: <words>` (exit 56), the words from `CurlSocketErrorText` (BL-1325) for the platform, so an aborted connection reads `Recv failure: Connection was aborted` on Windows instead of `Failure when receiving data from the peer`, and a reset reads `Connection reset by peer` off Windows.

## Context

- Today `Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs` `ReceiveFailure(IOException)` (line 427) gives the missing-`close_notify` text, `ConnectionReset` (line 65, `Recv failure: Connection was reset` on every platform) for `SocketError.ConnectionReset`, or `ReceiveFailed` (line 70); `SendFailure(IOException)` (line 451) gives `SendConnectionReset` (line 437) or `SendFailed` (line 443). FR-085 in `Documentation/Product/Requirements.md` records the measured reset text.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/cf-socket.c` lines 1562 and 1618: every socket error but `EAGAIN` is `failf(data, "Send failure: %s" / "Recv failure: %s", curlx_strerror(...))`, Winsock words on Windows and `strerror`'s elsewhere (BL-1325's Context has the table).
- Change only these two helpers to use `CurlSocketErrorText.ReceiveFailure` / `SendFailure`, keeping the missing-`close_notify` case first and the fallback texts for an `IOException` with no `SocketException`. HTTP/2 and HTTP/3 failure texts are not this task's.
- Existing tests pinning `Connection was reset` hold on Windows only after this change: mark each `[OSCondition(OperatingSystems.Windows)]` and add a non-Windows twin (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`) asserting the exception's own message.
- BL-1331 also changes this library; this task waits for it so the two do not collide.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Http.UnitTests` makes the response read throw `IOException` wrapping `SocketException(SocketError.ConnectionAborted)` and asserts exit 56 with `Recv failure: Connection was aborted` (Windows-only) plus a non-Windows twin with `Recv failure: ` + the exception's own message.
- [ ] A test makes the request send fail the same way and asserts exit 55 with `Send failure: Connection was aborted` (Windows) and its twin.
- [ ] Every existing reset test is split by platform; a missing `close_notify` keeps its text and an `IOException` with no socket error keeps the fallback texts.
- [ ] `HttpTransferMessages` no longer declares its own `Connection was reset` constants.
- [ ] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
