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
completed:
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

- [ ] A unit test per protocol pins exit 43 and that no command carrying the injected text is sent.
- [ ] SMTP `VRFY` with a CR LF address is exit 43 with curl 8.22.0's message, pinned by a unit test.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-13: Created.
