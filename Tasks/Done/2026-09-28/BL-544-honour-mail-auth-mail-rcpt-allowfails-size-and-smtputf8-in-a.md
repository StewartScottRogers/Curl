---
id: BL-544
title: Honour --mail-auth, --mail-rcpt-allowfails, SIZE and SMTPUTF8 in an SMTP upload
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-542]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-544 — Honour --mail-auth, --mail-rcpt-allowfails, SIZE and SMTPUTF8 in an SMTP upload

## Goal

The SMTP upload adds `AUTH=<addr>` to `MAIL FROM` for `--mail-auth`, carries on past refused recipients with `--mail-rcpt-allowfails` (failing only when all are refused), adds `SIZE=<n>` when the server advertises SIZE and the size is known, and `SMTPUTF8` when a non-ASCII address needs it, exactly as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 31. Builds on BL-542.
- Measure with `Record-CurlExchange.ps1 -Smtp`: `--mail-auth a@b`, two recipients with the first refused with and without `--mail-rcpt-allowfails`, both refused with it, SIZE advertised with `-T file` and with `-T -`, and a UTF-8 local part with and without `SMTPUTF8` advertised; record `request.bin` exactly.

## Acceptance criteria

- [x] Measured first as above; request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Smtp.UnitTests` pin each case's client bytes and outcome.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 on curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1 -Smtp`,
`-sS --mail-from a@b --mail-rcpt c@d -T mail.txt smtp://127.0.0.1:18025/h`, `mail.txt` the
21 bytes `Subject: t\r\n\r\nhello\r\n`; request lines from `request.bin`/`transcript.txt`:

- `--mail-auth x@y` without `-u`: `MAIL FROM:<a@b> SIZE=21`, exit 0. No `AUTH=`: curl only
  sends it once SASL succeeded.
- `-u u:p --mail-auth x@y` (and `<x@y>`): `AUTH CRAM-MD5`..., `MAIL FROM:<a@b> AUTH=<x@y> SIZE=21`, exit 0.
- `-u u:p --mail-auth xy`, no `--mail-from`: `MAIL FROM:<> AUTH=<xy> SIZE=21`.
- `-u u:p --mail-auth x@yü.de`: `MAIL FROM:<> AUTH=<x@xn--y-eha.de> SIZE=21 SMTPUTF8`.
- `-u u:p --mail-auth xü@y`: `MAIL FROM:<a@b> AUTH=<xü@y> SIZE=21 SMTPUTF8`.
- `--mail-auth ''`: exit 2, `curl: option --mail-auth: blank argument where content is expected`.
- `-T -` (stdin), `-T empty.txt`, `EHLO` without `SIZE`, or `HELO` session: `MAIL FROM:<a@b>`.
- `EHLO` with `size 100`, `SIZE` or `SIZEX`: `SIZE=21` (any case, prefix match).
- Two recipients, first `RCPT` 550, no allowfails: `RCPT TO:<c@d>`, `QUIT`; exit 55 `curl: (55) RCPT failed: 550`.
- Same with `--mail-rcpt-allowfails`: both `RCPT`s, `DATA`, message; exit 0. Second refused 551: exit 0. First 450: exit 0.
- Allowfails, both refused (550, 551): both `RCPT`s, `QUIT`; exit 55 `curl: (55) RCPT failed: 551 (last error)`.
- Allowfails, one accepted, `DATA=554`: exit 55 `DATA failed: 554`. Second `RCPT` unanswered: exit 56.
- `--mail-from aü@b --mail-rcpt cü@dü.de`: `MAIL FROM:<aü@b> SIZE=21 SMTPUTF8`, `RCPT TO:<cü@xn--d-eha.de>`;
  without `SMTPUTF8` advertised: same without ` SMTPUTF8` (host still an A-label).
- `--mail-from a@bü.de`: `MAIL FROM:<a@xn--b-eha.de> SIZE=21 SMTPUTF8`. `--mail-rcpt cü@d` only: `MAIL FROM:<a@b> SIZE=21 SMTPUTF8`.
- `EHLO` advertising lowercase `smtputf8`: `MAIL FROM:<aü@b> SMTPUTF8`, and with no upload `VRFY jörg@x SMTPUTF8`.
- `ü` went out as the single byte `FC` (Windows argv code page), which the Latin-1 channel reproduces.

Decisions (ADR-0136): `MAIL FROM` parameters in order `AUTH=`, `SIZE=`, `SMTPUTF8`; `SIZE` is
the bytes left in a seekable upload; addresses share `SmtpMailbox` with `VRFY`; `STARTTLS`,
`SIZE` and `SMTPUTF8` match in any case (`SmtpReply.Advertises`). That last point corrects
BL-543's `ExecuteAsync_NonAsciiRecipientWithoutSmtpUtf8Advertised_SendsNoSmtpUtf8`, which
pinned lowercase `smtputf8` as not advertised without measuring it; the test now uses an
`EHLO` without `SMTPUTF8`, and a new test pins the measured lowercase case.

Added `Documentation/Planning/Decisions` to `touches` for ADR-0136 and its README index line;
no task in Doing names it.

Result: all 183 SMTP tests pass, the new cases in `SmtpProtocolHandlerMailExtensionTests`; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary`: 100% line,
100% branch, 87 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. SMTP upload sends AUTH= after SASL, SIZE= and SMTPUTF8 as advertised, A-label hosts, and --mail-rcpt-allowfails carries on past refused recipients, as curl 8.21.0 does
