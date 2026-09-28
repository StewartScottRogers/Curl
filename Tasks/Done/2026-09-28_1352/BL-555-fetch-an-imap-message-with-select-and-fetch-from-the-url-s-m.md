---
id: BL-555
title: Fetch an IMAP message with SELECT and FETCH from the URL's mailbox, UID and section
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-553]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-555 — Fetch an IMAP message with SELECT and FETCH from the URL's mailbox, UID and section

## Goal

`imap://host/<mailbox>;UID=<n>` (with `;UIDVALIDITY=`, `;SECTION=` and `;PARTIAL=` as curl reads them, and `;MAILINDEX=` where curl 8.21.0 accepts it) sends `SELECT` then the `FETCH`/`UID FETCH` curl sends, writes the literal's bytes, and maps a UIDVALIDITY mismatch and a `NO` to curl's exit codes and messages.

## Context

- Conformance audit 2026-09-28, row 34. URL model: BL-533's ADR and RFC 5092 (IMAP URL), as curl reads it.
- Measure with `Record-CurlExchange.ps1 -Imap`: `INBOX;UID=1`, `INBOX;MAILINDEX=1`, `INBOX;UID=1;SECTION=TEXT`, `INBOX;UID=1;PARTIAL=0.10`, `INBOX;UIDVALIDITY=2;UID=1` against `UIDVALIDITY 1`, a mailbox name needing quoting (`My Box`), and `FETCH` answered `NO`; record request lines and stdout bytes.

## Acceptance criteria

- [x] Measured first as above; request lines, stdout bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Imap.UnitTests` pin client bytes, output bytes and outcome for each case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (curl 8.21.0 Schannel, 2026-09-28)

`Record-CurlExchange.ps1 -Port 18155 -Imap -ImapIdleMilliseconds 1500 -CurlArgs '-sS','imap://127.0.0.1:18155/<path>'`,
default replies unless an `-ImapReply` is named. Every run sent `A001 CAPABILITY` first;
"then" lists what followed. Default stdout is the recorder's 100-byte message
(`From: sender@example.com\r\n...Hello from the recorder.\r\n`).

| Path / reply | Then sent | Stdout | Exit, stderr |
| --- | --- | --- | --- |
| `INBOX;UID=1` | `A002 SELECT INBOX`, `A003 UID FETCH 1 BODY[]`, `A004 LOGOUT` | message | 0 |
| `INBOX;MAILINDEX=1` | `SELECT INBOX`, `A003 FETCH 1 BODY[]`, `LOGOUT` | message | 0 |
| `INBOX;UID=1;SECTION=TEXT` | `SELECT INBOX`, `UID FETCH 1 BODY[TEXT]`, `LOGOUT` | message | 0 |
| `INBOX;UID=1;PARTIAL=0.10` | `SELECT INBOX`, `UID FETCH 1 BODY[]<0.10>`, `LOGOUT` | message | 0 |
| `INBOX;UIDVALIDITY=2;UID=1` (server 1) | `SELECT INBOX`, `A003 LOGOUT` | empty | 78 `curl: (78) Mailbox UIDVALIDITY has changed` |
| `INBOX;UIDVALIDITY=1;UID=1`, `=01`, `=abc` | `SELECT`, `UID FETCH 1 BODY[]`, `LOGOUT` | message | 0 (compared as a number; no number, no check) |
| `My%20Box;UID=1` | `SELECT "My Box"`, fetch, `LOGOUT` | message | 0 |
| `a%22b`, `a%5Cb`, `a(b`, `a%25b`, `a%5Db`, `a*b`, `a%7Bb` | `SELECT "a\"b"`, `"a\\b"`, `"a(b"`, `"a%b"`, `"a]b"`, `"a*b"`, `"a{b"` | message | 0 |
| `INBOX/;UID=1/;SECTION=TEXT/` | `SELECT INBOX`, `UID FETCH 1 BODY[TEXT]` | message | 0 |
| `INBOX;uid=1;section=text` | `SELECT INBOX`, `UID FETCH 1 BODY[text]` | message | 0 |
| `INBOX;FOO=1`, `INBOX;UID`, `INBOX;UID=1;UID=2`, `a%09b;UID=1` | `A002 LOGOUT` | empty | 3 `curl: (3) URL using bad/illegal format or missing URL` |
| `SELECT=NO no mailbox` | `SELECT INBOX`, `A003 LOGOUT` | empty | 67 `curl: (67) Select failed` |
| `UID FETCH=NO no such message` | `SELECT`, `UID FETCH`, `A004 LOGOUT` | empty | 78 `curl: (78) Remote file not found` |
| `UID FETCH=OK nothing` | same | empty | 78 `Remote file not found` |
| `UID FETCH=* 1 FETCH (UID 1 BODY[] "hi")\r\nOK done` | same | empty | 8 `curl: (8) Failed to parse FETCH response.` |
| `UID FETCH=* 1 FETCH (BODY[] {3}\r\nabc)\r\nNO after` | same | `abc` | 8 `curl: (8) Weird server reply` |
| `UID FETCH=* 1 FETCH (BODY[] {100}\r\nabc` (last line tagged `A003 abc`, then hang-up) | `SELECT`, `UID FETCH`; no `LOGOUT` seen | `A003 abc\r\n` | 18 `curl: (18) end of response with 90 bytes missing` |
| `UID FETCH=* 1 FETCH (BODY[] {0}\r\n)\r\nOK done` | `SELECT`, `UID FETCH`, `LOGOUT` | empty | 0 |

### Plan and decisions

- `ImapUrlPath` reads the path as `imap_parse_url_path` does; `ImapQuoting` quotes the
  mailbox as `imap_atom` does; `ImapSession.PerformAsync` runs `SELECT`/`FETCH` once the
  session is open. `ImapControlChannel` gained `ReadUntaggedAsync` (stop at the untagged
  `FETCH`, literal unread) and `ReadLiteralPieceAsync` (stream the literal), so a message
  of any size streams to the output instead of passing the 65535-byte line cap.
- Unmeasurable choices are in ADR-0132: no `LOGOUT` after a failure inside the literal
  (exit 18, 23), exit 56 when the server closes after the literal, a read failure
  mid-literal counts as the server closing, and non-fetch URLs keep closing with success
  until BL-556/BL-557.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0132 and its index row; no
  task in Doing names it (BL-519 touches Curl.Console, BL-549 Pop3).
- Without credentials curl sends no `LOGIN`, so the fetch was measured unauthenticated;
  authentication is BL-554's.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. imap://host/<mailbox>;UID=n (UIDVALIDITY, SECTION, PARTIAL, MAILINDEX) sends SELECT and FETCH/UID FETCH as curl 8.21.0 does, streams the literal to the output and maps SELECT NO (67), UIDVALIDITY mismatch and FETCH NO (78), unparsable FETCH (8), cut-short literal (18) and malformed paths (3)
