---
id: BL-1120
title: Fail a POP3 response line holding a NUL byte with curl's exit 8
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1120 — Fail a POP3 response line holding a NUL byte with curl's exit 8

## Goal

A POP3 response line that contains a NUL byte ends the transfer with exit 8 `Nul byte in server response line`, without reporting that line under `-v` and without sending `QUIT`, as curl 8.21.0 does; the message body of a `RETR` or `LIST` is not checked.

## Context

- curl 8.21.0, `lib/pingpong.c` `Curl_pp_readresp` (line 298 at https://github.com/curl/curl/blob/curl-8_21_0/lib/pingpong.c): for every complete response line, before `Curl_debug` shows it, `if(memchr(line, 0, length)) { failf(data, "Nul byte in server response line"); return CURLE_WEIRD_SERVER_REPLY; }`. The greeting, the `+OK`/`-ERR` lines and the `CAPA` lines are pingpong lines and are checked; the body after a `RETR` or `LIST` `+OK` goes through `pop3_write` and is not.
- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -CurlArgs "-v,pop3://127.0.0.1:<port>/"`: greeting `+OK hel\x00lo\r\n` gives exit 8, stderr `curl: (8) Nul byte in server response line`, `-v` lines `* Nul byte in server response line` then `* closing connection #0`, and no `<` line for the greeting. Greeting `+OK hi\r\n` then `+OK ca\x00pa\r\n` (the reply to `CAPA`) gives `> CAPA`, `* Nul byte in server response line`, `* closing connection #0`, exit 8, and no `QUIT`.
- Curl today: `Curl.Protocol.Pop3.UnitLibrary/Pop3ControlChannel.cs` `ReadLineAsync` refuses only a 65536-byte line, with an `InvalidDataException` that `Pop3Session` (lines 108 and 302) turns into exit 100. The NUL refusal needs its own exception type or result so it maps to `CurlExitCode.WeirdServerReply`, with the message in `Pop3SessionMessages`. `ReadChunkAsync` (the body) stays unchecked.

## Acceptance criteria

- [ ] New tests in `Curl.Protocol.Pop3.UnitTests` pin the two measured cases: exit 8 `Nul byte in server response line`, the `-v` events as measured (none for the NUL line), and no `QUIT` written to the scripted connection.
- [ ] A test pins that a NUL inside a `CAPA` list line fails the same way, and one that a NUL inside a `RETR` body is written to the output unchanged and the transfer succeeds.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
