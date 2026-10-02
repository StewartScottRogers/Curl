---
id: BL-1117
title: Fail an FTP reply line holding a NUL byte with curl's exit 8
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1117 — Fail an FTP reply line holding a NUL byte with curl's exit 8

## Goal

An FTP control-connection reply line that contains a NUL byte ends the transfer with exit 8 `Nul byte in server response line`, without reporting that line under `-v` and without sending `QUIT`, as curl 8.21.0 does; today the byte is read as part of the reply text.

## Context

- curl 8.21.0, `lib/pingpong.c` `Curl_pp_readresp` (line 298 at https://github.com/curl/curl/blob/curl-8_21_0/lib/pingpong.c): for every complete line, before `Curl_debug` shows it, `if(memchr(line, 0, length)) { failf(data, "Nul byte in server response line"); return CURLE_WEIRD_SERVER_REPLY; }`. It applies to every reply line, continuation lines included, and not to the data connection.
- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1`: a greeting `220 hel\x00lo\r\n` gives exit 8, stderr `curl: (8) Nul byte in server response line`, and `-v` lines `* Nul byte in server response line` then `* closing connection #0`, with no `<` line for the greeting. Greeting `220 hi\r\n` then `331 pa\x00ss\r\n` gives `< 220 hi`, `> USER anonymous`, `* Nul byte in server response line`, `* closing connection #0`, exit 8, and no `> QUIT`.
- Curl today: `Curl.Protocol.Ftp.UnitLibrary/FtpControlChannel.cs` `ReadLineAsync` / `ReadReplyAsync` refuse only a line of 65536 bytes (an `InvalidDataException` that `FtpSession.ReadReplyAsync` turns into exit 100). The NUL refusal needs its own exception type or result, since `InvalidDataException` already means exit 100 in `FtpSession.ReadReplyAsync` and `SendIgnoringReplyAsync`; map it to `CurlExitCode.WeirdServerReply` with the message in `FtpTransferMessages`, ending the transfer without `QUIT` (as the 421 path does).
- Decide (and write in Notes) what a NUL in the reply to `QUIT` or `ABOR` does, where `SendIgnoringReplyAsync` already ignores an oversized reply; curl's `ftp_quit` treats any read error there as the end of the connection.

## Acceptance criteria

- [x] New tests in `Curl.Protocol.Ftp.UnitTests` pin the two measured cases above: exit 8 with `Nul byte in server response line`, the `-v` events as measured (no event for the NUL line), and no `QUIT` written to the scripted connection.
- [x] A test pins that a NUL in a `220-` continuation line fails the same way, and one that a NUL in data-connection bytes of a download is written to the output unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- `FtpControlChannel.ReadLineAsync` throws the new `FtpReplyNulByteException` for a complete line holding a NUL, before reporting it; `FtpSession.ReadReplyAsync` maps it to exit 8 `Nul byte in server response line` with no `QUIT` (the throw path, as 421). Its own type because `InvalidDataException` is sealed and already means exit 100.
- Decided: a NUL in the reply to `QUIT` or `ABOR` is ignored like an oversized one (`SendIgnoringReplyAsync`), since curl's `ftp_quit` treats any read error there as the end of the connection. Pinned by `ExecuteAsync_NulByteInTheReplyToQuit_StillSucceeds`.
- The `* Nul byte...` and `* closing connection #0` `-v` lines are the console's generic failure lines; the handler reports nothing for the NUL line (tests pin the transcript).

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. An FTP reply line holding a NUL byte fails with exit 8, unreported and without QUIT, as curl 8.21.0
