---
id: BL-217
title: Answer Digest challenges in Curl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-161, BL-151]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-217 — Answer Digest challenges in Curl.Authentication

## Goal

Digest authentication answers challenges with MD5, SHA-256, SHA-512-256, the `-sess` variants, `qop=auth` and `userhash`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item A2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- RFC 7616. The cnonce source is injected.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] RFC 7616 section 3.9 test vectors pass, and one measured curl 8.21.0 exchange is reproduced with its cnonce injected.
- [ ] `dotnet build Curl.Authentication.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Authentication`.

## Notes

- Plan item: A2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
