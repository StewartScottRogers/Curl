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
completed: 2026-09-28
---
# BL-529 — Add an -Smtp session mode to Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1 -Smtp` serves one SMTP session the way `-Ftp` serves FTP: a greeting, then one reply per command line from a default table overridable per verb, with the `DATA` body read up to the terminating `CRLF.CRLF`, recording every line in `request.bin` and both directions in `transcript.txt`, and `STARTTLS` switching to TLS as `-Ftp`'s `AUTH` does.

## Context

- Needed to measure `smtp://`/`smtps://` (audit rows 31 and 34) before any SMTP bytes are pinned.
- Model on the `-Ftp` mode (`.PARAMETER Ftp`, `.PARAMETER FtpReply` `VERB=reply` overrides, `FtpIdleMilliseconds`, the throwaway TLS certificate of `-Tls`). Reuse its helpers rather than copying them where the script's structure allows.
- Default replies (RFC 5321): greeting `220 localhost ESMTP`, `EHLO` a multiline `250` advertising `AUTH PLAIN LOGIN CRAM-MD5`, `STARTTLS`, `SIZE 1000000`, `8BITMIME`, `SMTPUTF8`; `HELO 250`; `AUTH` `235` (with the `334` continuations LOGIN needs); `MAIL 250`; `RCPT 250`; `DATA 354` then `250` after the body; `VRFY 250`; `EXPN 250`; `HELP 214`; `NOOP 250`; `RSET 250`; `QUIT 221`; anything else `502`. Implicit TLS for `smtps://` is the existing `-Tls` switch combined with `-Smtp`.

## Acceptance criteria

- [x] `.PARAMETER Smtp` and `.PARAMETER SmtpReply` are documented in the script header with the default table above.
- [x] `Record-CurlExchange.ps1 -Smtp -CurlArgs '-sS','--mail-from','a@b','--mail-rcpt','c@d','-T','mail.txt','smtp://127.0.0.1:<P>/'` against the reference curl writes `request.bin` holding `EHLO`, `MAIL FROM`, `RCPT TO`, `DATA`, the body and `QUIT`, and `transcript.txt` with both directions.
- [x] `-SmtpReply 'RCPT=550 no such user'` changes that reply; `STARTTLS` with `--ssl-reqd -k` completes over TLS.
- [x] The existing HTTP and `-Ftp` modes produce identical files before and after the change.

## Notes

- Verified 2026-09-28 against curl 8.21.0 (Git for Windows): plain send (exit 0; request.bin
  `EHLO mail.txt`, `MAIL FROM:<a@b> SIZE=35`, `RCPT TO:<c@d>`, `DATA`, the dot-stuffed body,
  `.`, `QUIT`); `RCPT=550 no such user` gives exit 55 `curl: (55) RCPT failed: 550`;
  `--ssl-reqd -k` does STARTTLS, re-EHLOs over TLS and sends (exit 0); `-Smtp -Tls` smtps://
  with `-u` authenticates by CRAM-MD5 (curl's pick of the three advertised); AUTH=LOGIN and
  AUTH=PLAIN continuations work; `MAIL=CLOSE` gives exit 56.
- Regression: five HTTP, HTTPS and FTP (RETR, refused PASS, explicit-TLS STOR) runs recorded
  with the old and new script gave byte-identical files (26 files), apart from the PASV data
  port, which differs per run by design.
- Choices: `Wrap-Tls` and `Get-Override` moved into a shared `$sessionHelpers` block that both
  sessions dot-source from text, since a script block handed to the server runspace would run
  back in the main one, which is blocked waiting for curl; FtpReply and SmtpReply parse through
  one `ConvertTo-ReplyOverrides`. EHLO leaves STARTTLS out once the session is TLS (RFC 3207).
  Extra override verb DATADONE (the reply after the body), mirroring STORDONE. Added
  `-SmtpIdleMilliseconds` (default 5000) rather than reusing the FTP-named one. `-Smtp` with
  `-Ftp` or `-NoServer` is refused. The CRAM-MD5 challenge is the fixed
  `<1896.697170952@localhost>`, so recordings are reproducible.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Record-CurlExchange.ps1 -Smtp serves one SMTP session (EHLO, AUTH, STARTTLS, DATA body) with -SmtpReply overrides and a two-way transcript
