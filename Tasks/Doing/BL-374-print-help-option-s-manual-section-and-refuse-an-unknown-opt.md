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
completed:
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

- [ ] For every option in curl 8.21.0's list, `--help --<name>` (and `-<letter>` where it has one) gives lines byte-equal to the reference build's output (measured with a script; record the command in Notes), pinned in a test for at least `-v`, `--verbose`, `--no-verbose`, `--xattr` and one option with no letter.
- [ ] `--help --bogus`, `--help --`, `--help -` and `--help --no-output` give the Incorrect-option line for standard error and no standard-output lines.
- [ ] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
