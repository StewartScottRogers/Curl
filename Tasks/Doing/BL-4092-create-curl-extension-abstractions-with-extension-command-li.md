---
id: BL-4092
title: Create Curl.Extension.Abstractions with extension command-line options
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4091]
touches: [Curl.Extension.Abstractions.UnitLibrary, Curl.Extension.Abstractions.UnitTests, Curl.Cli.UnitLibrary/CommandLineParser.cs, Curl.Cli.UnitTests, Curl.Console/CurlComposition.cs, Curl.Extension.Composition.UnitLibrary/ExtensionProtocolHandlers.cs, Curl.Extension.Composition.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4092 — Create Curl.Extension.Abstractions with extension command-line options

## Goal

An extension can add a long command-line option (one self-registering file) that the parser accepts only when curl itself has no option of that name and extensions are on.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". ADR-0999 names `Curl.Extension.Abstractions.UnitLibrary` as the contract for extensions that need more than a URL scheme. This task creates it with the first piece: option rows. Most extensions in the list are options (`--sse`, `--cache-dir`, `--npipe`...).

## Acceptance criteria

- [ ] `Curl.Extension.Abstractions.UnitLibrary` and `.UnitTests` exist in `/Extensions/Abstractions/` in `Curl.slnx`.
- [ ] An extension option row names the option, its argument kind and its one-line help; a test registers one through the catalogue and parses it to a value the extension reads at transfer time.
- [ ] Composition fails, naming both, when an extension option has the name of a curl option or of another extension option (unit test).
- [ ] With `CURL_EXTENSIONS=0` an extension option is refused exactly as curl refuses an unknown option (exit 2, same stderr bytes); unit test.
- [ ] Every existing `Curl.Cli.UnitTests` and `Curl.Console.UnitTests` test passes unchanged.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

Keep the contract minimal: later groundwork tasks add transfer, connection, resolver and certificate hooks in their own subfolders of the Abstractions project.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a1784791 6733a932 33e4531e 38c80764 51538dbb e35aff77 3d836d5e.
