---
id: BL-1121
title: Fail an SMTP reply line holding a NUL byte with curl's exit 8
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1121 — Fail an SMTP reply line holding a NUL byte with curl's exit 8

## Goal

An SMTP reply line that contains a NUL byte ends the transfer with exit 8 `Nul byte in server response line`, without reporting that line under `-v` and without sending `QUIT`, as curl 8.21.0 does.

## Context

- curl 8.21.0, `lib/pingpong.c` `Curl_pp_readresp` (line 298 at https://github.com/curl/curl/blob/curl-8_21_0/lib/pingpong.c): for every complete reply line, continuation lines (`250-...`) included and before `Curl_debug` shows it, `if(memchr(line, 0, length)) { failf(data, "Nul byte in server response line"); return CURLE_WEIRD_SERVER_REPLY; }`.
- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -CurlArgs "-v,smtp://127.0.0.1:<port>/"`: greeting `220 hel\x00lo\r\n` gives exit 8, stderr `curl: (8) Nul byte in server response line`, `-v` lines `* Nul byte in server response line` then `* closing connection #0`, and no `<` line for the greeting. Greeting `220 hi\r\n` then `250 eh\x00lo\r\n` (the reply to `EHLO`) gives `> EHLO <host>`, `* Nul byte in server response line`, `* closing connection #0`, exit 8, and no `QUIT`.
- Curl today: `Curl.Protocol.Smtp.UnitLibrary/SmtpControlChannel.cs` `ReadLineAsync` refuses only a 65536-byte line, with an `InvalidDataException` that `SmtpSession` (line 84), `SmtpMailTransaction` (line 71) and `SmtpCommandTransfer` (line 62) turn into exit 100 and `SmtpControlChannel.QuitAsync` (line 181) ignores. The NUL refusal needs its own exception type or result so it maps to `CurlExitCode.WeirdServerReply` in each of those places, with the message in `SmtpSessionMessages`, and no `QUIT` is sent after it.

## Acceptance criteria

- [ ] New tests in `Curl.Protocol.Smtp.UnitTests` pin the two measured cases: exit 8 `Nul byte in server response line`, the `-v` events as measured (none for the NUL line), and no `QUIT` written to the scripted connection.
- [ ] Tests pin the same failure for a NUL in a `250-` continuation line of the `EHLO` reply and in the reply to `MAIL FROM` during a send.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
