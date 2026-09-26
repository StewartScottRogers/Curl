---
id: BL-242
title: Wire -v and --trace output in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-228, BL-229, BL-195, BL-173, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-242 — Wire -v and --trace output in Curl.Console

## Goal

`-v`, `--trace`, `--trace-ascii`, `--trace-time` and `--stderr` route BL-163 events through BL-228/BL-229 formatters to the right stream.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W13. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-231 added as a dependency beyond the plan.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `-v` over a fake HTTP exchange writes the measured lines to stderr; `--trace file` writes the measured dump.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W13 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
