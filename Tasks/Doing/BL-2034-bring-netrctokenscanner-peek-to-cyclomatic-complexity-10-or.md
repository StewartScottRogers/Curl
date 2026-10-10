---
id: BL-2034
title: Bring NetrcTokenScanner.Peek to cyclomatic complexity 10 or less
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2034 — Bring NetrcTokenScanner.Peek to cyclomatic complexity 10 or less

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports no failing member: `NetrcTokenScanner.Peek()` (`Curl.Authentication.UnitLibrary/NetrcTokenScanner.cs:184`) measures cyclomatic complexity 10 or less, with behaviour unchanged.

## Context

BL-2022's measurement on 2026-10-10 found `NetrcTokenScanner.Peek()` at complexity 12 (CRAP 12, 100% line and branch coverage), the library's only failing member, after BL-1986 (commit 65d0e6fc4) ended a netrc line at NUL. Extract a private method (for example the character classification) so the existing `NetrcFileTests` still pass unchanged. Never raise the threshold in `CodeMetricsConfig.txt`.

## Acceptance criteria

- [x] `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 0 failing members and 100% line and branch coverage.
- [x] Every existing `NetrcFile` test passes unchanged; `dotnet build -warnaserror` is clean and the fast tests are green.
- [x] No option changes, so `--ai-help` needs nothing.

## Notes

Extracted the NUL line skip from `Peek` into `SkipToEndOfLine`. Measured: 0 failing members, 100% line and branch.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
