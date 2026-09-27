---
id: BL-223
title: Refuse cookies set on a public suffix
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-220, BL-222]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-223 — Refuse cookies set on a public suffix

## Goal

The cookie store refuses a cookie whose Domain is a public suffix, using the embedded PSL snapshot.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item Q4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- The reference build has the PSL feature (BL-153).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] A cookie with `Domain=co.uk` is refused and one for `example.co.uk` accepted, matching curl 8.21.0 (measured).
- [ ] `dotnet build Curl.Cookies.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies`.

## Notes

- Plan item: Q4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
