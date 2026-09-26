---
id: BL-235
title: Print -w output after each transfer in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-225, BL-226, BL-194, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-235 — Print -w output after each transfer in Curl.Console

## Goal

`-w` renders after each transfer to stdout or stderr as the template says, also after a failure.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W6. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `-w '%{http_code}\n'` after success and after a failure matches curl 8.21.0 (measured), including stream choice.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W6 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
