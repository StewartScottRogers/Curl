---
id: BL-544
title: Honour --mail-auth, --mail-rcpt-allowfails, SIZE and SMTPUTF8 in an SMTP upload
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-542]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-544 — Honour --mail-auth, --mail-rcpt-allowfails, SIZE and SMTPUTF8 in an SMTP upload

## Goal

The SMTP upload adds `AUTH=<addr>` to `MAIL FROM` for `--mail-auth`, carries on past refused recipients with `--mail-rcpt-allowfails` (failing only when all are refused), adds `SIZE=<n>` when the server advertises SIZE and the size is known, and `SMTPUTF8` when a non-ASCII address needs it, exactly as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 31. Builds on BL-542.
- Measure with `Record-CurlExchange.ps1 -Smtp`: `--mail-auth a@b`, two recipients with the first refused with and without `--mail-rcpt-allowfails`, both refused with it, SIZE advertised with `-T file` and with `-T -`, and a UTF-8 local part with and without `SMTPUTF8` advertised; record `request.bin` exactly.

## Acceptance criteria

- [ ] Measured first as above; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Smtp.UnitTests` pin each case's client bytes and outcome.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
