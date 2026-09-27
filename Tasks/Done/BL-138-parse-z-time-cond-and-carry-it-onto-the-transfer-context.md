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
completed: 2026-09-26
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

- [x] `CommandLineOptionTable` has a `time-cond` row with short name `z` taking a value, and
      `CommandLineOptions` exposes the parsed `TimeCondition?` (null when `-z` is absent).
- [x] Tests in a new `Curl.Cli.UnitTests/CommandLineTimeConditionOptionTests.cs` show
      `-z "1 Jan 2030"` gives `IfModifiedSince` at 2030-01-01T00:00:00Z,
      `--time-cond "-1 Jan 2000"` gives `IfUnmodifiedSince` at 2000-01-01T00:00:00Z, and an
      RFC 1123 date (`"Sun, 06 Nov 1994 08:49:37 GMT"`) parses to that instant.
- [x] The set of `curl_getdate` spellings supported is written in the XML remarks of the
      parsing method; spellings not supported are listed in `Notes` with a follow-up task
      filed for them.
- [x] The file-name fallback (a value that is not a date and names an existing file uses
      that file's modification time) is either implemented through an injected file
      reader, with a test, or recorded in `Notes` with a follow-up task filed.
- [x] What curl 8.21.0 prints and returns for a value that is neither a date nor an
      existing file is measured and recorded in `Notes` with the command used, and a test
      asserts that behaviour (warning text and exit code) byte for byte.
- [x] `CurlCommandRunner.CreateContext` sets `TimeCondition` from the options; a test in
      `Curl.Console.UnitTests` named `RunAsync_TimeCond_PassesConditionToHandler` asserts
      the fake handler receives it.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` and `dotnet build Curl.Console -warnaserror`
      are clean; `dotnet test --filter "TestCategory!=Integration"` passes;
      `Measure-CodeQuality.ps1` reports no new failing member in `Curl.Cli` or `Curl.Console`.

## Notes

- Filed with BL-136 (no `-o` file for an unmet `-z`), which is tested without this task
  but only reachable end to end once this lands.
- Date parsing uses the BCL only; no parser package.
- Plan (delivered in-session, no separate architect run: one row, one property, one parser
  class, one context line): `CommandLineOption.Value("time-cond", 'z', SetTimeCondition)`;
  `CommandLineOptions.TimeCondition`; `CurlDateParser.TryParse`, a port of libcurl 8.21.0's
  `parsedate`/`curl_getdate`; `CurlCommandRunner.CreateContext` passes it on.
- Measured on the local curl 8.21.0 (Windows, 2026-09-26) by bracketing a `file://` source's
  modification time (at the expected instant "not new enough", one second later transferred):
  every accepted spelling in `CurlDateParserTests` reads the instant pinned there. Two
  findings differ from older libcurl sources: zone names are matched case-sensitively (`gmt`,
  `cest`, `z` are illegal; `GMT`, `CEST`, `Z` work), and a time is libcurl's newer
  `match_time` (1-2 digit fields, no spaces, hour < 24, minute < 60, second <= 60, otherwise not
  a time at all), so `10: 20` is illegal.
- Not a date, neither date nor file: `curl -z notadate -o NUL file:///Z:/repos/Curl.lanes/lane-3/global.json`
  prints `Warning: Illegal date format for -z, --time-cond (and not a filename). ` (trailing
  space) and `Warning: Disabling time condition. See curl_getdate(3) for valid date syntax.`,
  transfers with no condition and exits 0; `-z -notadate` is the same; with `-s` nothing is
  printed. Pinned by `CommandLineTimeConditionOptionTests.Parse_TimeCondNotADate_*` and
  `CurlCommandRunnerTransferOptionTests.RunAsync_TimeCondNotADate_WarnsAndTransfersUnconditionally`.
- Unsupported `curl_getdate` range: a year outside 1-9999 (`1 Jan 099999999`), which curl
  computes in a 64-bit `time_t` and `DateTimeOffset` cannot hold; refused as not a date.
  Follow-up BL-247.
- File-name fallback deferred to follow-up BL-246: the applier signature carries only
  `pathExists` and `IDataFileReader`, and a modification-time seam is its own change. Until it
  lands, `-z <existing file>` warns and drops the condition. BL-246 also records curl's extra
  `Warning: Failed to get filetime: CreateFile failed: GetLastError 0x00000003` line, printed
  before the two lines for `-z ""` and `-z -` on Windows.
- Choice: `-z =<date>` (curl's `CURL_TIMECOND_LASTMOD`) is recorded as `IfModifiedSince`.
  `TimeConditionKind` has no third member, and for `file://` libcurl's
  `Curl_meets_timecondition` treats LASTMOD exactly as if-modified-since (measured: `-z "=1 Jan
  2030"` skips a 2026 file as not new enough). It only differs for HTTP, which does not yet
  send time-condition headers; revisit when it does.
- `--no-time-cond` is refused as not reversible (measured, exit 2), which a `Value` row already does.
- `Documentation/Product/Requirements.md` FR-009 is outside `touches`; its status text is left
  for the next docs pass.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -z/--time-cond parses curl_getdate dates (measured against curl 8.21.0), warns like curl for a non-date, and reaches handlers as TransferContext.TimeCondition
