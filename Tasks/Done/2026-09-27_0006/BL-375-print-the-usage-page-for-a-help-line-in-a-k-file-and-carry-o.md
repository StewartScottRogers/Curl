---
id: BL-375
title: Print the usage page for a help line in a -K file and carry on, as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-201]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-375 — Print the usage page for a help line in a -K file and carry on, as curl does

## Goal

A `help` line in a `-K` file makes the parse report the usage page (or the named subject's page) for printing and carry on reading, as curl 8.21.0 does.

## Context

- Measured 2026-09-27 on the mingw curl 8.21.0: a config file holding `help` prints the usage page to standard output and then carries on, so `curl -K cfg` prints the page, then `curl: (2) no URL specified` and exits 2; `help = "all"` prints the `--help all` page first; a file holding `-h` then an unknown option prints the page and then the unknown-option refusal; `curl -K cfg -V` with `--help` in the file prints the page and then the version.
- BL-201 ignores `help` in a `-K` file (`CommandLineParser.ApplyConfigFileLine` calls `ForgetInformationRequests`), which differs from curl only in the missing page. The parse result needs a list of help pages to print before anything else, which the console (BL-376 or its successor) writes.

## Acceptance criteria

- [x] A parse of `-K cfg` with `help` in the file reports one usage-page request to print and still refuses with `curl: (2) no URL specified`.
- [x] `help = "all"` in the file reports the `all` subject; `--help` in the file followed by `-V` on the command line reports the page and asks for the version.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- The parse result carries `ConfigFileHelpSubjects`: the subject of each `help` / `-h` line read from a `-K` file or `.curlrc`, in order, `null` for the usage page, on accepted and refused results alike. `CommandLineOptions.MoveConfigFileInformationRequests` (replacing `ForgetInformationRequests`) moves a -K help request there and still drops `version` and `manual`. Printing the pages is the console's job (BL-376).
- Measured 2026-09-27 with the local curl: `.curlrc` holding `help` and no arguments prints the usage page and then the try-help line, so `RefusedEmpty` keeps the subjects too.
- ADR-0069 still says the -K help line is ignored until BL-375; it lies outside this task's touches, so align-and-document should update it.
- Tests: `CommandLineHelpAndManualOptionTests` (help in -K reported with and without a URL, `help = "all"`, `--help` then `-V`, `-h` then an unknown option, order across two files) and `CommandLineDefaultConfigFileTests.Parse_EmptyCommandLineWithHelpInDefaultConfigFile_ReportsTheUsagePageThenRefuses`. Curl.Cli: 100% line and branch, 0 failing members; the one failing member solution-wide is the pre-existing `DiskWriteOutFileOpener.TryOpen` in Curl.Console.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A help line in a -K file or .curlrc is reported in CommandLineParseResult.ConfigFileHelpSubjects and parsing carries on
