---
id: BL-243
title: Apply -K files and .curlrc before the command line in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-197, BL-198, BL-230]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-243 — Apply -K files and .curlrc before the command line in Curl.Console

## Goal

`Curl.Console` reads `.curlrc` (unless `-q`) and `-K` files with injected readers so their options apply in curl's order.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W14. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-230 added as a dependency beyond the plan.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] A `.curlrc` in a fake home and a `-K` file each change a transfer as measured on curl 8.21.0.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W14 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
