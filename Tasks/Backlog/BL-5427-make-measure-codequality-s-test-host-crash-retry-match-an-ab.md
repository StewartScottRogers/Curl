---
id: BL-5427
title: Make Measure-CodeQuality's test host crash retry match an aborted run's summary
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-18
completed:
---
# BL-5427 — Make Measure-CodeQuality's test host crash retry match an aborted run's summary

## Goal

`Measure-CodeQuality.ps1` runs a test project once more when its test host crashed without a failing test, as BL-5416 meant.

## Context

Found by the shift-end review (ADR-0710) of the 2026-11-17 shift (code-reviewer and silent-failure-hunter agents), in BL-5416's change.
`Measure-CodeQuality.ps1:264-267`: `$hostCrashed` requires that no line matches `Failed!\s+-\s+Failed:`. `dotnet test` prints that summary line for an unsuccessful run, very likely including an aborted one with `Failed: 0` (`Failed!  - Failed:     0, Passed: ...`), so the retry would never run. Neither reviewer confirmed this against a real crash, and BL-5416 says the script was not run end to end. After a retry the first attempt's coverage files also stay in the results directory, so both attempts' coverage could be merged.

## Acceptance criteria

- [ ] The crash check is confirmed against a captured output of a real crashed run, and counts a run as crashed when it saw `Test host process crashed` and no failed test (for example only `Failed:\s+[1-9]` counts as a real failure).
- [ ] A retry removes or ignores the first attempt's coverage files.
- [ ] The script still runs clean with `powershell -NoProfile -File Measure-CodeQuality.ps1` and under `pwsh`.

## Notes

## Log

- 2026-11-18: Created.
