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
completed: 2026-10-17
---
# BL-2600 — Fix flaky Curl.Console.UnitTests: different tests fail on each full fast run under lane load

## Goal

`dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes on every run, not only on some.

## Context

Found by BL-2598 on 2026-10-17 (lane 5, base 1e3c7d613). Two runs of the project with no Curl.Console change failed different tests each time: first 6 (among them `RunAsync_FtpUrlWithoutUserInformation_CarriesNoCredentials`, `RunAsync_SuccessToAnOutputFileOnWindows_WritesTheCodeWithCrLf`, `RunAsync_TraceAscii_WritesTheMeasuredDumpAndThePayloadsOnStandardOutput`), then 5 (`RunAsync_Http11AndAnH2EntryThenH1Entry_UsesTheH1OneAndWritesBothBack`, `RunAsync_TwoTransfers_DrawsEachOnesStatusLinesFromItsOwnReports`, `RunAsync_UserOptionWithAUserName_WinsAndNetrcIsNotRead ("")`, `:@ sends none`, `RunAsync_SuccessToStandardOutputOnWindows_WritesTheCodeAfterTheBodyWithALineFeed`). Failures that change from run to run point at state shared between parallel test methods (a process-wide console writer, environment variable or current directory). Start by finding what those tests share.

## Acceptance criteria

- [x] The shared state the failing tests race on is named under Notes and removed or isolated per test.
- [x] Three consecutive runs of `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` pass.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Shared state found: the only process-wide mutable state the project's tests touch is the process environment, which two tests set while the other ~2800 run in parallel (MSTestSettings.cs: method-level, all cores): `IPFS_GATEWAY` in `CurlCompositionTests.CreateRunner_IpfsUrl_ReadsTheGatewayFromTheProcessEnvironment` and `SSH_AUTH_SOCK` in `CurlCompositionSshVerboseTests.CreateRunner_ScpDownloadWithKeyboardInteractive_WritesTheAgentThenTheMethodAsMeasured`. Both now carry `[DoNotParallelize]`, so they run alone. Production code in Curl.Console, Curl.Cli, Curl.Engine, Curl.Output and Curl.Core has no mutable static fields, no Console.SetOut/SetError, no SetCurrentDirectory; the runner reads environment and files only through injected delegates in the failing tests.
Not reproduced: the original failures could not be reproduced on this lane. Before the change one run passed, then a run under full CPU load passed, then three concurrent runs under 2x CPU load passed; after it, three consecutive runs passed. The two earlier failing runs (BL-2598) listed tests that do not read IPFS_GATEWAY or SSH_AUTH_SOCK, so the env tests may not be the whole cause. If it recurs, the failing run's message text (not just names) is the next thing to capture; file a new task then.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 49f0379e a3b7ade7 3d836d5e.
- 2026-10-17: Doing -> Done. Isolated the two env-mutating tests; three consecutive runs pass
