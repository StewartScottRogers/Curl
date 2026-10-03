---
id: BL-1339
title: Word every gopher socket send and receive failure with the shared CurlSocketErrorText table
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1325]
touches: [Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests]
requirement: FR-085
created: 2026-10-03
completed:
---
# BL-1339 — Word every gopher socket send and receive failure with the shared CurlSocketErrorText table

## Goal

A `gopher://` selector send or reply read that fails with any socket error ends with curl 8.21.0's `Send failure: <words>` (exit 55) / `Recv failure: <words>` (exit 56), the words from `CurlSocketErrorText` (BL-1325) for the platform, instead of the fallback texts for everything but a send reset.

## Context

- Today `Curl.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs` `SendFailure` (line 233) gives `GopherTransferMessages.SendConnectionReset` (line 29, the Windows words on every platform) for `SocketError.ConnectionReset` and `SendFailed` (line 35) otherwise; a failed read (line 327) is always `ReceiveFailed` (line 23) unless it is a `MissingCloseNotifyException`, so even a reset read gets `Failure when receiving data from the peer`.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/cf-socket.c` lines 1562 and 1618: every socket error but `EAGAIN` is `failf(data, "Send failure: %s" / "Recv failure: %s", curlx_strerror(...))`, Winsock words on Windows and `strerror`'s elsewhere (BL-1325's Context has the table).
- Use `CurlSocketErrorText.SendFailure` / `ReceiveFailure`, keeping the fallback texts for an `IOException` with no `SocketException` and the missing-`close_notify` text as it is. The fallback check at line 192 (texts without a `-v` line of their own) keeps meaning only the two fallback texts.
- Existing tests pinning `Connection was reset` hold on Windows only after this change: mark each `[OSCondition(OperatingSystems.Windows)]` and add a non-Windows twin (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`) asserting the exception's own message.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Gopher.UnitTests` makes the reply read throw `IOException` wrapping `SocketException(SocketError.ConnectionAborted)` and asserts exit 56 with `Recv failure: Connection was aborted` (Windows-only) plus a non-Windows twin with `Recv failure: ` + the exception's own message, the bytes written before the failure still counted.
- [ ] A test pins the same for a reset read (`Recv failure: Connection was reset` on Windows), and for an aborted send (exit 55, `Send failure: Connection was aborted`).
- [ ] Every existing reset test is split by platform; an `IOException` with no socket error still gives the fallback texts, and a missing `close_notify` its own text.
- [ ] `dotnet build Curl.Protocol.Gopher.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Gopher.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Gopher.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
