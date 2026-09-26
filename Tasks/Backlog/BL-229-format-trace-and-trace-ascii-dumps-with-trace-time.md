---
id: BL-229
title: Format --trace and --trace-ascii dumps with --trace-time
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-163]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-229 — Format --trace and --trace-ascii dumps with --trace-time

## Goal

Trace formatters render `--trace` and `--trace-ascii` dumps, with `--trace-time` prefixes, from BL-163's events.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item O6. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Dumps for one HTTP exchange are byte-equal to curl 8.21.0 (measured), with timestamps from an injected clock.
- [ ] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Plan item: O6 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
