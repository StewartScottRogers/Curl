---
id: BL-549
title: List and retrieve POP3 messages with LIST and RETR
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-547]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-549 — List and retrieve POP3 messages with LIST and RETR

## Goal

`pop3://host/` sends `LIST` and writes the listing, and `pop3://host/<n>` sends `RETR <n>` and writes the message with dot-unstuffing and without the terminating `.` line, exactly as curl 8.21.0 writes them, with `-ERR` answers mapped to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 34. URL model: BL-533's ADR.
- Measure with `Record-CurlExchange.ps1 -Pop3` (`-Pop3Message` with a stuffed `..` line and CRLF endings): `pop3://h/`, `pop3://h/1`, `pop3://h/9` answered `-ERR no such message`, and a message split so the terminator straddles two reads (the handler test covers the split; measurement covers the output bytes). Record stdout bytes exactly.
- Download progress and `%{size_download}` follow what curl counts (measure with `-w '%{size_download}'`).

## Acceptance criteria

- [x] Measured first as above; request lines, stdout bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Pop3.UnitTests` pin output bytes and outcome for each case, and the terminator split across reads.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (curl 8.21.0 Schannel, 2026-09-28, `Record-CurlExchange.ps1 -Pop3`)

curl ran `-sS -w '[%{size_download}]' pop3://127.0.0.1:18110/<path>`; stdout below is
without the `[n]` suffix, and n is the byte count. Without `-u` no login is sent. The
recorder sends each reply in one write, so the `LIST`/`RETR` answer and the `QUIT` reply
arrive in separate reads. `M` = `-Pop3Message 'Subject: a\r\n\r\nline one\r\n..two dots\r\n.one dot\r\nlast\r\n'` (52 bytes).

| Case | Client lines | Exit | stdout | stderr |
| --- | --- | --- | --- | --- |
| `/`, message `M` | `CAPA`, `LIST`, `QUIT` | 0 | `1 52\r\n2 52\r\n` (12) | - |
| `/1`, message `M` (sent stuffed `...two dots`, `..one dot`) | `CAPA`, `RETR 1`, `QUIT` | 0 | `M` exactly (52) | - |
| `/9`, `RETR=-ERR no such message` | `CAPA`, `RETR 9`, `QUIT` | 8 | empty (0) | `curl: (8) Weird server reply` |
| `/`, `LIST=-ERR nope` | `CAPA`, `LIST`, `QUIT` | 8 | empty | `curl: (8) Weird server reply` |
| `/1`, `RETR=+ here\r\nx\r\n.` | `CAPA`, `RETR 1`, `QUIT` | 8 | empty | `curl: (8) Weird server reply` |
| `/1`, `RETR=junk\r\n+OK\r\nabc\r\n.` | as `/1` | 0 | `abc\r\n` (5) | - |
| `/1`, empty message | as `/1` | 0 | `\r\n` (2) | - |
| `/1`, message `a\r\r\nb\r\n\r\n.\r\n` | as `/1` | 0 | `a\r\r\nb\r\n\r\n.\r\n` (12) | - |
| `/1`, message `.first\r\nb\r\n` | as `/1` | 0 | `.first\r\nb\r\n` (11) | - |
| `/1`, `RETR=+OK\r\nabc\r\n.x\r\n.` | as `/1` | 0 | `abc\r\n.x\r\n` (9) | - |
| `/1`, `RETR=+OK\r\nabc`, server hangs up | `CAPA`, `RETR 1` | 0 | `abc` (3) | - |
| `/1`, `RETR=+OK\r\nabc\r\n.\r\nextra` (one write), hang-up | `CAPA`, `RETR 1` | 0 | `abc\r\n.\r\nextra` (13) | - |
| `/1`, `RETR=+OK\nabc\n.\n`, hang-up | `CAPA`, `RETR 1` | 0 | `abc\n.\n` (6) | - |
| `/1`, `RETR=CLOSE`; `/`, `LIST=CLOSE` | `CAPA`, the command | 56 | empty | `curl: (56) response reading failed (errno: 0)` |
| `/1`, `QUIT=CLOSE` | as `/1` | 0 | the message | - |
| `/a%20b`, `/1/2`, `/%zz%4`, `/%C3%A9`, `/%7f` | `RETR a b`, `RETR 1/2`, `RETR %zz%4`, `RETR` + bytes C3 A9, `RETR` + 0x7F | 0 | the message | - |
| `/%0d`, `/%1f` | `CAPA`, `QUIT` | 3 | empty | `curl: (3) URL using bad/illegal format or missing URL` |

### Plan and decisions

- Every rule is the measured behaviour above, so no ADR was needed. The body decoder
  (`Pop3BodyDecoder`) is a port of curl's `pop3_write` byte matcher for `CRLF.CRLF`,
  which explains every oddity measured: only CRLF counts, a `..` line loses one dot, a
  `.x` line is written as it came, an empty message writes CRLF, bytes that may begin the
  terminator are held back (and lost if the server hangs up), and the terminator ends the
  body only when it ends a received chunk (the `extra` case). The chunk left over after
  the status line is decoded first, as curl decodes its overflow.
- `Pop3MessageId` decodes the path after the first `/` byte-wise (invalid escapes kept,
  bytes sent as Latin-1) and refuses a byte below 0x20 with exit 3, after `CAPA` and
  before `QUIT`, as measured.
- A status other than `+OK` (capitals, as for the greeting) is exit 8
  `Weird server reply` and `QUIT` is still sent. The server closing (or a read failing)
  mid-body is success with what was written and no `QUIT`.
- The success's byte count is what was written, matching `%{size_download}`; progress
  reports `ReportTransferStarted` before the body and `ReportDownloaded` per chunk.
- Test scripts deliver the `LIST`/`RETR` answer and the `QUIT` reply as separate reads, as
  the recorder does; one test pins that a `QUIT` reply in the same read as the terminator
  keeps the body open. BL-547's session tests now include the `LIST` exchange.
- `-l`, `-I` and `-X` stay BL-550's; login stays BL-548's.
- Pipeline stages ran in-session (plan, tests, implementation, verify, quality gate).

### Results

- 87 tests in `Curl.Protocol.Pop3.UnitTests` (28 new), all fake connections.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary`: 100% line, 100%
  branch, 43 members, 0 failing, worst CRAP 10.
- `dotnet build Curl.slnx -warnaserror` clean; every fast test project passed.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. pop3:// sends LIST and pop3://host/<n> sends RETR <n>, writing curl 8.21.0's bytes (dot-unstuffed, terminator dropped) with -ERR as exit 8 and a bad id as exit 3
