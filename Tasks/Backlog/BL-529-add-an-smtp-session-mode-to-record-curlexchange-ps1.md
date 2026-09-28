---
id: BL-529
title: Add an -Smtp session mode to Record-CurlExchange.ps1
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-529 — Add an -Smtp session mode to Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1 -Smtp` serves one SMTP session the way `-Ftp` serves FTP: a greeting, then one reply per command line from a default table overridable per verb, with the `DATA` body read up to the terminating `CRLF.CRLF`, recording every line in `request.bin` and both directions in `transcript.txt`, and `STARTTLS` switching to TLS as `-Ftp`'s `AUTH` does.

## Context

- Needed to measure `smtp://`/`smtps://` (audit rows 31 and 34) before any SMTP bytes are pinned.
- Model on the `-Ftp` mode (`.PARAMETER Ftp`, `.PARAMETER FtpReply` `VERB=reply` overrides, `FtpIdleMilliseconds`, the throwaway TLS certificate of `-Tls`). Reuse its helpers rather than copying them where the script's structure allows.
- Default replies (RFC 5321): greeting `220 localhost ESMTP`, `EHLO` a multiline `250` advertising `AUTH PLAIN LOGIN CRAM-MD5`, `STARTTLS`, `SIZE 1000000`, `8BITMIME`, `SMTPUTF8`; `HELO 250`; `AUTH` `235` (with the `334` continuations LOGIN needs); `MAIL 250`; `RCPT 250`; `DATA 354` then `250` after the body; `VRFY 250`; `EXPN 250`; `HELP 214`; `NOOP 250`; `RSET 250`; `QUIT 221`; anything else `502`. Implicit TLS for `smtps://` is the existing `-Tls` switch combined with `-Smtp`.

## Acceptance criteria

- [ ] `.PARAMETER Smtp` and `.PARAMETER SmtpReply` are documented in the script header with the default table above.
- [ ] `Record-CurlExchange.ps1 -Smtp -CurlArgs '-sS','--mail-from','a@b','--mail-rcpt','c@d','-T','mail.txt','smtp://127.0.0.1:<P>/'` against the reference curl writes `request.bin` holding `EHLO`, `MAIL FROM`, `RCPT TO`, `DATA`, the body and `QUIT`, and `transcript.txt` with both directions.
- [ ] `-SmtpReply 'RCPT=550 no such user'` changes that reply; `STARTTLS` with `--ssl-reqd -k` completes over TLS.
- [ ] The existing HTTP and `-Ftp` modes produce identical files before and after the change.

## Notes

## Log

- 2026-09-28: Created.
