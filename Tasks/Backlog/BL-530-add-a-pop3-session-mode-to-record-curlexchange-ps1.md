---
id: BL-530
title: Add a -Pop3 session mode to Record-CurlExchange.ps1
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-530 — Add a -Pop3 session mode to Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1 -Pop3` serves one POP3 session the way `-Ftp` serves FTP: a greeting, one reply per command from a default table overridable per verb, multi-line replies ended by `.` with dot-stuffing, `STLS` switching to TLS, and every line recorded in `request.bin` and `transcript.txt`.

## Context

- Needed to measure `pop3://`/`pop3s://` (audit row 34) before any POP3 bytes are pinned.
- Model on `-Ftp` (`.PARAMETER Ftp`, `.PARAMETER FtpReply`, `-Tls`).
- Default replies (RFC 1939, RFC 2449, RFC 5034): greeting `+OK POP3 ready <1896.697170952@localhost>` (the APOP timestamp), `CAPA` a multi-line list with `USER`, `SASL PLAIN LOGIN`, `STLS`, `TOP`, `UIDL`; `USER +OK`; `PASS +OK`; `APOP +OK`; `AUTH` `+` continuations then `+OK`; `STAT +OK 2 20`; `LIST` multi-line two messages (and `LIST n` single-line); `RETR n` multi-line from a `-Pop3Message` text with a line starting `.` to exercise stuffing; `DELE +OK`; `TOP` multi-line; `UIDL` multi-line; `NOOP +OK`; `QUIT +OK`; anything else `-ERR`.

## Acceptance criteria

- [ ] `.PARAMETER Pop3`, `.PARAMETER Pop3Reply` and `.PARAMETER Pop3Message` are documented in the script header.
- [ ] `Record-CurlExchange.ps1 -Pop3 -CurlArgs '-sS','-u','u:p','pop3://127.0.0.1:<P>/1'` against the reference curl writes the commands curl sent and the message it printed.
- [ ] A `-Pop3Reply 'PASS=-ERR denied'` override takes effect; `STLS` with `--ssl-reqd -k` completes over TLS.
- [ ] The existing HTTP and `-Ftp` modes produce identical files before and after the change.

## Notes

## Log

- 2026-09-28: Created.
