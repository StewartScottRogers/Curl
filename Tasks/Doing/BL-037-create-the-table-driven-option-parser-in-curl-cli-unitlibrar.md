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
completed:
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

- [ ] `CommandLineParser.Parse(IReadOnlyList<string> arguments)` returns either a
      `CommandLineOptions` or a refusal with `CurlExitCode.FailedInit` and its stderr
      lines, and never throws for any argument array.
- [ ] Tests in `Curl.Cli.UnitTests` assert: `-sS` sets both flags; `-ofile` and
      `-o file` and `--output file` each set the output to `file`; `--url a b` collects
      `a` then `b`.
- [ ] Tests assert the exact two lines for `--bogus`, for an unknown short option, and
      for `-o` and `--output` given as the last argument (`requires parameter`, naming
      the option as spelled).
- [ ] A test asserts the numeric helper refuses `abc` for `--tftp-blksize` with exactly
      `curl: option --tftp-blksize: expected a proper numerical parameter` and the `try`
      line.
- [ ] Adding an option is a new table row plus its model property; the XML
      documentation on the table says so.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

`--no-` negation, `-K`/config files, `.curlrc`, `--variable` and `--next` are out of
scope; file them as tasks if the design here makes any of them awkward.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
