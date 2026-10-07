---
id: BL-1132
title: Write every IMAP response line to the -D file as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1129]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1132 — Write every IMAP response line to the -D file as curl does

## Goal

An `imap://` or `imaps://` transfer with `-D` writes every response line it reads (tagged and untagged), byte for byte with its line ending and in arrival order, to the `-D` stream BL-1129 adds, and not the literal message data that goes to the output; under `-i` alone nothing extra is written, as curl 8.21.0 does.

## Context

- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response '* OK hi\r\nA001 BAD no\r\n'`: `curl -s -D <file> imap://127.0.0.1:<port>/` exits 56 and the file holds exactly `* OK hi\r\nA001 BAD no\r\n`; `curl -s -i` writes nothing to stdout.
- curl 8.21.0 `lib/pingpong.c` `Curl_pp_readresp` (https://github.com/curl/curl/blob/curl-8_21_0/lib/pingpong.c) passes every response line to `Curl_client_write(data, CLIENTWRITE_INFO, ...)`, which the tool writes to the `-D` stream. The literal after a `{size}` is body data and goes to the output instead.
- Before pinning a whole session, measure with `Record-CurlExchange.ps1 -Imap` (with `-D` and with `-i`): a `LIST` of the mailbox root (where the untagged `* LIST` lines are also the output), a `FETCH` by UID (the `* 1 FETCH (... {N}` line, the literal, and the line closing it) and a `SEARCH`. Record in Notes exactly which lines reach the `-D` file in each, since the listing lines are written both as INFO and as body.
- Code: `Curl.Protocol.Imap.UnitLibrary/ImapControlChannel.cs` (`ReadLineAsync(int lineBytes)`, literal reads separate); write to the BL-1129 property (read its Notes for the final name) when it is not `null`. `Curl.Protocol.Imap.UnitLibrary` may also have BL-1119 waiting; they share `touches` and run one after another.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Imap.UnitTests` pin the measured exchange above, and the measured `LIST`, `FETCH` and `SEARCH` sessions, so the new stream's bytes match real curl's `-D` file byte for byte and `Output` is unchanged.
- [x] A test pins that with the new stream `null` and `HeaderOutput` set to `Output` (the `-i` shape) `Output` gains nothing.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-01, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Imap -CurlArgs '-s','-u','u:p','-D',<file>,<url>` with the default replies:
  - `LIST` (`imap://host/`): the file holds the greeting, `* CAPABILITY`, `A001 OK`, the `+ ` continuation of `AUTHENTICATE PLAIN`, `A002 OK`, both `* LIST` lines and `A003 OK LIST completed`. The `* LIST` lines are also stdout. The `LOGOUT` answer (`* BYE`, `A004 OK`) is not in the file.
  - `FETCH` (`INBOX;UID=1`): the same opening, every `SELECT` line, `* 1 FETCH (UID 1 BODY[] {100}`, `)` and `A004 OK FETCH completed`. The 100 literal bytes are stdout only.
  - `SEARCH` (`INBOX?NEW`): the same opening, the `SELECT` lines, `* SEARCH 1 2` (also stdout) and `A004 OK SEARCH completed`.
- How it works: the lines curl writes to `-D` are exactly the lines `-v` already shows as response headers. So `ImapControlChannel.ReportLineAsync` writes each reported line to `DumpHeaderOutput` too, and `StopReporting` (called before `LOGOUT`) stops both. `ImapProtocolHandler` passes `context.DumpHeaderOutput` to the channel. A listed literal is body and ends the listing, so its bytes reach the output only.
- Follow-up filed: BL-1138. A failed `-D` write throws `IOException`, which `ImapSession.RunAsync` does not catch yet. Real curl's exit 23 needs measuring first.
- Tests: 7 new in `ImapProtocolHandlerDumpHeaderTests`; 381 IMAP tests pass; 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. imap:// with -D writes every response line read, as curl 8.21.0 does; -i writes nothing extra
