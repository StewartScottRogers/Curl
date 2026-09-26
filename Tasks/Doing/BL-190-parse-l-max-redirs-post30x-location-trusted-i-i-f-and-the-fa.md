---
id: BL-190
title: Parse -L, --max-redirs, --post30x, --location-trusted, -i, -I, -f and the fail options
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-190 — Parse -L, --max-redirs, --post30x, --location-trusted, -i, -I, -f and the fail options

## Goal

`-L`, `--max-redirs`, `--post301`, `--post302`, `--post303`, `--location-trusted`, `-i`, `-I`, `-f`, `--fail-with-body` and `--fail-early` parse into `CommandLineOptions`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- None of these has a row in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` today (checked 2026-09-26). `-I` maps onto the existing `NoBody` transfer member when wired (BL-232).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `--max-redirs -1` means no limit; the default is 50.
- [ ] The measured curl 8.21.0 result of combining `-f` and `--fail-with-body` is pinned in a test.
- [ ] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
