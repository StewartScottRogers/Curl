---
id: BL-1338
title: Word every ftp socket send and receive failure with the shared CurlSocketErrorText table
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1325, BL-1333]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: FR-085
created: 2026-10-03
completed: 2026-10-03
---
# BL-1338 — Word every ftp socket send and receive failure with the shared CurlSocketErrorText table

## Goal

An `ftp://` control-connection send or data-connection receive that fails with any socket error ends with curl 8.21.0's `Send failure: <words>` (exit 55) / `Recv failure: <words>` (exit 56), the words from `CurlSocketErrorText` (BL-1325) for the platform, instead of `Failed sending data to the peer` / `Failure when receiving data from the peer` for every error but a reset.

## Context

- Today `Curl.Protocol.Ftp.UnitLibrary/FtpTransferMessages.cs` `SendFailed(IOException)` (line 86) gives `SendConnectionReset` (line 73, the Windows words on every platform) for `SocketError.ConnectionReset` and `SendFailedToPeer` (line 79) for anything else; a data connection that fails mid-transfer always gets `ReceiveFailed` (line 105), used in `FtpSession.cs` line 1592, whatever the socket error.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/cf-socket.c` lines 1562 and 1618: every socket error but `EAGAIN` is `failf(data, "Send failure: %s" / "Recv failure: %s", curlx_strerror(...))`, Winsock words on Windows and `strerror`'s elsewhere (BL-1325's Context has the table).
- Use `CurlSocketErrorText.SendFailure` / `ReceiveFailure` where these texts are chosen, and keep the fallback texts for an `IOException` with no `SocketException` inside. Keep the scope the reset check has today: an `ftps` connection's failure that surfaces through TLS is not changed by this task unless it already took the reset text.
- Existing tests pinning `Connection was reset` hold on Windows only after this change: mark each `[OSCondition(OperatingSystems.Windows)]` and add a non-Windows twin (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`) asserting the exception's own message.
- BL-1333 also changes `FtpSession`; this task waits for it so the two do not collide.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Ftp.UnitTests` makes a control-connection send throw `IOException` wrapping `SocketException(SocketError.ConnectionAborted)` and asserts exit 55 with `Send failure: Connection was aborted`, Windows-only, plus its non-Windows twin with `Send failure: ` + the exception's own message.
- [x] A test makes the data connection's read fail the same way mid-download and asserts exit 56 with `Recv failure: Connection was aborted` (Windows) and its twin, with the bytes already received still counted.
- [x] Every existing reset test is split by platform; an `IOException` with no socket error still gives the fallback texts.
- [x] `FtpTransferMessages` no longer declares its own `Connection was reset` constant.
- [x] `dotnet build Curl.Protocol.Ftp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Ftp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports no failing member.

## Notes

- Delivered directly (one library and its tests; the `/feature` stages would add nothing to a two-method change). `FtpTransferMessages.SendFailed` and the new `ReceiveFailed(IOException)` call `CurlSocketErrorText.SendFailure`/`ReceiveFailure` only when the `IOException` directly wraps a `SocketException` - the scope the old reset check had - so a TLS-wrapped failure deeper down keeps `Failed sending data to the peer` / `Failure when receiving data from the peer` (pinned by `ExecuteAsync_DataReadFailsWithTheSocketErrorDeeperDown_KeepsTheFallbackText`). The old receive constant is renamed `ReceiveFailedFromPeer`; `SendConnectionReset` is gone.
- The test fake `ScriptedConnection` gained `ReadFailure`, the exception an exhausted read throws.
- Gates: build clean with -warnaserror; 604 passed, 4 skipped (off-Windows twins); Measure-CodeQuality -Library Curl.Protocol.Ftp.UnitLibrary reports 0 failing members.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. ftp send and data-receive socket failures now say curl's Send failure/Recv failure words from CurlSocketErrorText
