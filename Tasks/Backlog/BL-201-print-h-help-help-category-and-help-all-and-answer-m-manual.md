---
id: BL-201
title: Print -h/--help, --help <category> and --help all, and answer -M/--manual
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-201 — Print -h/--help, --help <category> and --help all, and answer -M/--manual

## Goal

`-h`, `--help <category>`, `--help all` and `-M` print what curl 8.21.0 prints, byte for byte.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C15. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Each of `-h`, `--help all`, `--help http` and `--help category` is byte-equal to curl 8.21.0 (measured), generated from the option table where possible.
- [ ] `-M` behaves as measured on the reference build (manual text or its refusal).
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C15 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
