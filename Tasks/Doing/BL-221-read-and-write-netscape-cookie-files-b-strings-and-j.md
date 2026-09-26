---
id: BL-221
title: Read and write Netscape cookie files, -b strings and -j
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-220]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-221 — Read and write Netscape cookie files, -b strings and -j

## Goal

The store loads Netscape cookie files and `-b` strings, drops session cookies under `-j`, and writes a jar byte-equal to curl's.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item Q3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] The jar header lines and `#HttpOnly_` prefix are byte-equal to curl 8.21.0's jar (measured).
- [ ] An unwritable jar gives the measured message.
- [ ] `dotnet build Curl.Cookies.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies`.

## Notes

- Plan item: Q3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
