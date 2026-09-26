---
id: BL-225
title: Supply the -w transfer variables from TransferReport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-224, BL-160]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-225 — Supply the -w transfer variables from TransferReport

## Goal

The renderer's variable source reads response, size, count, URL, method, scheme, IP, error message and exit code variables from `TransferReport` and the transfer result.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item O2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- The BL-158 ADR maps each field to its variables.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Each variable's formatting is measured on curl 8.21.0 and pinned.
- [ ] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Plan item: O2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
