---
id: BL-550
title: Send POP3 -X custom commands and honour -l, -I and --login-options
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-549]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-550 — Send POP3 -X custom commands and honour -l, -I and --login-options

## Goal

`-X <command>` on a POP3 URL (`-X DELE`, `-X UIDL`, `-X TOP` and so on) is sent as curl 8.21.0 sends it, with a multi-line or single-line answer written as curl writes it, and `-l`/`--list-only`, `-I` and the remaining `--login-options` behave as curl's do for POP3.

## Context

- Conformance audit 2026-09-28, rows 23 and 34. `CurlManual.txt` (`--request`, `--list-only`) and https://everything.curl.dev/usingcurl/pop3 describe the behaviour; confirm each by measurement.
- Measure with `Record-CurlExchange.ps1 -Pop3`: `-X DELE pop3://h/1`, `-X UIDL pop3://h/`, `-X "TOP 1 0" pop3://h/`, `-l pop3://h/1`, `-I pop3://h/1`; record request lines and stdout bytes.

## Acceptance criteria

- [ ] Measured first as above; request lines, stdout bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Pop3.UnitTests` pin client bytes, output bytes and outcome for each case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
