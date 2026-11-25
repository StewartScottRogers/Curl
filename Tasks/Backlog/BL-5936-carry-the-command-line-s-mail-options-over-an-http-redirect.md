---
id: BL-5936
title: Carry the command line's mail options over an HTTP redirect to a mail URL
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/RedirectFollowerTests.cs, Curl.Engine.UnitLibrary/MailRequestOptionsMapping.cs, Curl.Engine.UnitTests/MailRequestOptionsMappingTests.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5936 — Carry the command line's mail options over an HTTP redirect to a mail URL

## Goal

After `-L --proto-redir all` follows an HTTP redirect to an `imap://`, `pop3://` or `smtp://` URL, Curl uses the command line's `--login-options`, `--mail-from`, `--mail-rcpt` and other mail options as curl 8.22.0 does.

## Context

Found in BL-5925. `MailRequestOptionsMapping` builds `MailRequestOptions` only for a mail scheme, so an HTTP first URL has `Mail` null, and `RedirectFollower.HopMail` then makes a bare `MailRequestOptions { BearerTokenWithheld = true }` (or passes null) for the mail hop. Everything else on the command line for mail is lost.

Measured with `Record-CurlExchange.ps1` (BL-5925): an HTTP server answering `302` with `Location: smtp://v@127.0.0.1:<port>`, and curl run as `-s -S -L --proto-redir all --oauth2-bearer tok --login-options AUTH=PLAIN --mail-from a@b --mail-rcpt c@d -T NUL http://127.0.0.1:<port2>/`. curl 8.22.0 sends `EHLO <host>`, `AUTH PLAIN` (`AHYA`), `MAIL FROM:<a@b>`, `RCPT TO:<c@d>`, `DATA`, `.`, `QUIT`, exit 0. Curl sends `EHLO`, `AUTH CRAM-MD5`, then `HELP` (no mail-from or recipients), exit 67 after BL-5925. The same with `imap://v@.../INBOX` and `--login-options AUTH=PLAIN`: curl sends `AUTHENTICATE PLAIN` and lists INBOX; Curl stops after `CAPABILITY`, exit 67, because the login options are lost.

## Acceptance criteria

- [ ] Both measurements above, run again through `Record-CurlExchange.ps1` against real curl and Curl, give the same commands and exit codes.
- [ ] A focused unit test in each changed library's own `.UnitTests` project pins the behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
