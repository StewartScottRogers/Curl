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
completed:
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

- [ ] A test in `Curl.Console.UnitTests` asserts that `RunAsync(["--connect-timeout", "10",
      "-m", "20", "tftp://127.0.0.1/f"])` hands the recorded handler a context whose
      `ConnectTimeout` is `TimeSpan.FromSeconds(10)` and `MaxTime` is
      `TimeSpan.FromSeconds(20)`.
- [ ] A test asserts both are `null` when neither option is given.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"`
      is green, and `Curl.Console` keeps 100% line and branch coverage.

## Notes

Filed 2026-09-26 while re-planning BL-075.

## Log

- 2026-09-26: Created.
