---
id: BL-1134
title: Write every SMTP reply line to the -D file as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1129]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1134 — Write every SMTP reply line to the -D file as curl does

## Goal

An `smtp://` or `smtps://` transfer with `-D` writes every reply line it reads, continuation lines included, byte for byte with its line ending and in arrival order, to the `-D` stream BL-1129 adds; under `-i` alone nothing extra is written, as curl 8.21.0 does.

## Context

- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response '220 hi\r\n554 no\r\n'`: `curl -s -D <file> smtp://127.0.0.1:<port>/` exits 56 and the file holds exactly `220 hi\r\n554 no\r\n`; `curl -s -i` writes nothing to stdout.
- curl 8.21.0 `lib/pingpong.c` `Curl_pp_readresp` (https://github.com/curl/curl/blob/curl-8_21_0/lib/pingpong.c) passes every reply line to `Curl_client_write(data, CLIENTWRITE_INFO, ...)`, which the tool writes to the `-D` stream. Curl already writes a command's final reply line to the output for some transfers (`SmtpReply.FinalLine`, BL-543); that stays as it is.
- Before pinning a whole session, measure with `Record-CurlExchange.ps1 -Smtp` (with `-D` and with `-i`) a send (`--mail-from`, `--mail-rcpt`, `-T`) and a `VRFY`/`-X` command transfer, and record in Notes exactly what the `-D` file holds, the multi-line `EHLO` reply included.
- Code: `Curl.Protocol.Smtp.UnitLibrary/SmtpControlChannel.cs` `ReadReplyAsync` / `ReadLineAsync`; write to the BL-1129 property (read its Notes for the final name) when it is not `null`. `Curl.Protocol.Smtp.UnitLibrary` may also have BL-1121 waiting; they share `touches` and run one after another.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Smtp.UnitTests` pin the measured exchange above, and the measured send and command sessions, so the new stream's bytes match real curl's `-D` file byte for byte and `Output` is unchanged.
- [x] A test pins that with the new stream `null` and `HeaderOutput` set to `Output` (the `-i` shape) `Output` gains nothing beyond what it gets today.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-01 on curl 8.21.0 (Schannel) with `Record-CurlExchange.ps1 -Smtp`, default replies:
  - Send (`-s -D f --mail-from a@b --mail-rcpt c@d -T mail.txt`): `f` holds `220 localhost ESMTP`, the six `EHLO` lines (`250-localhost` … `250 SMTPUTF8`), `250 OK`, `250 OK`, `354 End data with <CR><LF>.<CR><LF>`, `250 OK message accepted`, each with CRLF; not `QUIT`'s `221 Bye`. Stdout empty, exit 0.
  - `-X 'VRFY c@d'` answered `250-first\r\n250 second`: `f` holds greeting, `EHLO` reply and both lines; stdout `250-first\r\n250 second\r\n` as before.
  - `HELP` session with `-i` alone: stdout only the `214` line. With `-D -` (`-X NOOP`) stdout holds every line, then `250 OK` again as the body: the dump comes first.
  - A skipped line (`GREETING=junk line\r\n220 hi`) is in the `-D` file too, as `Curl_pp_readresp` writes every line.
- Implementation: `SmtpControlChannel` takes `context.DumpHeaderOutput` and writes each line read in `ReadLineAsync` right after `-v` reports it (a NUL-byte line is refused before either); `QuitAsync` stops dumping as it stops reporting.
- Tests: `SmtpProtocolHandlerDumpHeaderTests` (5). SMTP 268 tests green; fast suite green; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. smtp and smtps transfers write every reply line before QUIT to the -D stream as curl 8.21.0 does
