---
id: BL-037
title: Create the table-driven option parser in Curl.Cli.UnitLibrary
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-037 — Create the table-driven option parser in Curl.Cli.UnitLibrary

## Goal

`Curl.Cli.UnitLibrary` has a table-driven `CommandLineParser` that turns an argument
array into a `CommandLineOptions` model or a refusal carrying exit 2 and curl 8.21.0's
exact stderr lines, and later tasks add options by adding table rows.

## Context

No option parser exists: `Curl.Cli.UnitLibrary` holds only `UploadUrl.cs`, and BL-027,
BL-013 and BL-030 each say they need a parser that no task creates. This task creates the
mechanism and a deliberately small first table; it does not wire `Curl.Console`.

Measured on 2026-09-26 with the local curl 8.21.0 (Release-Date 2026-06-24); exit 2 is
`CURLE_FAILED_INIT` (<https://curl.se/libcurl/c/libcurl-errors.html>), options per
<https://curl.se/docs/manpage.html>:

- `curl --bogus` prints `curl: option --bogus: is unknown` then
  `curl: try 'curl --help' or 'curl --manual' for more information`, exit 2.
- `curl -t` prints `curl: option -t: requires parameter` then the same `try` line,
  exit 2; `curl --data` prints `curl: option --data: requires parameter` - a value
  option at the end of the arguments names the option as it was spelled.
- `curl --tftp-blksize abc URL` prints
  `curl: option --tftp-blksize: expected a proper numerical parameter` then the `try`
  line, exit 2 - the numeric refusal BL-027 and the Phase 4 options reuse.

The first table holds: positional URLs and `--url <url>` (collected in command-line
order); the booleans `-s`/`--silent` and `-S`/`--show-error`; and the value option
`-o`/`--output <file>`. That is enough to prove bundling (`-sS`), an attached short
value (`-ofile`), a separate value (`-o file`) and both refusals. A numeric helper that
produces the "expected a proper numerical parameter" refusal is part of the mechanism,
tested directly, because no numeric option is in the first table.

The parser returns message lines without line terminators; which newline the console
writes is the console layer's decision.

## Acceptance criteria

- [x] `CommandLineParser.Parse(IReadOnlyList<string> arguments)` returns either a
      `CommandLineOptions` or a refusal with `CurlExitCode.FailedInit` and its stderr
      lines, and never throws for any argument array.
- [x] Tests in `Curl.Cli.UnitTests` assert: `-sS` sets both flags; `-ofile` and
      `-o file` and `--output file` each set the output to `file`; `--url a b` collects
      `a` then `b`.
- [x] Tests assert the exact two lines for `--bogus`, for an unknown short option, and
      for `-o` and `--output` given as the last argument (`requires parameter`, naming
      the option as spelled).
- [x] A test asserts the numeric helper refuses `abc` for `--tftp-blksize` with exactly
      `curl: option --tftp-blksize: expected a proper numerical parameter` and the `try`
      line.
- [x] Adding an option is a new table row plus its model property; the XML
      documentation on the table says so.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

`--no-` negation, `-K`/config files, `.curlrc`, `--variable` and `--next` are out of
scope; file them as tasks if the design here makes any of them awkward.

Delivered (feature pipeline: plan, tests first, implement, verify, review, conformance,
align-and-document, one fix round):

- Types, all in `Curl.Cli`: `CommandLineParser.Parse` returns a `CommandLineParseResult`
  (`IsAccepted`, `Options`, `Refusal`). `CommandLineOptionTable.Rows` holds
  `CommandLineOption` rows built with `Flag`, `Text` or `Value`. `CommandLineOptions` is the
  model. `CommandLineRefusal` carries exit 2 and the two stderr lines, with no terminators.
  `CommandLineNumber.ParseNonNegative` is the numeric helper.
- Choice: `--name=value` is accepted. curl 8.21.0 accepts `--output=file` (added in 8.16.0,
  per the manpage), and `--silent=x` is accepted with the value ignored. This follows the
  measured behaviour.
- Choice: the refusal names the whole argument as typed (`-s!x`, `--bogus=x`, `-so`), as
  measured.
- Choice: a lone `-` is refused as unknown. The first `--` ends option parsing.
- Choice: an empty value or an empty URL is refused with
  `blank argument where content is expected`, as measured.
- Choice: the blank check lives in the `Text` row, not in the parser. Numeric options get
  `expected a proper numerical parameter` for `""`, as curl does; this was a finding of the
  conformance audit.
- Choice: `OutputFiles` is a list, because curl pairs each `-o` with a URL in order.
- Choice: a null element in the argument list reads as `""`, which gives the blank refusal.
  A null list is a caller bug and throws `ArgumentNullException`.
- Choice: `-t` is refused as unknown. Real curl reads it as `--telnet-option`, which is not in
  the first table, and the test says so.
- Choice: numbers are capped at `int.MaxValue`, which matches Windows curl, where C `long` is
  32-bit. The cross-platform question is filed as BL-053 for Stewart. BL-027
  (`--continue-at`) needs a 64-bit reader and must not reuse this helper.
- Verified: `dotnet build -warnaserror` is clean. `dotnet format --verify-no-changes` is clean.
  Fast tests are green: 152 in Curl.Cli.UnitTests, 305 in the solution. Every `CommandLine*`
  type has 100% line and branch coverage.
- Follow-ups filed:
  - BL-050: refuse a command line with no URL.
  - BL-051: warn when a filename argument looks like a flag.
  - BL-052: warn when there are more output options than URLs.
  - BL-053: numeric ceiling (Stewart).
  - BL-054: `--no-` negation.
  - BL-055: wiki, glossary and requirements.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Curl.Cli.UnitLibrary has a table-driven CommandLineParser: -s/-S/-o/--url with bundling and --name=value, and curl 8.21.0's exact exit-2 refusals
