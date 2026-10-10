---
id: BL-2025
title: Cover and finish -F MIME mail messages: Console mail branch tests, unknown-length SIZE, 7bit partial bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2025 — Cover and finish -F MIME mail messages: Console mail branch tests, unknown-length SIZE, 7bit partial bytes

## Goal

Finish what BL-1988 left of -F MIME mail messages to smtp:// and imap:// URLs, matching curl 8.21.0.

## Context

- BL-1988 added MultipartFormBodyBuilder.BuildMailMessageAsync and wired it in CurlCommandRunner.BuildFormAsync; its measurements are in BL-1988 Notes.
- Measured on curl 8.21.0: `-F "=@m.txt;encoder=quoted-printable"` to smtp sends `MAIL FROM:<>` with no `SIZE=` (the message length is unknown); Curl sends `SIZE=`.
- Measured: `-F "=@b.txt;encoder=7bit"` with bytes A, 0xE9, B, LF sends `A` after the part headers, then exit 26 `read error getting mime data`; Curl sends nothing after DATA.

## Acceptance criteria

- [ ] A Curl.Console.UnitTests test runs -F to smtp:// and to imap:// through the runner and pins the uploaded message bytes, covering the mail branch of CurlCommandRunner.BuildFormAsync and MailRequestOptionsMapping.SendsFormAsMimeMessage.
- [ ] A mail message with a quoted-printable part sends no SIZE= over SMTP and IMAP answers as curl 8.21.0 does (measure first).
- [ ] A 7bit file part sends the bytes before the refused byte, as measured; a 7bit text part with a byte above 127 fails after DATA, not before connecting (measure first).
- [ ] Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Core.UnitLibrary,Curl.Protocol.Smtp.UnitLibrary reports 100% line and branch coverage for the changed code.
- [ ] dotnet build -warnaserror is clean and the fast tests are green.

## Notes

## Log

- 2026-10-10: Created.
