---
id: BL-207
title: Expand URL globs and #N in -o
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-207 — Expand URL globs and #N in -o

## Goal

URL globs (`{a,b}`, `[1-10]`, `[01-10]`, `[a-z:2]`) expand in curl's order, `#N` in `-o` substitutes, and `-g` turns globbing off.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K5. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-031 (globs in `-T`) waits on BL-030 and may reuse this expander.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Each glob form and `#N` substitution is tested against curl 8.21.0's expansion.
- [ ] A bad glob returns `CurlExitCode.UrlMalformat` (3) with the measured `bad range ... position N` message.
- [ ] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K5 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
