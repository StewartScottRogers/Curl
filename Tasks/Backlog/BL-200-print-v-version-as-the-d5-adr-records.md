---
id: BL-200
title: Print -V/--version as the D5 ADR records
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-155]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-200 — Print -V/--version as the D5 ADR records

## Goal

`-V`/`--version` prints the lines the BL-155 ADR records for the running OS and exits 0.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C14. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-155 ADR has the exact lines per OS and the rule for `Protocols:` and `Features:`.

## Acceptance criteria

- [ ] Output is byte-equal to the BL-155 ADR's lines for Windows, Linux and macOS (OS injected in tests).
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C14 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
