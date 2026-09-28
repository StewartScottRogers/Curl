---
id: BL-531
title: Add an -Imap session mode to Record-CurlExchange.ps1
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-531 — Add an -Imap session mode to Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1 -Imap` serves one IMAP4rev1 session: an untagged greeting, then for each tagged command the untagged data and the tagged completion from a default table overridable per command, literals (`{n}`) both ways (`APPEND` uploads and `FETCH` bodies), `STARTTLS` switching to TLS, and every line recorded in `request.bin` and `transcript.txt`.

## Context

- Needed to measure `imap://`/`imaps://` (audit rows 31 and 34) before any IMAP bytes are pinned.
- Model on `-Ftp` (`.PARAMETER Ftp`, `.PARAMETER FtpReply`, `-Tls`). Tagged replies echo the client's tag (`A001 OK ...`).
- Default replies (RFC 3501): greeting `* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready`; `CAPABILITY`; `LOGIN OK`; `AUTHENTICATE` with `+` continuations then `OK`; `SELECT`/`EXAMINE` with `* 2 EXISTS`, `* OK [UIDVALIDITY 1]`; `FETCH`/`UID FETCH` returning `-ImapMessage` as a literal; `LIST` two mailboxes; `SEARCH` `* SEARCH 1 2`; `APPEND` with the `+` continuation then `OK`; `NOOP`; `LOGOUT` with `* BYE`; anything else `BAD`.

## Acceptance criteria

- [x] `.PARAMETER Imap`, `.PARAMETER ImapReply` and `.PARAMETER ImapMessage` are documented in the script header.
- [x] `Record-CurlExchange.ps1 -Imap -CurlArgs '-sS','-u','u:p','imap://127.0.0.1:<P>/INBOX;UID=1'` against the reference curl writes the tagged commands curl sent and the message it printed.
- [x] An `APPEND` upload (`-T mail.txt imap://.../INBOX`) records the literal curl sent; an `-ImapReply 'LOGIN=NO denied'` override takes effect; `STARTTLS` with `--ssl-reqd -k` completes over TLS.
- [x] The existing HTTP and `-Ftp` modes produce identical files before and after the change.

## Notes

- `-Imap` is a self-contained `$serveImapSession` block modelled on `-Smtp`, sharing only
  `$sessionHelpers` (`Wrap-Tls`, `Get-Override`). Added `-ImapIdleMilliseconds` (default
  5000) like the other session modes, and `-Imap -Tls` for implicit imaps://.
- Choice: an `-ImapReply` value is written without the tag; its last line gets curl's tag,
  earlier lines go out as given, so one override can replace untagged data too. UID
  commands are keyed `UID FETCH`/`UID SEARCH`. A literal is always accepted with `+`
  before the reply (overriding APPEND changes only the tagged completion).
- Measured against curl 8.21.0 (Schannel): with `AUTH=PLAIN AUTH=LOGIN` advertised curl
  uses `AUTHENTICATE PLAIN` (no SASL-IR, so one `+ ` continuation), so the `LOGIN=NO denied`
  check needs `--login-options AUTH=+LOGIN`; curl then exits 67, "Access denied.". APPEND
  sends `APPEND INBOX (\Seen) {26}`. `--ssl-reqd -k` runs STARTTLS, re-issues CAPABILITY
  over TLS and fetches normally. `imaps://` with `-Tls` lists INBOX and Sent.
- HTTP and `-Ftp` recordings before and after the change are byte-identical (the FTP
  transcript's EPSV port, random per run, masked).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Record-CurlExchange.ps1 -Imap serves an IMAP4rev1 session (literals both ways, STARTTLS, overrides) and records it
