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
completed:
---
# BL-375 — Print the usage page for a help line in a -K file and carry on, as curl does

## Goal

A `help` line in a `-K` file makes the parse report the usage page (or the named subject's page) for printing and carry on reading, as curl 8.21.0 does.

## Context

- Measured 2026-09-27 on the mingw curl 8.21.0: a config file holding `help` prints the usage page to standard output and then carries on, so `curl -K cfg` prints the page, then `curl: (2) no URL specified` and exits 2; `help = "all"` prints the `--help all` page first; a file holding `-h` then an unknown option prints the page and then the unknown-option refusal; `curl -K cfg -V` with `--help` in the file prints the page and then the version.
- BL-201 ignores `help` in a `-K` file (`CommandLineParser.ApplyConfigFileLine` calls `ForgetInformationRequests`), which differs from curl only in the missing page. The parse result needs a list of help pages to print before anything else, which the console (BL-376 or its successor) writes.

## Acceptance criteria

- [ ] A parse of `-K cfg` with `help` in the file reports one usage-page request to print and still refuses with `curl: (2) no URL specified`.
- [ ] `help = "all"` in the file reports the `all` subject; `--help` in the file followed by `-V` on the command line reports the page and asks for the version.
- [ ] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

## Log

- 2026-09-27: Created.
