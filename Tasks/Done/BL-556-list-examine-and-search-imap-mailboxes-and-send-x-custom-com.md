---
id: BL-556
title: List, examine and search IMAP mailboxes and send -X custom commands
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-555]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-556 — List, examine and search IMAP mailboxes and send -X custom commands

## Goal

`imap://host/` sends `LIST`, `imap://host/<mailbox>` without a UID does what curl 8.21.0 does (`EXAMINE` or `SELECT` and its output), `imap://host/<mailbox>?<criteria>` sends `SEARCH`, and `-X <command>` is sent as curl sends it, each writing the untagged responses curl writes.

## Context

- Conformance audit 2026-09-28, row 34.
- Measure with `Record-CurlExchange.ps1 -Imap`: `imap://h/`, `imap://h/INBOX`, `imap://h/INBOX?NEW`, `-X "EXAMINE INBOX" imap://h/`, `-X "STORE 1 +FLAGS \Seen" imap://h/INBOX`, and `SEARCH` answered `BAD`; record request lines and stdout bytes.

## Acceptance criteria

- [x] Measured first as above; request lines, stdout bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Imap.UnitTests` pin client bytes, output bytes and outcome for each case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 on curl 8.21.0 (Schannel), `Record-CurlExchange.ps1 -Imap`, `-sS -u u:p`,
recorder default replies unless an `-ImapReply` is named. Commands after `AUTHENTICATE PLAIN` (A002):

| Case | Commands sent | stdout | stderr / exit |
| --- | --- | --- | --- |
| `imap://h/` | `A003 LIST "" *`, `A004 LOGOUT` | `* LIST (\HasNoChildren) "/" INBOX\r\n* LIST (\HasNoChildren) "/" Sent\r\n` | 0 |
| `imap://h/INBOX` | `A003 LIST "INBOX" *` (no EXAMINE, no SELECT), LOGOUT | the two LIST lines | 0 |
| `imap://h/INBOX?NEW` | `SELECT INBOX`, `SEARCH NEW`, LOGOUT | `* SEARCH 1 2\r\n` | 0 |
| `-X "EXAMINE INBOX" imap://h/` | `A003 EXAMINE INBOX` (no SELECT), LOGOUT | all five untagged EXAMINE lines | 0 |
| `-X "STORE 1 +FLAGS \Seen" imap://h/INBOX` | `SELECT INBOX`, `STORE 1 +FLAGS \Seen` answered BAD, LOGOUT | empty | `curl: (21) Quote command returned error` |
| `imap://h/INBOX?NEW`, `SEARCH=BAD Nope` | SELECT, `SEARCH NEW`, LOGOUT | empty | `curl: (21) Quote command returned error` |

Further measured rules, each pinned in `ImapProtocolHandlerListTests`:
- Filtering: LIST writes only untagged `LIST` (in any case, numbered or not); SEARCH only `SEARCH`; `-X` writes the
  untagged responses named by its first word, plus `FETCH` for `STORE`, and every untagged response for
  SELECT, EXAMINE, SEARCH, EXPUNGE, LSUB, UID, GETQUOTAROOT, NOOP (NOOP, UID, LSUB, lower-case `store`, NAMESPACE measured).
  Each written line keeps its CR LF.
- `-X` is percent-decoded (`FOO%20BAR` sent `FOO BAR`); `-X FOO%0ABAR` is exit 3 after login and LOGOUT.
- The mailbox in `LIST "..." *` has `\` and `"` escaped but is never quoted again (`A%22B%5CC` -> `"A\"B\\C"`).
- The query is percent-decoded and sent raw (`SUBJECT%C3%A9` -> the two bytes, no CHARSET); an empty query or one
  decoding to a control byte is dropped (LIST instead); a query without a mailbox is ignored (`LIST "" *`).
- `SELECT` refused before a `-X` command: exit 67 `Select failed`. LIST refused: exit 21.
- A written line announcing a literal (first `{` outside a quoted string, `\` escaping inside quotes, then digits and
  `}`) ends the listing: the line, then the literal's bytes with no line end, then LOGOUT, rest unread, exit 0.
  `"{3}"`, `"a {3}"`, `"a\"{3}"` are not literals; `{x} {3}` is none (only the first brace is read); `{0}` ends it too.

Decision (no ADR needed, all behaviour measured): the listing's literal is streamed with the fetch's copy loop, so
the server closing inside it is exit 18 and a failing output exit 23, both without LOGOUT, as for FETCH (not measured).
`ImapControlChannel.ReadUntaggedAsync` now returns the completion's status instead of `null`, so LIST can tell OK
from NO. Upload (BL-557) still logs out with success.

Tests: IMAP 291 pass (55 new in `ImapProtocolHandlerListTests`; 56 older session/login tests now expect the
`LIST "" *` curl sends after login). Quality: 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. imap:// lists with LIST, ?query searches with SEARCH, and -X sends its command, writing the untagged responses curl 8.21.0 writes
