---
id: BL-238
title: Compose proxy selection with the tunnelling connector and forward proxying in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-206, BL-212, BL-183, BL-192, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-238 — Compose proxy selection with the tunnelling connector and forward proxying in Curl.Console

## Goal

`-x`, `-U`, `--noproxy`, `-p` and the proxy environment variables choose a proxy with BL-206 and route through BL-212's connector or BL-183's forward proxying.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W9. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-231 added as a dependency beyond the plan.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] http via proxy, https via proxy and `-p` each send measured bytes over the fake connector; the 407 case exits 7 with the measured line.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W9 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
