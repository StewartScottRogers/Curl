---
id: BL-188
title: Parse the --data-* options, --json, -G and --url-query into CommandLineOptions
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-188 — Parse the --data-* options, --json, -G and --url-query into CommandLineOptions

## Goal

`--data-binary`, `--data-raw`, `--data-ascii`, `--data-urlencode`, `--json`, `-G`/`--get` and `--url-query` parse into `CommandLineOptions` with curl's encoding and joining.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `-d` and joining several `-d` with `&` exist (BL-038, BL-057, BL-080).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Every `--data-urlencode` form (`content`, `=content`, `name=content`, `@file`, `name@file`) encodes as measured on curl 8.21.0.
- [ ] `-G` moves the data into the query string (as measured), and `--url-query` appends as measured.
- [ ] How several `--json` values join is measured and pinned.
- [ ] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
