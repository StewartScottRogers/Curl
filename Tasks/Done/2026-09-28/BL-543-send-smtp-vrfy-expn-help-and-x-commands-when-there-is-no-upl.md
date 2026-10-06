---
id: BL-543
title: Send SMTP VRFY, EXPN, HELP and -X commands when there is no upload
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-540]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-543 — Send SMTP VRFY, EXPN, HELP and -X commands when there is no upload

## Goal

Without `-T`, the SMTP handler does what curl 8.21.0 does: `VRFY` for each `--mail-rcpt`, `EXPN` with `-X EXPN`, `HELP` when there is no recipient, and any other `-X <command>` sent as given, writing the server's reply text to the output.

## Context

- Conformance audit 2026-09-28, row 34. The mapping of `-X` and `--mail-rcpt` to commands is in `CurlManual.txt` (`--request`, `--mail-rcpt`) and https://everything.curl.dev/usingcurl/smtp; confirm each by measurement.
- Measure with `Record-CurlExchange.ps1 -Smtp`: `smtp://h/ --mail-rcpt a@b`, `-X EXPN --mail-rcpt list`, `smtp://h/` alone, `-X NOOP`, and a `VRFY` answered `550`; record stdout, which carries the reply text.

## Acceptance criteria

- [x] Measured first as above; request lines, stdout bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Smtp.UnitTests` pin client bytes, output bytes and outcome for each case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 on curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1 -Port 18125 -Smtp`,
`-sS [-w '[%{response_code} %{size_download} %{size_upload}]'] ... smtp://127.0.0.1:18125/`. The recorder's
EHLO advertises SMTPUTF8. Request lines after EHLO; stdout bytes; stderr; exit:

| Case | Sent | stdout | stderr | exit |
| --- | --- | --- | --- | --- |
| `--mail-rcpt a@b` | `VRFY a@b`, `QUIT` | `250 Recorder <recorder@localhost>\r\n` | none | 0 |
| `--mail-rcpt a@b --mail-rcpt c@d` | `VRFY a@b`, `VRFY c@d`, `QUIT` | both replies | none | 0 |
| `-X EXPN --mail-rcpt list`, EXPN=`250-Alice <a@b>\r\n250 Bob <c@d>` | `EXPN list SMTPUTF8`, `QUIT` | `250-Alice <a@b>\r\n250 Bob <c@d>\r\n` | none | 0 |
| `smtp://h/` alone, HELP=`214-Commands:\r\n214 HELO EHLO MAIL RCPT DATA` | `HELP`, `QUIT` | both lines, CRLF each | none | 0 |
| `-X NOOP` | `NOOP`, `QUIT` | `250 OK\r\n` | none | 0 |
| `--mail-rcpt x@y`, VRFY=`550 no such user` | `VRFY x@y`, `QUIT` | empty | `curl: (8) Command failed: 550` | 8 |
| VRFY=`553 ambiguous` | `VRFY a@b` | `553 ambiguous\r\n` `[553 15 0]` | none | 0 |
| HELP=`550-first\r\n550 second` | `HELP`, `QUIT` | `550-first\r\n` `[550 11 0]` | `curl: (8) Command failed: 550` | 8 |
| `-X VRFY --mail-rcpt '<a@b>'` | `VRFY <a@b>` | reply `[250 35 0]` | none | 0 |
| `-X NOOP --mail-rcpt a@b --mail-rcpt c@d` | `NOOP a@b`, `NOOP c@d` | `250 OK\r\n250 OK\r\n` `[250 16 0]` | none | 0 |
| `-X EXPN --mail-rcpt list`, EHLO=`502 no` | `HELO`, `EXPN list` | reply `[250 35 0]` | none | 0 |
| `--mail-rcpt '<a@b>' --mail-rcpt local` | `VRFY a@b`, `VRFY local` | both `[250 70 0]` | none | 0 |
| `-T mail.txt`, no `--mail-rcpt` | `HELP` | reply `[214 74 0]` | none | 0 |
| two rcpts, VRFY=`550 no` | `VRFY a@b`, `QUIT` | `[550 0 0]` | `curl: (8) Command failed: 550` | 8 |
| `-I -X NOOP` | `NOOP` | `[250 0 0]` only | none | 0 |
| `-X 'FOO bar'` | `FOO bar`, `QUIT` | `[502 0 0]` | `curl: (8) Command failed: 502` | 8 |
| `-X NOOP`, NOOP=`250-a\n250 b` | `NOOP` | `250-a\n250 b\r\n` `[250 13 0]` | none | 0 |
| `--mail-rcpt jörg@example.com` | `VRFY j\xF6rg@example.com SMTPUTF8` (ANSI byte) | reply | none | 0 |
| `--mail-rcpt a@bücher.example` | `VRFY a@xn--bcher-kva.example SMTPUTF8` | reply | none | 0 |
| `-X expn --mail-rcpt list` | `expn list` | reply | none | 0 |
| `-X ''` | nothing: `curl: option -X: blank argument where content is expected` | | | 2 |

Plan and decisions (ADR-0135, decided under Stewart's delegation): new `SmtpCommandTransfer` sends
VRFY / `-X` per recipient or HELP / `-X` alone; continuation lines are written as they arrive
(`SmtpControlChannel.ReadReplyAsync(Func<string, ValueTask>)`), the final line
(`SmtpReply.FinalLine`, as received) only when 2xx or 553-for-a-recipient; refusal is exit 8
`Command failed: <code>` and still sends QUIT; `BytesTransferred` = bytes written, `Report.ResponseCode`
= last command reply. IDNA via the BCL `IdnMapping`. Commands stay Latin-1 like RCPT; UTF-8 on
Linux/macOS and RCPT's A-label conversion filed as BL-776.

Existing session, auth and EHLO-domain tests now expect the `HELP` a session without a message
sends (constants `HelpAndQuit`, `HelpReplyAndBye`, `SmtpRun.HelpAnswered`); the three upload tests
that pinned "no mail, no command" now pin HELP or VRFY.

Added `Documentation/Planning/Decisions` (ADR-0135 and its README row) to `touches`: the ADR is
required by the delegation rule, and no task in Doing names that folder.

Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Smtp 151/151);
`Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary`: line 100, branch 100, 82 members,
0 failing, worst CRAP 10 (`SmtpSession.RunAsync` split to stay at complexity 10 or less).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. SMTP without a message sends VRFY per --mail-rcpt, -X per recipient or alone, or HELP, and writes each reply line to the output as curl 8.21.0 does (ADR-0135)
