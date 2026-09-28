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
completed: 2026-09-26
---
# BL-119 — Parse --no-progress-meter and -#/--progress-bar in Curl.Cli

## Goal

`CommandLineParser` accepts `--no-progress-meter` and `-#`/`--progress-bar` (and their `--no-` negations, as curl 8.21.0 does) and exposes them on `CommandLineOptions`, so Curl.Console can decide whether to print the progress meter.

## Context

Split out of BL-102, which cannot print or suppress curl's progress meter until the parser knows these options. Today `Curl.Cli.UnitLibrary` has `CommandLineOptions.Silent` (row `CommandLineOption.NegatableFlag("silent", 's', ...)` in `CommandLineOptionTable.cs`) but no `progress` option at all, so `--no-progress-meter` exits 2 as unknown. Upstream reference: https://curl.se/docs/manpage.html (`--no-progress-meter`, `-#, --progress-bar`). Check the negation behaviour (`--progress-meter` re-enables the meter; `--no-progress-bar`) against the local curl 8.21.0 before writing the rows, and record what was measured under Notes.

## Acceptance criteria

- [x] Tests in `Curl.Cli.UnitTests` pin that `--no-progress-meter` parses and sets a `CommandLineOptions` property that says the meter is off, and that the last of `--no-progress-meter`/`--progress-meter` wins.
- [x] Tests in `Curl.Cli.UnitTests` pin that `-#` and `--progress-bar` parse and set a `CommandLineOptions` property that says the bar form was chosen.
- [x] Curl 8.21.0's behaviour for `--progress-meter` and `--no-progress-bar` is recorded under Notes with the commands used, and the tests match it.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Measured with the local curl 8.21.0 on 2026-09-26: `curl <arguments> --limit-rate 1M -o out.bin file:///Z:/.../big.bin` (3 MB random file), reading standard error and the exit code.
  - No option: the classic meter. `--no-progress-meter`: nothing. `--progress-meter`: meter. `--no-progress-meter --progress-meter`: meter. `--progress-meter --no-progress-meter`: nothing. So the upstream option is `progress-meter`, negatable, and the last spelling wins.
  - `-#`, `--progress-bar`, `--no-progress-bar -#`: the `####... 100.0%` bar. `--no-progress-bar`, `-# --no-progress-bar`: the classic meter. So `progress-bar` is negatable with short letter `#`, last spelling wins.
  - `-# --no-progress-meter` and `--no-progress-meter -#`: nothing in either order, so the two are independent settings and meter-off outranks the bar.
  - `--progress-meter=x`, `--no-progress-bar=x`, `--progress-bar=x`: accepted, value ignored (all exit 0). `-s -#` and `-#s`: nothing, exit 0. `--no-#` and `--Progress-bar`: exit 2, `curl: option <as typed>: is unknown` and the try-help line.
- Choice: two `NegatableFlag` rows, `progress-meter` (sets `CommandLineOptions.ProgressMeterOff = !on`) and `progress-bar`/`#` (sets `ProgressBar`). Kept as two independent properties rather than one enum, because curl keeps them independent and the console layer (BL-102) needs both plus `Silent` to decide what to print. Delivered directly rather than through the full multi-agent `/feature` stages: two table rows and two properties following the table's documented pattern, too small for a separate plan.
- Tests: `Curl.Cli.UnitTests/CommandLineProgressOptionTests.cs` (19 cases) and two rows in `CommandLineOptionTableTests`. Curl.Cli.UnitTests: 561 total, 559 in the fast run.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --no-progress-meter/--progress-meter and -#/--progress-bar/--no-progress-bar parse into CommandLineOptions.ProgressMeterOff and ProgressBar, matching curl 8.21.0
