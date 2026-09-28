---
id: BL-775
title: Send SMTP MAIL, RCPT and VRFY addresses as UTF-8 on Linux and macOS and convert RCPT hosts to IDNA A-labels
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-543]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-775 — Send SMTP MAIL, RCPT and VRFY addresses as UTF-8 on Linux and macOS and convert RCPT hosts to IDNA A-labels

## Goal

A non-ASCII `--mail-from` or `--mail-rcpt` goes out in the bytes the platform's curl sends, and `MAIL FROM` / `RCPT TO` convert the host part to an IDNA A-label and add `SMTPUTF8` as curl 8.21.0 does.

## Context

- ADR-0135 §6: every SMTP command is sent as Latin-1, so a character above U+00FF becomes `?`. curl sends its argv bytes: the ANSI code page on Windows (measured, BL-543: `ö` went out as `F6`) and UTF-8 on Linux and macOS.
- curl's `smtp_parse_address` converts the host part of every address (`MAIL`, `RCPT`, `VRFY`) to an A-label, and `smtp_perform_mail`/`smtp_perform_rcpt_to` add `SMTPUTF8` when the server offers it and an address is not ASCII. `SmtpCommandTransfer.VerifyCommand` does this for `VRFY`; `SmtpMailTransaction.Bracket` does neither.
- Measure with `Record-CurlExchange.ps1 -Smtp` on Windows, and pin Linux/macOS behaviour from curl's source where it cannot be measured here.

## Acceptance criteria

- [ ] Measured first: `--mail-from jörg@bücher.example --mail-rcpt a@bücher.example -T mail.txt` request lines copied into Notes.
- [ ] `Curl.Protocol.Smtp.UnitTests` pin the `MAIL FROM` and `RCPT TO` bytes for a non-ASCII local part and host, with and without `SMTPUTF8` offered, per platform where they differ.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Filed from BL-543.

## Log

- 2026-09-28: Created.
