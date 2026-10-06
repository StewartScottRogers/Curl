---
id: BL-374
title: Print --help <option>'s manual section, and refuse an unknown option name as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-201]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-374 — Print --help <option>'s manual section, and refuse an unknown option name as curl does

## Goal

`--help <option>` (`--help -v`, `--help --verbose`, `--help --no-verbose`) prints that option's section of the manual as curl 8.21.0 does, and an unknown option name prints `Incorrect option name to show help for, see curl -h` on standard error.

## Context

- curl 8.21.0's `tool_help` (src/tool_help.c at tag curl-8_21_0): a subject starting with `-` is looked up with `findlongopt` (after dropping a `--no-` prefix, which only a boolean option may carry) or `findshortopt` for a single letter; the manual text after `
ALL OPTIONS
` is scanned for `
    -<letter>, --`, `
    --no-<name>` (a negatable option without a letter) or `
    <subject>`, and printed up to the next `
    -` (or `
FILES` for `--xattr`, the last option). Measured: `curl --help -v` and `curl --help --no-verbose` print the `-v, --verbose` section; `curl --help --bogus` and `curl --help --` print the Incorrect-option line on standard error and exit 0.
- BL-201 added `CurlManual.Lines()` (the embedded manual) and `CurlHelpText.IsOptionSubject`; `CurlHelpText.Lines` throws for an option subject today. The lookup needs curl's full option list with each option's letter and whether it is boolean and negatable, which `CommandLineOptionTable` does not hold for options it does not parse - add that list beside `CurlHelpTable` (ADR-0069).

## Acceptance criteria

- [x] For every option in curl 8.21.0's list, `--help --<name>` (and `-<letter>` where it has one) gives lines byte-equal to the reference build's output (measured with a script; record the command in Notes), pinned in a test for at least `-v`, `--verbose`, `--no-verbose`, `--xattr` and one option with no letter.
- [x] `--help --bogus`, `--help --`, `--help -` and `--help --no-output` give the Incorrect-option line for standard error and no standard-output lines.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Delivered in `Curl.Cli.UnitLibrary`: `CurlOptionAliasTable` (curl's `aliases` array: name, letter,
  `CurlOptionNoPrefix` = not accepted / accepted (`ARG_BOOL`) / documented (`ARG_NO`)) and
  `CurlOptionManualSection.TryGetLines(subject, out lines)`, a port of `tool_help`'s option branch and
  `helpscan`. `false` means curl writes `IncorrectOptionNameMessage` to stderr and exits 0; `true` with no
  lines means curl knows the name but the manual has no heading for it (`--help --no-compressed`,
  `--help --include`, 88 subjects in all), where curl prints nothing. Writing either from `Curl.Console` is
  BL-376's job; `CurlHelpText.Lines` still throws for an option subject.
- Source: `src/tool_help.c`, `src/tool_getparam.c` and `.h` at tag `curl-8_21_0`, fetched from
  raw.githubusercontent.com. The table is the release build's: the `DEBUGBUILD` rows `test-duphandle`,
  `test-event` and the `USE_WATT32` row `wdebug` are left out, because the reference build refuses them
  (280 rows). Lookup is exact and case-sensitive (curl's `bsearch` with `strcmp`), so `--VERBOSE` and
  `--verb` are refused.
- Measured with a throwaway PowerShell script (not committed) that, for every alias, ran
  `& 'C:\Program Files\Git\mingw64\bin\curl.exe' --help <subject>` for `--<name>`, `--no-<name>` and
  `-<letter>`, plus `--bogus`, `--`, `-`, `-vv`, `-?`, `--VERBOSE`, `--no-`, `--no-no-buffer`, `--verb`,
  capturing stdout bytes, stderr and the exit code through `System.Diagnostics.Process` (634 subjects, all
  exit 0). `Record-CurlExchange.ps1` was not used: these runs make no connection, and its listener made
  634 runs take over ten minutes. Results are pinned in
  `Curl.Cli.UnitTests/HelpReference/help-option-measurements.txt` (byte count, SHA-256, stderr kind per
  subject) and checked by `TryGetLines_EveryMeasuredSubject_GivesTheMeasuredBytes`; full sections are
  pinned for `-v`, `--verbose`, `--no-verbose`, `--xattr`, `--cert-status`, `--keepalive`, `--no-keepalive`.
- No new ADR: ADR-0069 already decides that this list sits beside `CurlHelpTable`, and matching the
  reference build decides the rest.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` gave 100% line, 100% branch,
  0 failing members, worst CRAP 10. `dotnet format --verify-no-changes` reports only ENDOFLINE on files
  that were already in the branch (a line-ending setting of this checkout), nothing in the new code.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --help <option> gives curl 8.21.0's manual section for all 634 measured subjects, and an unknown name reports the Incorrect-option line
