---
id: BL-776
title: Send SMTP MAIL, RCPT and VRFY addresses as UTF-8 on Linux and macOS and convert RCPT hosts to IDNA A-labels
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-543]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Documentation/Planning/Decisions/ADR-0135-smtp-without-a-message-sends-vrfy-expn-help-or-the-x-command-and-writes-each-reply-line.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-776 — Send SMTP MAIL, RCPT and VRFY addresses as UTF-8 on Linux and macOS and convert RCPT hosts to IDNA A-labels

## Goal

A non-ASCII `--mail-from` or `--mail-rcpt` goes out in the bytes the platform's curl sends, and `MAIL FROM` / `RCPT TO` convert the host part to an IDNA A-label and add `SMTPUTF8` as curl 8.21.0 does.

## Context

- ADR-0135 §6: every SMTP command is sent as Latin-1, so a character above U+00FF becomes `?`. curl sends its argv bytes: the ANSI code page on Windows (measured, BL-543: `ö` went out as `F6`) and UTF-8 on Linux and macOS.
- curl's `smtp_parse_address` converts the host part of every address (`MAIL`, `RCPT`, `VRFY`) to an A-label, and `smtp_perform_mail`/`smtp_perform_rcpt_to` add `SMTPUTF8` when the server offers it and an address is not ASCII. `SmtpCommandTransfer.VerifyCommand` does this for `VRFY`; `SmtpMailTransaction.Bracket` does neither.
- Measure with `Record-CurlExchange.ps1 -Smtp` on Windows, and pin Linux/macOS behaviour from curl's source where it cannot be measured here.

## Acceptance criteria

- [x] Measured first: `--mail-from jörg@bücher.example --mail-rcpt a@bücher.example -T mail.txt` request lines copied into Notes.
- [x] `Curl.Protocol.Smtp.UnitTests` pin the `MAIL FROM` and `RCPT TO` bytes for a non-ASCII local part and host, with and without `SMTPUTF8` offered, per platform where they differ.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Filed from BL-543.

Measured 2026-09-29, curl 8.21.0 (C:\windows\system32\curl.exe, Schannel, ACP 1252), `Record-CurlExchange.ps1 -Smtp`, EHLO advertising SIZE and SMTPUTF8:

- `--mail-from jörg@bücher.example --mail-rcpt a@bücher.example -T mail.txt`:
  `MAIL FROM:<j F6 rg@xn--bcher-kva.example> SIZE=18 SMTPUTF8` then `RCPT TO:<a@xn--bcher-kva.example>`.
- `--mail-from €łx@b.example`: `MAIL FROM:<80 6C 78 @b.example> SIZE=18 SMTPUTF8` (Windows best fit: `ł` -> `l`).
- `--mail-from łx@ł.example --mail-rcpt a@b`: `MAIL FROM:<lx@l.example> SIZE=18` - no SMTPUTF8, so curl decides on the argv bytes after best fit.
- `--mail-rcpt ł€@bücher.example` (no -T): `VRFY l 80 @xn--bcher-kva.example SMTPUTF8`.
- `--mail-rcpt jörg -X EXPN`: `EXPN j F6 rg SMTPUTF8`.

Linux and macOS are pinned from curl's source: argv is sent as given, which is UTF-8.

Decisions (by Claude under Stewart's delegation, recorded as an amendment to ADR-0135 §6):

- New `SmtpCommandLineText` in the SMTP library chooses the argv encoding from the host (system ANSI code page via `CodePagesEncodingProvider` on Windows, which best-fits; UTF-8 elsewhere), the same rule as `CredentialEncoding`. Carrying it on `MailRequestOptions` was rejected: it would touch `Curl.Protocol.Abstractions` (every protocol task) for one protocol.
- The address is round-tripped through the argv encoding before the IDNA step, so a best-fitted host is converted as curl sees it; SMTPUTF8 is decided on the argv bytes.
- The `-X` command and its recipient also go out as argv bytes (same rule, one line; measured `EXPN jörg`).
- `SmtpRun` pins Windows-1252 for every existing test so their Latin-1 expectations hold on Linux and macOS CI; the new tests pass UTF-8 explicitly.

Touches: added the ADR-0135 file for the §6 amendment; no task in Doing names it.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SMTP MAIL, RCPT, VRFY and -X send curl's argv bytes: ANSI code page with best fit on Windows, UTF-8 on Linux and macOS; hosts as IDNA A-labels; SMTPUTF8 decided on those bytes
