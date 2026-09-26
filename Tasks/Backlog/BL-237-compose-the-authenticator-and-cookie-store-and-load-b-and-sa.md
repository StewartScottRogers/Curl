---
id: BL-237
title: Compose the authenticator and cookie store and load -b and save -c in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-218, BL-221, BL-181, BL-182, BL-192, BL-193, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-237 — Compose the authenticator and cookie store and load -b and save -c in Curl.Console

## Goal

`Curl.Console` composes `Curl.Authentication` and `Curl.Cookies` into the HTTP handler, loads `-b` before the first transfer and writes `-c` after the last.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W8. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-231 added as a dependency beyond the plan: the handler must be registered first.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `-u`, `--digest`, `--anyauth` and `--oauth2-bearer` reach the authenticator; tests over a fake connector.
- [ ] `-b file`, `-b 'a=1'`, `-c jar` and `-j` behave as measured on curl 8.21.0.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W8 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
