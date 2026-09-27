---
id: BL-137
title: Bring CurlCommandRunner.TransferWithHeaderOutputAsync to cyclomatic complexity 10 or less
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-137 — Bring CurlCommandRunner.TransferWithHeaderOutputAsync to cyclomatic complexity 10 or less

## Goal

`CurlCommandRunner.TransferWithHeaderOutputAsync` in `Curl.Console/CurlCommandRunner.cs`
measures at cyclomatic complexity 10 or less, so `Curl.Console` has no failing member in
the quality audit, with behaviour unchanged.

## Context

- `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports
  `TransferWithHeaderOutputAsync` at complexity 12 (CRAP 12, 100% line and branch
  coverage). It is the only failing member in `Curl.Console`. The root `CLAUDE.md` holds
  every method to complexity 10 or less; the threshold in `CodeMetricsConfig.txt` is
  Stewart's and must not be raised.
- The method predates BL-079; it came from BL-121's `-D`/`--dump-header` work. It picks
  between no `-D`, `-D -` (standard output) and a `-D` file, opens the header file
  (truncate for the first URL, append after), reports an open failure, then calls
  `TransferAsync`.
- Likely shape: extract the `-D` file branch (open, failure report, transfer, dispose) into
  its own private method with a name that says what it does, leaving the dispatcher method
  to choose the branch.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports
      0 failing members, and every method it lists for `CurlCommandRunner` is at
      complexity 10 or less.
- [x] No test in `Curl.Console.UnitTests` is changed in its assertions; tests are added
      only if a new method is not already fully covered, keeping `Curl.Console` at 100%
      line and branch coverage.
- [x] `dotnet build Curl.Console -warnaserror` is clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] Every extracted method has an XML `<summary>` that states what it does.

## Notes

- Extracted the `-D` file branch (open, failure report, transfer, dispose) into `TransferWithHeaderFileAsync`; `TransferWithHeaderOutputAsync` now only chooses between no `-D`, `-D -` and a `-D` file. Existing tests cover the new method fully, so no test was added or changed.
- Measured after: `Measure-CodeQuality.ps1 -Library Curl.Console` reports 123 members, 0 failing, 100% line and branch, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. TransferWithHeaderOutputAsync is at complexity 10 or less; Curl.Console has 0 failing members
