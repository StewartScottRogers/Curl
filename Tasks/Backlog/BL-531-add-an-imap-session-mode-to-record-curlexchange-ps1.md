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
completed:
---
# BL-531 — Add an -Imap session mode to Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1 -Imap` serves one IMAP4rev1 session: an untagged greeting, then for each tagged command the untagged data and the tagged completion from a default table overridable per command, literals (`{n}`) both ways (`APPEND` uploads and `FETCH` bodies), `STARTTLS` switching to TLS, and every line recorded in `request.bin` and `transcript.txt`.

## Context

- Needed to measure `imap://`/`imaps://` (audit rows 31 and 34) before any IMAP bytes are pinned.
- Model on `-Ftp` (`.PARAMETER Ftp`, `.PARAMETER FtpReply`, `-Tls`). Tagged replies echo the client's tag (`A001 OK ...`).
- Default replies (RFC 3501): greeting `* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready`; `CAPABILITY`; `LOGIN OK`; `AUTHENTICATE` with `+` continuations then `OK`; `SELECT`/`EXAMINE` with `* 2 EXISTS`, `* OK [UIDVALIDITY 1]`; `FETCH`/`UID FETCH` returning `-ImapMessage` as a literal; `LIST` two mailboxes; `SEARCH` `* SEARCH 1 2`; `APPEND` with the `+` continuation then `OK`; `NOOP`; `LOGOUT` with `* BYE`; anything else `BAD`.

## Acceptance criteria

- [ ] `.PARAMETER Imap`, `.PARAMETER ImapReply` and `.PARAMETER ImapMessage` are documented in the script header.
- [ ] `Record-CurlExchange.ps1 -Imap -CurlArgs '-sS','-u','u:p','imap://127.0.0.1:<P>/INBOX;UID=1'` against the reference curl writes the tagged commands curl sent and the message it printed.
- [ ] An `APPEND` upload (`-T mail.txt imap://.../INBOX`) records the literal curl sent; an `-ImapReply 'LOGIN=NO denied'` override takes effect; `STARTTLS` with `--ssl-reqd -k` completes over TLS.
- [ ] The existing HTTP and `-Ftp` modes produce identical files before and after the change.

## Notes

## Log

- 2026-09-28: Created.
