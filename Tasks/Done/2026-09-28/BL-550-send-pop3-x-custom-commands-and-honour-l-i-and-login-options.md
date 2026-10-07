---
id: BL-550
title: Send POP3 -X custom commands and honour -l, -I and --login-options
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-549]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-550 — Send POP3 -X custom commands and honour -l, -I and --login-options

## Goal

`-X <command>` on a POP3 URL (`-X DELE`, `-X UIDL`, `-X TOP` and so on) is sent as curl 8.21.0 sends it, with a multi-line or single-line answer written as curl writes it, and `-l`/`--list-only`, `-I` and the remaining `--login-options` behave as curl's do for POP3.

## Context

- Conformance audit 2026-09-28, rows 23 and 34. `CurlManual.txt` (`--request`, `--list-only`) and https://everything.curl.dev/usingcurl/pop3 describe the behaviour; confirm each by measurement.
- Measure with `Record-CurlExchange.ps1 -Pop3`: `-X DELE pop3://h/1`, `-X UIDL pop3://h/`, `-X "TOP 1 0" pop3://h/`, `-l pop3://h/1`, `-I pop3://h/1`; record request lines and stdout bytes.

## Acceptance criteria

- [x] Measured first as above; request lines, stdout bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Pop3.UnitTests` pin client bytes, output bytes and outcome for each case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 on curl 8.21.0 (Schannel build) with
`Record-CurlExchange.ps1 -Pop3`, curl run `-sS -u u:p <args>`. Every case logged in with
`CAPA`, `AUTH PLAIN`, `AHUAcA==` first; below is what followed. stderr empty and exit 0
unless stated.

| Args | Sent after login | Server | stdout |
| --- | --- | --- | --- |
| `-X DELE …/1` | `DELE 1`, `QUIT` | `+OK Message deleted` | empty |
| `-X dele …/1` | `dele 1`, `QUIT` | same | empty |
| `-X "DELE " …/1` | `DELE  1` (two spaces), `QUIT` | same | empty |
| `-X DELE …/` | `DELE`, `QUIT` | same | empty |
| `-X NOOP` / `RSET` / `STAT` / `UTF8` / `APOP` / `PASS` / `"USER x"` | the command, `QUIT` | `+OK …` | empty |
| `-X UIDL …/` | `UIDL`, `QUIT` | multi-line `1 uid-1`, `2 uid-2` | `1 uid-1\r\n2 uid-2\r\n` (18 bytes) |
| `-X "TOP 1 0" …/` and `-X "TOP%201 0"` | `TOP 1 0`, `QUIT` | headers, blank line, `.` | the 74 header bytes incl. blank line |
| `-X FOO` (reply overridden to `+OK`, `bar`, `.`) | `FOO`, `QUIT` | multi-line | `bar\r\n` |
| `-X UIDL …/1`, `-X LIST …/1`, `-X DELEX …/1` (`+OK`), `-X MSG`/`XTND`/`TOP` alone (`+OK`) | the command (id appended), no `QUIT` | single `+OK` line | empty; curl waits for a body until the server hangs up, exit 0 |
| `-X "LIST 1" …/`, `-X "uidl 1" …/` | the command, `QUIT` | single line | empty |
| `-l …/1` | `LIST 1`, `QUIT` | `+OK 1 133` | empty |
| `-l …/` | `LIST`, `QUIT` | listing | `1 133\r\n2 133\r\n` |
| `-l -X "TOP 1 0" …/` | `TOP 1 0`, `QUIT` | headers | the headers |
| `-l -X DELE …/1`, `-l -X UIDL …/1` | the command with id, `QUIT` | single line | empty |
| `-l -X RETR …/1` | `RETR 1`, `QUIT` right after the status line | whole message | empty |
| `-I …/1`, `-I …/`, `-I -X UIDL …/` | exactly as without `-I` | | as without `-I` |
| `-X DELE …/9` (`-ERR no such message`), `-X UIDL` (`-ERR nope`), `-l …/1` (`-ERR nope`), `-X DELEX …/1` (`-ERR`) | the command, `QUIT` | `-ERR …` | empty; exit 8 `curl: (8) Weird server reply` |
| `-X "DELE%0A1"` | only `QUIT` | | exit 3 `curl: (3) URL using bad/illegal format or missing URL` |
| `-X ""` | nothing; the command line refuses it | | exit 2 `option -X: blank argument where content is expected` (the CLI's, not this library's) |

The results fit curl's `pop3_is_multiline` table exactly: a command's answer has a body
by its name alone (APOP AUTH DELE NOOP PASS QUIT RSET STAT STLS USER UTF8 never; CAPA MSG
RETR TOP XTND always; LIST and UIDL only without arguments), matched case-insensitively
when the name ends the command or is followed by a space, anything else assumed to have
one; the URL's id is not part of the check. `-l` on a URL naming a message suppresses the
body whatever the command. `-I` has no effect on POP3.

Implemented as `Pop3Command` (chooses the line and whether a body is written), used by
`Pop3Session.TransferAsync`; the `-X` command is percent-decoded by the same routine as
the message id (`Pop3MessageId.Decode`). No design choice was needed beyond matching
these measurements, so no ADR.

`--login-options`: the only option curl 8.21.0's `pop3_parse_url_options` reads is
`AUTH=`, which BL-548 already implemented and pinned in `Pop3LoginOptionsTests`; nothing
remained for this task.

Left as measured-but-unpinned: `-X QUIT` (curl's second `QUIT` goes to a socket the
server already closed) and `-X STLS` (the recorder switched to TLS), neither observable
in the recorder's transcript. Tests use no credential so no login precedes the command.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. POP3 sends -X commands as curl 8.21.0 does, writing a body only where curl's single/multi-line table says so; -l on a message sends LIST <id> and writes nothing; -I changes nothing
