---
id: BL-542
title: Send an SMTP upload with MAIL FROM, RCPT TO and DATA
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-540]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-542 — Send an SMTP upload with MAIL FROM, RCPT TO and DATA

## Goal

With `-T <file>` (or `-T -`), the SMTP handler sends `MAIL FROM:<...>`, one `RCPT TO:<...>` per `--mail-rcpt`, `DATA`, the message with dot-stuffing and line endings as curl 8.21.0 sends them, and the terminating `CRLF.CRLF`, and maps a refused command to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, rows 31 and 34. Options arrive on the context (BL-534); `--mail-auth`, `--mail-rcpt-allowfails` and SIZE/SMTPUTF8 are BL-544.
- Measure with `Record-CurlExchange.ps1 -Smtp`: a body with a line starting `.`, a body with bare LF endings, a body not ending in a newline, `-T -` from `-StandardInput`, two `--mail-rcpt`, `MAIL` answered `550`, the only `RCPT` answered `550`, `DATA` answered `554`, and the final `250` replaced by `552`; record `request.bin` exactly.
- `%{size_upload}` and the progress reports follow what curl counts (measure with `-w '%{size_upload}'`).

## Acceptance criteria

- [ ] Measured first as above; request bytes, stdout, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Smtp.UnitTests` pin the client bytes byte for byte for each body shape, and the exit code and message for each refusal.
- [ ] Upload progress and `size_upload` match the measured values.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
