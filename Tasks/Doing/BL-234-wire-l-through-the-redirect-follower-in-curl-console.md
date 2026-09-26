---
id: BL-234
title: Wire -L through the redirect follower in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-203, BL-179, BL-190, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-234 — Wire -L through the redirect follower in Curl.Console

## Goal

`-L`, `--max-redirs`, `--post30x` and `--location-trusted` drive BL-203's redirect follower from `Curl.Console`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W5. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: `-L --max-redirs 0` exits 47 `curl: (47) Maximum (0) redirects followed`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `-L -i` prints every response's headers, as measured.
- [ ] `--max-redirs 0` exits 47 with the measured line.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W5 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
