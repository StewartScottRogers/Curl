---
id: BL-542
title: Send an SMTP upload with MAIL FROM, RCPT TO and DATA
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-540]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-542 — Send an SMTP upload with MAIL FROM, RCPT TO and DATA

## Goal

With `-T <file>` (or `-T -`), the SMTP handler sends `MAIL FROM:<...>`, one `RCPT TO:<...>` per `--mail-rcpt`, `DATA`, the message with dot-stuffing and line endings as curl 8.21.0 sends them, and the terminating `CRLF.CRLF`, and maps a refused command to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, rows 31 and 34. Options arrive on the context (BL-534); `--mail-auth`, `--mail-rcpt-allowfails` and SIZE/SMTPUTF8 are BL-544.
- Measure with `Record-CurlExchange.ps1 -Smtp`: a body with a line starting `.`, a body with bare LF endings, a body not ending in a newline, `-T -` from `-StandardInput`, two `--mail-rcpt`, `MAIL` answered `550`, the only `RCPT` answered `550`, `DATA` answered `554`, and the final `250` replaced by `552`; record `request.bin` exactly.
- `%{size_upload}` and the progress reports follow what curl counts (measure with `-w '%{size_upload}'`).

## Acceptance criteria

- [x] Measured first as above; request bytes, stdout, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Smtp.UnitTests` pin the client bytes byte for byte for each body shape, and the exit code and message for each refusal.
- [x] Upload progress and `size_upload` match the measured values.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (curl 8.21.0 mingw Schannel, 2026-09-28)

Command: `Record-CurlExchange.ps1 -Smtp -CurlArgs -sS -w '[%{size_upload}|%{response_code}|%{size_download}]' --mail-from a@b --mail-rcpt c@d -T <body> smtp://127.0.0.1:<port>/dom`.
The recorder advertises `SIZE`, so every `MAIL` carried ` SIZE=<file length>` (BL-544's part;
the tests answer `EHLO` without `SIZE`). Envelope for every case:
`EHLO dom\r\nMAIL FROM:<a@b> SIZE=n\r\nRCPT TO:<c@d>\r\nDATA\r\n`. Bytes after `DATA`'s 354 (request.bin), stdout, exit:

| Body (file bytes) | Sent after 354 | stdout | exit |
| --- | --- | --- | --- |
| `one\r\n` | `one\r\n.\r\n` | `[8\|250\|0]` | 0 |
| `Subject: t\r\n\r\n.hidden\r\nline\r\n..two\r\n.\r\nend\r\n` | `Subject: t\r\n\r\n..hidden\r\nline\r\n...two\r\n..\r\nend\r\n.\r\n` | `[50\|250\|0]` | 0 |
| `a\nb\n\n.c\nd\n` (bare LF) | `a\nb\n\n.c\nd\n\r\n.\r\n` (LF not rewritten, `.` after LF not stuffed) | `[15\|250\|0]` | 0 |
| `abc` (no newline) | `abc\r\n.\r\n` | `[8\|250\|0]` | 0 |
| `x\r\n.` | `x\r\n..\r\n.\r\n` | `[10\|250\|0]` | 0 |
| `a\rb\r\n.\rc` | `a\rb\r\n..\rc\r\n.\r\n` | `[14\|250\|0]` | 0 |
| empty | `.\r\n` | `[3\|250\|0]` | 0 |
| `.x\r\n` | `..x\r\n.\r\n` | `[8\|250\|0]` | 0 |
| `.` | `..\r\n.\r\n` | `[7\|250\|0]` | 0 |
| `a\r` | `a\r\r\n.\r\n` | `[7\|250\|0]` | 0 |
| `a\r\n.\r\nb` | `a\r\n..\r\nb\r\n.\r\n` | `[13\|250\|0]` | 0 |
| `\r\n` | `\r\n.\r\n` | `[5\|250\|0]` | 0 |
| `a\r\r\n.b\n\r\n.c` | `a\r\r\n..b\n\r\n..c\r\n.\r\n` | `[18\|250\|0]` | 0 |
| `-T -`, stdin `hi\n.x\n` | `hi\n.x\n\r\n.\r\n`; `MAIL FROM:<a@b>` has no SIZE | `[11\|250\|0]` | 0 |

Two `--mail-rcpt` (c@d, e@f): `RCPT TO:<c@d>`, `RCPT TO:<e@f>` in order, then DATA; `[8|250|0]`, exit 0.

Refusals (stderr exactly as shown; every one of them still sends `QUIT`):

| Reply | stderr | stdout | exit |
| --- | --- | --- | --- |
| MAIL `550` | `curl: (55) MAIL failed: 550` | `[0\|550\|0]` | 55 |
| only RCPT `550` | `curl: (55) RCPT failed: 550` | `[0\|550\|0]` | 55 |
| only RCPT `450` | `curl: (55) RCPT failed: 450` | `[0\|450\|0]` | 55 |
| 2nd RCPT `550` (1st 250) | `curl: (55) RCPT failed: 550` | `[0\|550\|0]` | 55 |
| DATA `554` | `curl: (55) DATA failed: 554` | `[0\|554\|0]` | 55 |
| DATA `350` | `curl: (55) DATA failed: 350` | `[0\|350\|0]` | 55 |
| final `552` (body sent) | `curl: (8) Weird server reply` | `[8\|552\|0]` | 8 |
| final `251` | `curl: (8) Weird server reply` | `[8\|251\|0]` | 8 |
| MAIL `251`, RCPT `251` | accepted | `[8\|250\|0]` | 0 |
| close instead of MAIL reply | `curl: (56) response reading failed (errno: 0)`, no QUIT | `[0\|250\|0]` | 56 |
| close instead of final reply | same, no QUIT | `[8\|000\|0]` | 56 |

Addresses: `--mail-from` absent → `MAIL FROM:<>`; `<a@b>`, `<a@b`, `a@b` → `<a@b>`; `c@d>` → `<c@d>`;
`<<a@b>>` → `<<a@b>>` (one leading `<` and one trailing `>` stripped, then bracketed); `alice` → `<alice>`.
`--mail-from ''` is exit 2 in the command-line layer. `-T` without `--mail-rcpt` sends `HELP` (BL-543), not MAIL.

Progress meter (no `-s`): `crlfend` (5-byte file) ends `160      5   0      0 160      8` - Total 5 (file
length), Xferd 8 (bytes sent with the mark), so 160%; 128 KiB file 100%/128.0k, size_upload 131077;
`-T -` shows Total 8 = Xferd 8, i.e. no known total. Final 552 still shows 8 sent.

### Decisions (taken under the unattended-run rules)

- `SIZE=`, `AUTH=`, `SMTPUTF8`, IDN conversion of the address's host and `--mail-rcpt-allowfails` stay BL-544's; the handler sends none of them and the tests answer `EHLO` without `SIZE`.
- The upload runs only with `-T` and at least one `--mail-rcpt`, as measured; otherwise the session keeps closing with `QUIT` (BL-543's commands).
- `size_upload` and progress count the bytes sent after `DATA`, stuffed dots and end mark included; the progress total is the upload's remaining length, or unknown for a non-seekable stream. `%{response_code}` is the last reply's code, reset to 0 once the message is sent (measured `000` when the server closes instead of answering). Results on the upload path carry a `TransferReport` with both; the session-open failures are unchanged.
- A read failure on the upload stream is not handled here (unmeasured); it propagates as before.
- The Console does not register the SMTP handler yet (BL-545), so the end-to-end check through `curl.exe` waits for it; the handler tests pin the bytes.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. SMTP -T sends MAIL FROM, RCPT TO per --mail-rcpt, DATA and the dot-stuffed message as curl 8.21.0 does, with exit 55/8/56 refusals, size_upload and upload progress
