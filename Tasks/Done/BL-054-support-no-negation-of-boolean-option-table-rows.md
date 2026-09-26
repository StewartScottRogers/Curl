---
id: BL-054
title: Support --no- negation of boolean option table rows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-054 — Support `--no-` negation of boolean option table rows

## Goal

`--no-<name>` turns off a boolean option that curl 8.21.0 lets be negated (for example
`--no-silent`), and a `--no-` spelling of any option curl does not let be negated is
refused with curl's exact stderr lines and exit 2.

## Context

Follow-up of BL-037, whose Notes put `--no-` negation out of scope. The parser in
`Curl.Cli.UnitLibrary` is table-driven: `CommandLineOptionTable.Rows` holds
`CommandLineOption` rows built with `Flag`, `Text` or `Value`; a `Flag` row's setter
today can only turn its property on (`options => options.Silent = true`). The table
currently holds `--url`, `-s`/`--silent`, `-S`/`--show-error` and `-o`/`--output`.
Refusals come from `CommandLineRefusal`; today `--no-silent` is refused as
`curl: option --no-silent: is unknown`.

The manual (<https://curl.se/docs/manpage.html>, checked for curl 8.21.0) marks boolean
options that can be negated with `--no-`; not every boolean can be. What is not yet
known, and must be measured with the local curl 8.21.0 before coding:

- whether `--no-silent` and `--no-show-error` are accepted, and what they do when the
  positive form was given earlier (`-s --no-silent`) or later (`--no-silent -s`);
- the exact stderr lines and exit code for `--no-` in front of a value option
  (`--no-output`) and in front of an unknown name (`--no-bogus`);
- whether a `--no-` spelling of a non-negatable boolean exists among the rows in the
  table; if none does, measure one outside the table and record it for later rows.

Record every measurement, with the command used, in this task's Notes and in the XML
documentation of the table.

## Acceptance criteria

- [x] A `Flag` row declares whether it may be negated, and the XML documentation on
      `CommandLineOptionTable` says how a new row opts in.
- [x] Tests in `Curl.Cli.UnitTests` assert that `-s --no-silent URL` leaves `Silent`
      false and `--no-silent -s URL` leaves it true, and the same for `--show-error`,
      if the local curl 8.21.0 accepts both; otherwise the tests assert the measured
      refusal.
- [x] A test asserts the exact stderr lines and `CurlExitCode.FailedInit` for
      `--no-output`, matching the measured curl 8.21.0 output byte for byte.
- [x] A test asserts the exact stderr lines for `--no-bogus`, matching the measured
      output.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

Measured with the local curl 8.21.0 (Git for Windows mingw64 build) on 2026-09-26, as
`curl <arguments> http://127.0.0.1:1/ >/dev/null`, reading standard error and `$?`:

- `-s --no-silent URL`: the connect error is printed, exit 7, so `--no-silent` turns
  silence off. `--no-silent -s URL`: nothing printed, exit 7, so the last spelling wins.
- `-S --no-show-error URL` prints the error (not silent anyway); `-s -S --no-show-error URL`
  prints nothing; `-s --no-show-error -S URL` prints the error. `--no-show-error` is accepted
  and the last spelling wins.
- `--no-insecure` and `--no-tftp-no-options` are accepted (exit 7).
- `--no-silent=x URL` is accepted, the value ignored, as `--silent=x` is.
- `--no-output x URL`, `--no-output` alone, `--no-url`, `--no-data`, `--no-range`,
  `--no-tlsv1.2`, `--no-tlsv1.3`: exit 2 with
  `curl: option --no-output: the given option cannot be reversed with a --no- prefix` then
  the try-help line. `--no-output=x` names the whole argument: `option --no-output=x:`.
  So the refusal is checked before a value is taken, and non-negatable booleans exist in
  the table (`--tlsv1.2`, `--tlsv1.3`).
- `--no-bogus`, `--no-`, `--no-no-silent`, `--no-Silent`: exit 2, `curl: option <as typed>: is unknown`.
- `--no-silent` alone: `curl: (2) no URL specified`, exit 2.

Choices:

- A negatable flag is a new factory, `CommandLineOption.NegatableFlag(name, letter,
  (options, on) => ...)`; `Flag` stays and means "cannot be negated". The row exposes
  `Action<CommandLineOptions>? Negate` (null = not negatable) rather than a bool plus a
  method, so the parser has one branch and no unreachable path. Rows switched to
  `NegatableFlag`: `silent`, `show-error`, `insecure`, `tftp-no-options`, as measured.
- The parser tries the exact long name first and only then strips one `no-`, so a future
  row whose own name starts with `no-` still matches directly.
- `CommandLineRefusal.CannotBeReversed` carries the new text.
- `Documentation/Wiki/Command-Line-Parsing.md` and FR-046 in `Documentation/Product/Requirements.md`
  still say `--no-` is unimplemented; they are outside this task's `touches`, so BL-100
  is filed to fix them.
- Tests: `CommandLineNegationTests` (parser), plus `NegatableFlag`/`Negate` in
  `CommandLineOptionTests` and `CannotBeReversed` in `CommandLineRefusalTests`.
  `Curl.Cli.UnitTests`: 468 passed.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --no-silent, --no-show-error, --no-insecure and --no-tftp-no-options turn their flag off; other --no- spellings are refused as curl 8.21.0 does
