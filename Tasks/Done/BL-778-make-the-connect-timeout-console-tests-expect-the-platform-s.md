---
id: BL-778
title: Make the connect-timeout Console tests expect the platform's line ending
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-778 — Make the connect-timeout Console tests expect the platform's line ending

## Goal

The CI workflow's Linux and macOS fast-test jobs pass again on `work/dark-factory`, so the shift can merge into `master`.

## Context

- CI run 36483047387 (2026-09-28): `Build and test (ubuntu-latest)` and `(macos-latest)` fail on two tests BL-510 added in `Curl.Console.UnitTests/CurlCommandRunnerConnectTimeoutTests.cs`; Windows passes.
- Both assert `curl: (28) Connection timed out after 1000 milliseconds\r\n`; off Windows the runner writes `\n`. The neighbouring `CurlCommandRunnerStandardErrorFileTests` expect `Environment.NewLine`.

## Acceptance criteria

- [x] `RunAsync_DictToAStalledDialWithConnectTimeout_EndsWithExit28AndCurlsMessage` and `RunAsync_DictToAStalledDialWithMaxTimeBelowConnectTimeout_EndsAtTheMaxTime` expect `Environment.NewLine`, not a literal `\r\n`.
- [x] `dotnet build` is clean and the fast tests pass on Windows.

## Notes

- 2026-09-28: Both asserts now append `Environment.NewLine`; `dotnet build` clean and every fast test passes on Windows. Linux and macOS are confirmed by the next CI run.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Connect-timeout tests expect Environment.NewLine
