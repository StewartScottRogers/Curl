---
id: BL-122
title: Parse --connect-timeout and -m/--max-time into CommandLineOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-122 — Parse --connect-timeout and -m/--max-time into CommandLineOptions

## Goal

`CommandLineParser.Parse` accepts `--connect-timeout <fractional seconds>` and
`-m`/`--max-time <fractional seconds>` and exposes them as `CommandLineOptions.ConnectTimeout`
and `CommandLineOptions.MaxTime` (`TimeSpan?`, `null` when not given), refusing a bad value
exactly as curl 8.21.0 does.

## Context

- Prerequisite of BL-075 (TFTP retransmission): curl derives TFTP's RRQ `timeout` option,
  retry interval and retry count from the time left under these two options. Today no
  `.cs` file in the solution parses either; only a test in
  `Curl.Cli.UnitTests/CommandLineOptionTests.cs` uses `max-time` as a sample row name.
- Upstream (manpage checked 2026-09-26, documenting curl 8.23.0;
  <https://curl.se/docs/manpage.html#--connect-timeout>, <https://curl.se/docs/manpage.html#-m>):
  both take seconds and "accept decimal values using a dot as separator" (examples
  `--connect-timeout 20`, `--connect-timeout 3.14`); if given several times, the last value
  is used. Neither is a boolean, so neither accepts a `--no-` prefix.
- Add two `CommandLineOption.Value` rows to `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`
  (`connect-timeout` with no short name, `max-time` with `'m'`) and two properties with
  `internal set` to `Curl.Cli.UnitLibrary/CommandLineOptions.cs`, beside `MaxFileSize`.
  `CommandLineNumber` today parses integers only; a decimal reader is new. Parse with
  `CultureInfo.InvariantCulture` so a comma locale does not change the result.
- Refusals reuse the existing `CommandLineRefusal` factories
  (`ExpectedProperNumericalParameter`, `ExpectedPositiveNumericalParameter`,
  `TooLargeNumber`, `CannotBeReversed`) where curl's text matches one. The exact text
  and exit code curl 8.21.0 prints for `abc`, `-1`, `1,5` and an out-of-range value are
  not recorded anywhere in the repository: measure them with the local curl 8.21.0
  (`curl --connect-timeout <value> http://127.0.0.1:1/`, reading stderr and the exit code)
  and record them in `Notes` before pinning them. Do not assume.
- Wiring the values into a transfer is BL-123 (the contract) and BL-124 (`Curl.Console`),
  not this task.

## Acceptance criteria

- [ ] `Notes` records, measured with curl 8.21.0, the stderr lines and exit code for
      `--connect-timeout` and `-m` given `abc`, `-1`, `1,5`, and a value too large to
      represent, and whether `0` is accepted.
- [ ] Tests in `Curl.Cli.UnitTests` assert `--connect-timeout 10` gives
      `ConnectTimeout == TimeSpan.FromSeconds(10)`, `--connect-timeout 3.14` gives
      3140 ms, `-m 2.5` and `--max-time 2.5` give `MaxTime == 2500 ms`, the last of two
      values wins, and both are `null` when not given.
- [ ] A test per measured bad value asserts the refusal's `StandardErrorLines` and
      `ExitCode` equal the measured curl output.
- [ ] A test asserts `--no-connect-timeout` and `--no-max-time` are refused with
      `CommandLineRefusal.CannotBeReversed`'s text and exit 2, unless the measurement shows
      curl answers otherwise, in which case the test pins what curl does.
- [ ] `Curl.Cli.UnitLibrary/README.md` lists both options.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean, `dotnet test --filter
      "TestCategory!=Integration"` is green, and `Curl.Cli.UnitLibrary` keeps 100% line and
      branch coverage.

## Notes

Filed 2026-09-26 while re-planning BL-075, which was blocked because nothing carried
`--connect-timeout`.

## Log

- 2026-09-26: Created.
