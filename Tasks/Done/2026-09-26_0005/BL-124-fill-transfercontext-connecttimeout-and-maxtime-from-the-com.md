---
id: BL-124
title: Fill TransferContext.ConnectTimeout and MaxTime from the command line in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-122, BL-123]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-124 — Fill TransferContext.ConnectTimeout and MaxTime from the command line in Curl.Console

## Goal

`curl --connect-timeout 10 -m 20 <url>` hands the protocol handler a `TransferContext`
whose `ConnectTimeout` is 10 s and `MaxTime` is 20 s.

## Context

- BL-122 parses the options into `CommandLineOptions.ConnectTimeout` and
  `CommandLineOptions.MaxTime`; BL-123 adds `TransferContext.ConnectTimeout` and
  `TransferContext.MaxTime`.
- `Curl.Console/CurlCommandRunner.cs`, method `CreateContext`, builds the `TransferContext`
  from `CommandLineOptions` (it already copies `MaxFileSize`, `TftpBlockSize` and so on).
  Copy the two new values there.
- `Curl.Console.UnitTests/RecordingProtocolHandler.cs` records the context a handler
  receives; the existing option tests in
  `Curl.Console.UnitTests/CurlCommandRunnerTransferOptionTests.cs` show the pattern.
- Enforcing the timeouts is each handler's job (BL-075 for TFTP); applying them to the TCP
  and UDP connectors in `Curl.Networking.UnitLibrary` is not in scope here.

## Acceptance criteria

- [x] A test in `Curl.Console.UnitTests` asserts that `RunAsync(["--connect-timeout", "10",
      "-m", "20", "tftp://127.0.0.1/f"])` hands the recorded handler a context whose
      `ConnectTimeout` is `TimeSpan.FromSeconds(10)` and `MaxTime` is
      `TimeSpan.FromSeconds(20)`.
- [x] A test asserts both are `null` when neither option is given.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"`
      is green, and `Curl.Console` keeps 100% line and branch coverage.

## Notes

Filed 2026-09-26 while re-planning BL-075.

- Delivered directly rather than through the full `/feature` stages: the change is two
  property copies in `CurlCommandRunner.CreateContext`, with no design choice for
  `protocol-architect` to make.
- Tests sit beside the other context-copy tests in `CurlCommandRunnerTests.cs`
  (`RunAsync_ConnectTimeoutAndMaxTime_ReachTheHandlersContext`,
  `RunAsync_NoConnectTimeoutOrMaxTime_ContextCarriesNull`), not in
  `CurlCommandRunnerTransferOptionTests.cs`, which pins range/resume/max-filesize bytes.
- Coverage: the two new lines are branch-free and inside an object initializer every
  transfer runs, so Curl.Console's line and branch coverage are unchanged at 100%.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --connect-timeout and -m reach the handler's TransferContext as ConnectTimeout and MaxTime
