---
id: BL-239
title: Name output files for -O, -J, --output-dir and --create-dirs in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-194, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-239 — Name output files for -O, -J, --output-dir and --create-dirs in Curl.Console

## Goal

`-O`, `--remote-name-all`, `-J`, `--output-dir` and `--create-dirs` name and create output files as curl does.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W10. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Windows name sanitising exists (`WindowsOutputFileNameSanitizer`, BL-091).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Remote names from the URL path, Content-Disposition parsing under `-J`, and the refusals (no file name in URL, existing file under `-J`) match curl 8.21.0 (measured).
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W10 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
