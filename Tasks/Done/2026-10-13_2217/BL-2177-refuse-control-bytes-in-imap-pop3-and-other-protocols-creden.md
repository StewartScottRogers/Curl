---
id: BL-2177
title: Refuse control bytes in IMAP, POP3 and other protocols' credentials and in SMTP VRFY and EXPN addresses with exit 43, as curl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2177 — Refuse control bytes in IMAP, POP3 and other protocols' credentials and in SMTP VRFY and EXPN addresses with exit 43, as curl 8.22.0 does

## Goal

An IMAP or POP3 user name or password holding a byte below 0x20, and an SMTP `VRFY` or `EXPN` address holding CR or LF, are exit 43 with no injected command sent, as curl 8.22.0 does.

## Context

- Found while doing BL-2162 (GF-0070), which covered FTP credentials, `--ftp-account`,
  `--ftp-alternative-to-user` and SMTP `MAIL FROM`, `AUTH=` and `RCPT TO`.
- curl 8.22.0 `lib/url.c` refuses a user name or password with a byte below 0x20 for every
  scheme without `PROTOPT_USERPWDCTRL`, before connecting: `control code detected in
  credentials`, exit 43 (`str_has_ctrl`).
- curl 8.22.0 `lib/smtp.c` `smtp_parse_address` also checks the `VRFY`/`EXPN` address
  (`smtp_perform_command`): `Refusing to send email address with a CR or LF`, exit 43. Curl
  builds it in `SmtpCommandTransfer`.
- Check which other Curl protocol handlers (LDAP, SMB, ...) write credentials into a command
  line, and add them to `touches`. Measure real curl with `Record-CurlExchange.ps1` before
  pinning whether `QUIT` follows.

## Acceptance criteria

- [x] A unit test per protocol pins exit 43 and that no command carrying the injected text is sent.
- [x] SMTP `VRFY` with a CR LF address is exit 43 with curl 8.22.0's message, pinned by a unit test.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Measured on curl 8.22.0 (Schannel, `Record-CurlExchange.ps1 -Smtp` and plain runs to port 1):
  - `-u` with a byte below 0x20 is exit 43, `control code detected in credentials`, before
    connecting, for imap, pop3, smtp and also ldap, smb, telnet, dict, mqtt, gopher, rtsp,
    tftp; http, https, ws and wss send it (exit 7 to port 1). A space is not refused.
  - `--mail-rcpt "a@b\r\nDELE x"` with no `-X`: EHLO, QUIT, exit 43, `Refusing to send email
    address with a CR or LF`.
  - `-X EXPN --mail-rcpt "a@b\r\nDELE x"`: curl sends `EXPN a@b\r\nDELE x SMTPUTF8` raw, exit 0;
    curl only parses the address for `VRFY`. So Curl refuses VRFY only and keeps sending a `-X`
    command's address as it is, pinned by `ExecuteAsync_ExpnRecipientWithCrLf_IsSentAsItIsAsCurlDoes`.
- The check sits at the top of each handler's `TransferAsync` (IMAP, POP3, SMTP), before any
  connect, as in FTP (BL-2162); protocols may not share code, so each has its own `HasControlByte`.
- The other schemes curl refuses for are filed as BL-2179.
- Tests: IMAP 503, POP3 356 (5 skipped), SMTP 367 (7 skipped) passed; full fast run green.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. IMAP, POP3 and SMTP credentials with a control byte, and an SMTP VRFY address with CR or LF, are exit 43 with nothing injected, as curl 8.22.0
