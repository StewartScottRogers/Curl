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
completed: 2026-09-26
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

- [x] `Notes` records, measured with curl 8.21.0, the stderr lines and exit code for
      `--connect-timeout` and `-m` given `abc`, `-1`, `1,5`, and a value too large to
      represent, and whether `0` is accepted.
- [x] Tests in `Curl.Cli.UnitTests` assert `--connect-timeout 10` gives
      `ConnectTimeout == TimeSpan.FromSeconds(10)`, `--connect-timeout 3.14` gives
      3140 ms, `-m 2.5` and `--max-time 2.5` give `MaxTime == 2500 ms`, the last of two
      values wins, and both are `null` when not given.
- [x] A test per measured bad value asserts the refusal's `StandardErrorLines` and
      `ExitCode` equal the measured curl output.
- [x] A test asserts `--no-connect-timeout` and `--no-max-time` are refused with
      `CommandLineRefusal.CannotBeReversed`'s text and exit 2, unless the measurement shows
      curl answers otherwise, in which case the test pins what curl does.
- [x] `Curl.Cli.UnitLibrary/README.md` lists both options.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean, `dotnet test --filter
      "TestCategory!=Integration"` is green, and `Curl.Cli.UnitLibrary` keeps 100% line and
      branch coverage.

## Notes

Filed 2026-09-26 while re-planning BL-075, which was blocked because nothing carried
`--connect-timeout`.

Measured 2026-09-26 with the local curl 8.21.0 (x86_64-w64-mingw32, Schannel), running
`curl <opt> <value> http://127.0.0.1:1/`; `--connect-timeout` and `-m` answer identically,
apart from the option name in the message:

| Value | stderr (then the try-help line) | Exit |
| --- | --- | --- |
| `abc`, `-1`, `+1`, ` 1`, `.5`, `nan`, `inf`, empty | `curl: option <opt>: expected a proper numerical parameter` | 2 |
| `99999999999999999999`, `9223372036854775807`, `2147483` | `curl: option <opt>: expected a proper numerical parameter` | 2 |
| `1.`, `5.`, `1.abc`, `1.-5`, `1.9999999999999999999` | `curl: option <opt>: too large number` | 2 |
| `1,5` | accepted as 1 s (the transfer then fails with 28) | - |
| `0` | accepted, meaning no limit (the transfer fails with 7) | - |
| `--no-connect-timeout`, `--no-max-time` | `curl: option --no-<name>: the given option cannot be reversed with a --no- prefix` | 2 |

- `1,5` is not refused: curl's `secs2ms` reads leading digits and ignores the rest, so
  `1,5`, `1e999` and `1 ` are 1 s and `0x10` is 0. There is no "value too large to
  represent" refusal distinct from the proper-number one: the whole seconds are capped at
  `LONG_MAX/1000 - 1`, which on Windows (32-bit `long`) is 2147482 (`2147482` accepted,
  `2147483` refused). Linux curl, with a 64-bit `long`, would accept more; the Windows
  measurement is pinned, as the task says to pin the local curl (choice: take the measured
  value rather than branch per platform, which is untestable at 100% branch coverage here).
- Milliseconds checked through `--libcurl` (`CURLOPT_CONNECTTIMEOUT_MS`/`CURLOPT_TIMEOUT_MS`):
  `3.14` 3140, `2.5` 2500, `1.123456789012` 1123, `0.99999999` 999, `12.3456789012345678`
  12345, `2147482.999` 2147482999, `0.0001` 0.
- Plan (pipeline `feature`, delivered in-session because the change is one small library):
  `CommandLineNumber.ParseSeconds` mirrors `secs2ms`; two `CommandLineOption.Value` rows and
  `CommandLineOptions.ConnectTimeout`/`MaxTime`. Pinned in
  `Curl.Cli.UnitTests/CommandLineTimeoutOptionTests.cs`. `Curl.Cli.UnitLibrary` stays at
  100% line and branch, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --connect-timeout and -m/--max-time parse into CommandLineOptions.ConnectTimeout/MaxTime with curl 8.21.0's refusals
