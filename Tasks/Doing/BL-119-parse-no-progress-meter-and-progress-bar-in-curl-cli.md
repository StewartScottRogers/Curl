---
id: BL-119
title: Parse --no-progress-meter and -#/--progress-bar in Curl.Cli
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-119 — Parse --no-progress-meter and -#/--progress-bar in Curl.Cli

## Goal

`CommandLineParser` accepts `--no-progress-meter` and `-#`/`--progress-bar` (and their `--no-` negations, as curl 8.21.0 does) and exposes them on `CommandLineOptions`, so Curl.Console can decide whether to print the progress meter.

## Context

Split out of BL-102, which cannot print or suppress curl's progress meter until the parser knows these options. Today `Curl.Cli.UnitLibrary` has `CommandLineOptions.Silent` (row `CommandLineOption.NegatableFlag("silent", 's', ...)` in `CommandLineOptionTable.cs`) but no `progress` option at all, so `--no-progress-meter` exits 2 as unknown. Upstream reference: https://curl.se/docs/manpage.html (`--no-progress-meter`, `-#, --progress-bar`). Check the negation behaviour (`--progress-meter` re-enables the meter; `--no-progress-bar`) against the local curl 8.21.0 before writing the rows, and record what was measured under Notes.

## Acceptance criteria

- [ ] Tests in `Curl.Cli.UnitTests` pin that `--no-progress-meter` parses and sets a `CommandLineOptions` property that says the meter is off, and that the last of `--no-progress-meter`/`--progress-meter` wins.
- [ ] Tests in `Curl.Cli.UnitTests` pin that `-#` and `--progress-bar` parse and set a `CommandLineOptions` property that says the bar form was chosen.
- [ ] Curl 8.21.0's behaviour for `--progress-meter` and `--no-progress-bar` is recorded under Notes with the commands used, and the tests match it.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
