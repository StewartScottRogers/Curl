---
id: BL-2600
title: Fix flaky Curl.Console.UnitTests: different tests fail on each full fast run under lane load
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2600 — Fix flaky Curl.Console.UnitTests: different tests fail on each full fast run under lane load

## Goal

`dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes on every run, not only on some.

## Context

Found by BL-2598 on 2026-10-17 (lane 5, base 1e3c7d613). Two runs of the project with no Curl.Console change failed different tests each time: first 6 (among them `RunAsync_FtpUrlWithoutUserInformation_CarriesNoCredentials`, `RunAsync_SuccessToAnOutputFileOnWindows_WritesTheCodeWithCrLf`, `RunAsync_TraceAscii_WritesTheMeasuredDumpAndThePayloadsOnStandardOutput`), then 5 (`RunAsync_Http11AndAnH2EntryThenH1Entry_UsesTheH1OneAndWritesBothBack`, `RunAsync_TwoTransfers_DrawsEachOnesStatusLinesFromItsOwnReports`, `RunAsync_UserOptionWithAUserName_WinsAndNetrcIsNotRead ("")`, `:@ sends none`, `RunAsync_SuccessToStandardOutputOnWindows_WritesTheCodeAfterTheBodyWithALineFeed`). Failures that change from run to run point at state shared between parallel test methods (a process-wide console writer, environment variable or current directory). Start by finding what those tests share.

## Acceptance criteria

- [ ] The shared state the failing tests race on is named under Notes and removed or isolated per test.
- [ ] Three consecutive runs of `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` pass.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 49f0379e a3b7ade7 3d836d5e.
