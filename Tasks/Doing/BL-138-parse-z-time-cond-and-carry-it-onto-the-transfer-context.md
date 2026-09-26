---
id: BL-138
title: Parse -z/--time-cond and carry it onto the transfer context
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: FR-009
created: 2026-09-26
completed:
---
# BL-138 — Parse -z/--time-cond and carry it onto the transfer context

## Goal

`curl -z <date>` and `curl --time-cond -<date>` are accepted on the command line and reach
the protocol handler as `TransferContext.TimeCondition`, so the `file://` handler's existing
time-condition logic runs from the real command line.

## Context

- `FileProtocolHandler` (`Curl.Protocol.File.UnitLibrary`) already honours
  `ITransferContext.TimeCondition` (BL-016, BL-017, BL-018, BL-100), and
  `Curl.Protocol.Abstractions.UnitLibrary/TimeCondition.cs` defines
  `TimeCondition(DateTimeOffset Value, TimeConditionKind Kind)`; its remarks say the
  command-line layer parses curl's date spellings into `Value`.
- Nothing does that yet: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` has no
  `time-cond` row, `CommandLineOptions` has no member for it, and
  `CurlCommandRunner.CreateContext` in `Curl.Console/CurlCommandRunner.cs` never sets
  `TimeCondition`. Today `-z` is rejected as an unknown option.
- Upstream (https://curl.se/docs/manpage.html#-z, curl 8.21.0): `-z`/`--time-cond <time>`
  takes a date expression; a leading `-` inverts it to "older than"; a string that is not a
  valid date is tried as a file name whose modification time is used. The accepted date
  spellings are those of `curl_getdate` (https://curl.se/libcurl/c/curl_getdate.html).
- Measured on curl 8.21.0 (Windows, 2026-09-26): `-z "1 Jan 2030"` and `-z "1 Jan 2000"`
  are both accepted (the first unmet, the second met, against a 2020 source file).
- The option sets model is shown by `-R`'s row
  (`CommandLineOption.NegatableFlag("remote-time", 'R', …)`) and its tests in
  `Curl.Cli.UnitTests/CommandLineRemoteTimeOptionTests.cs`.

## Acceptance criteria

- [ ] `CommandLineOptionTable` has a `time-cond` row with short name `z` taking a value, and
      `CommandLineOptions` exposes the parsed `TimeCondition?` (null when `-z` is absent).
- [ ] Tests in a new `Curl.Cli.UnitTests/CommandLineTimeConditionOptionTests.cs` show
      `-z "1 Jan 2030"` gives `IfModifiedSince` at 2030-01-01T00:00:00Z,
      `--time-cond "-1 Jan 2000"` gives `IfUnmodifiedSince` at 2000-01-01T00:00:00Z, and an
      RFC 1123 date (`"Sun, 06 Nov 1994 08:49:37 GMT"`) parses to that instant.
- [ ] The set of `curl_getdate` spellings supported is written in the XML remarks of the
      parsing method; spellings not supported are listed in `Notes` with a follow-up task
      filed for them.
- [ ] The file-name fallback (a value that is not a date and names an existing file uses
      that file's modification time) is either implemented through an injected file
      reader, with a test, or recorded in `Notes` with a follow-up task filed.
- [ ] What curl 8.21.0 prints and returns for a value that is neither a date nor an
      existing file is measured and recorded in `Notes` with the command used, and a test
      asserts that behaviour (warning text and exit code) byte for byte.
- [ ] `CurlCommandRunner.CreateContext` sets `TimeCondition` from the options; a test in
      `Curl.Console.UnitTests` named `RunAsync_TimeCond_PassesConditionToHandler` asserts
      the fake handler receives it.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` and `dotnet build Curl.Console -warnaserror`
      are clean; `dotnet test --filter "TestCategory!=Integration"` passes;
      `Measure-CodeQuality.ps1` reports no new failing member in `Curl.Cli` or `Curl.Console`.

## Notes

- Filed with BL-136 (no `-o` file for an unmet `-z`), which is tested without this task
  but only reachable end to end once this lands.
- Date parsing uses the BCL only; no parser package.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
