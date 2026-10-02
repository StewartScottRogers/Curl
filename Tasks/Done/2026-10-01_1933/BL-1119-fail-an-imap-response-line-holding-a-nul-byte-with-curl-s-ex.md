---
id: BL-1119
title: Fail an IMAP response line holding a NUL byte with curl's exit 8
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1119 — Fail an IMAP response line holding a NUL byte with curl's exit 8

## Goal

An IMAP response line that contains a NUL byte ends the transfer with exit 8 `Nul byte in server response line`, without reporting that line under `-v` and without sending `LOGOUT`, as curl 8.21.0 does; literal (`{N}`) message data is not checked.

## Context

- curl 8.21.0, `lib/pingpong.c` `Curl_pp_readresp` (line 298 at https://github.com/curl/curl/blob/curl-8_21_0/lib/pingpong.c): for every complete response line, before `Curl_debug` shows it, `if(memchr(line, 0, length)) { failf(data, "Nul byte in server response line"); return CURLE_WEIRD_SERVER_REPLY; }`. Untagged lines (`* CAPABILITY`, `* LIST`, `* SEARCH`, the `* N FETCH ... {size}` line) are pingpong lines and are checked; the literal bytes that follow a `{size}` are body data handed on by `imap_state_fetch_resp` and are not.
- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -CurlArgs "-v,imap://127.0.0.1:<port>/"`: greeting `* OK hel\x00lo\r\n` gives exit 8, stderr `curl: (8) Nul byte in server response line`, `-v` lines `* Nul byte in server response line` then `* closing connection #0`, and no `<` line for the greeting. Greeting `* OK hi\r\n` then `A001 OK ca\x00pa\r\n` gives `< * OK hi`, `> A001 CAPABILITY`, `* Nul byte in server response line`, `* closing connection #0`, exit 8, and no `LOGOUT`.
- Curl today: `Curl.Protocol.Imap.UnitLibrary/ImapControlChannel.cs` `ReadLineAsync(int lineBytes)` refuses only a 65536-byte line, with an `InvalidDataException` that `ImapSession` (lines 120 and 735) turns into exit 100. The NUL refusal needs its own exception type or result so it maps to `CurlExitCode.WeirdServerReply`, with the message in `ImapSessionMessages`. Leave the literal reads (`LiteralReadSize`) unchecked.

## Acceptance criteria

- [x] New tests in `Curl.Protocol.Imap.UnitTests` pin the two measured cases: exit 8 `Nul byte in server response line`, the `-v` events as measured (none for the NUL line), and no `LOGOUT` written to the scripted connection.
- [x] A test pins that a NUL inside an untagged `* LIST` line fails the same way, and one that a NUL inside a FETCH literal is written to the output unchanged and the transfer succeeds.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- The NUL refusal (`ImapWeirdResponseException` with `ImapSessionMessages.NulByteInLine`, exit 8, no LOGOUT) already existed from BL-553, but the line was reported to `-v` before the check. `ImapControlChannel.ReadLineAsync` now checks before `ReportLine`. New tests: `ImapProtocolHandlerNulByteTests` (greeting, CAPABILITY completion, `* LIST` line, FETCH literal).
- The `* LIST` case pins the result, the commands sent and that the line is not reported; its closing `-v` line was not measured, so it is not pinned.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. An IMAP response line holding a NUL byte fails with exit 8 before -v reports it, with no LOGOUT; FETCH literals pass unchecked
