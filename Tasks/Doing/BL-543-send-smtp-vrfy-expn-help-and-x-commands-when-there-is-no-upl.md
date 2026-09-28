---
id: BL-543
title: Send SMTP VRFY, EXPN, HELP and -X commands when there is no upload
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-540]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-543 — Send SMTP VRFY, EXPN, HELP and -X commands when there is no upload

## Goal

Without `-T`, the SMTP handler does what curl 8.21.0 does: `VRFY` for each `--mail-rcpt`, `EXPN` with `-X EXPN`, `HELP` when there is no recipient, and any other `-X <command>` sent as given, writing the server's reply text to the output.

## Context

- Conformance audit 2026-09-28, row 34. The mapping of `-X` and `--mail-rcpt` to commands is in `CurlManual.txt` (`--request`, `--mail-rcpt`) and https://everything.curl.dev/usingcurl/smtp; confirm each by measurement.
- Measure with `Record-CurlExchange.ps1 -Smtp`: `smtp://h/ --mail-rcpt a@b`, `-X EXPN --mail-rcpt list`, `smtp://h/` alone, `-X NOOP`, and a `VRFY` answered `550`; record stdout, which carries the reply text.

## Acceptance criteria

- [ ] Measured first as above; request lines, stdout bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Smtp.UnitTests` pin client bytes, output bytes and outcome for each case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
