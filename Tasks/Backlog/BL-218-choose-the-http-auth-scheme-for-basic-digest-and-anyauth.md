---
id: BL-218
title: Choose the HTTP auth scheme for --basic, --digest and --anyauth
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-216, BL-217]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-218 — Choose the HTTP auth scheme for --basic, --digest and --anyauth

## Goal

The authenticator chooses among offered challenges as curl ranks them for `--basic`, `--digest` and `--anyauth`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item A3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] The choice among challenges matches curl's ranking (measured); unsupported schemes (for example NTLM, Negotiate) fall back as measured on curl 8.21.0.
- [ ] `dotnet build Curl.Authentication.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Authentication`.

## Notes

- Plan item: A3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
