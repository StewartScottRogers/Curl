---
id: BL-199
title: Expand --variable and {{name}} with the --expand- options
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-197]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-199 — Expand --variable and {{name}} with the --expand- options

## Goal

`--variable` defines variables and `--expand-<option>` expands `{{name}}` with curl's functions.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C13. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- https://curl.se/docs/manpage.html#--variable (curl 8.21.0).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Every function (`trim`, `json`, `url`, `b64`, `64dec`) and every error case is measured on curl 8.21.0 and pinned.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C13 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
