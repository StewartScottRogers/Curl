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
completed:
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

- [ ] A `Flag` row declares whether it may be negated, and the XML documentation on
      `CommandLineOptionTable` says how a new row opts in.
- [ ] Tests in `Curl.Cli.UnitTests` assert that `-s --no-silent URL` leaves `Silent`
      false and `--no-silent -s URL` leaves it true, and the same for `--show-error`,
      if the local curl 8.21.0 accepts both; otherwise the tests assert the measured
      refusal.
- [ ] A test asserts the exact stderr lines and `CurlExitCode.FailedInit` for
      `--no-output`, matching the measured curl 8.21.0 output byte for byte.
- [ ] A test asserts the exact stderr lines for `--no-bogus`, matching the measured
      output.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
