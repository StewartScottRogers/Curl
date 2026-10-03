---
id: BL-1345
title: Word every smtp socket send failure with the shared CurlSocketErrorText table
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1325]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: FR-085
created: 2026-10-03
completed:
---
# BL-1345 — Word every smtp socket send failure with the shared CurlSocketErrorText table

## Goal

An `smtp://` command or message send that fails with any socket error ends with curl 8.21.0's `Send failure: <words>` (exit 55), the words from `CurlSocketErrorText` (BL-1325) for the platform, instead of `Failed sending data to the peer` for every error but a reset.

## Context

- Today `Curl.Protocol.Smtp.UnitLibrary/SmtpSendFailedException.cs` (line 16) recognises only `SocketError.ConnectionReset` and picks `SmtpSessionMessages.SendConnectionReset` (line 15, `Send failure: Connection was reset` on every platform) or `SendFailed` (line 21). BL-1243 added this.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/cf-socket.c` line 1562: every socket send error but `EAGAIN` is `failf(data, "Send failure: %s", curlx_strerror(...))`, Winsock words on Windows and `strerror`'s elsewhere (BL-1325's Context has the table).
- Use `CurlSocketErrorText.SendFailure`, keeping `SendFailed` for an `IOException` with no `SocketException`. Reply reads are out of scope: `response reading failed (errno: 0)` (`SmtpSessionMessages.ResponseReadingFailed`) is pingpong's text for a closed connection.
- Existing tests pinning `Connection was reset` hold on Windows only after this change: mark each `[OSCondition(OperatingSystems.Windows)]` and add a non-Windows twin (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`) asserting the exception's own message.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Smtp.UnitTests` makes a command send throw `IOException` wrapping `SocketException(SocketError.ConnectionAborted)` and asserts exit 55 (`CurlExitCode.SendError`) with `Send failure: Connection was aborted` (Windows-only), plus a non-Windows twin with `Send failure: ` + the exception's own message.
- [ ] A test pins the same for a send that fails while the message body is being sent after `DATA`.
- [ ] Every existing reset test is split by platform; an `IOException` with no socket error still gives `Failed sending data to the peer`; `SmtpSessionMessages` no longer declares its own `Connection was reset` constant.
- [ ] `dotnet build Curl.Protocol.Smtp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Smtp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
