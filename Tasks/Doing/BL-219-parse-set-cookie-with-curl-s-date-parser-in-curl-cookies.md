---
id: BL-219
title: Parse Set-Cookie with curl's date parser in Curl.Cookies
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-151]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-219 — Parse Set-Cookie with curl's date parser in Curl.Cookies

## Goal

`Curl.Cookies.UnitLibrary` parses Set-Cookie values into cookies with curl's attribute handling and date parser.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item Q1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Attributes: Expires, Max-Age, Domain, Path, Secure, HttpOnly, `__Secure-`/`__Host-` prefixes.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Each attribute and prefix rule is measured on curl 8.21.0 (via `-c` jar output) and pinned.
- [ ] `dotnet build Curl.Cookies.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies`.

## Notes

- Plan item: Q1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
