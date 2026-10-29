---
id: BL-4092
title: Create Curl.Extension.Abstractions with extension command-line options
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4091]
touches: [Curl.Extension.Abstractions.UnitLibrary, Curl.Extension.Abstractions.UnitTests, Curl.Cli.UnitLibrary/CommandLineParser.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineGlobalState.cs, Curl.Cli.UnitLibrary/CommandLineExtensionOption.cs, Curl.Cli.UnitTests, Curl.Console/CurlComposition.cs, Curl.Extension.Composition.UnitLibrary/ExtensionProtocolHandlers.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionCommandLineOptions.cs, Curl.Extension.Composition.UnitLibrary/Curl.Extension.Composition.UnitLibrary.csproj, Curl.Extension.Composition.UnitLibrary/CLAUDE.md, Curl.Extension.Composition.UnitTests, Curl.slnx, .gitattributes, Tasks/SharedListFiles.txt]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4092 — Create Curl.Extension.Abstractions with extension command-line options

## Goal

An extension can add a long command-line option (one self-registering file) that the parser accepts only when curl itself has no option of that name and extensions are on.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". ADR-0999 names `Curl.Extension.Abstractions.UnitLibrary` as the contract for extensions that need more than a URL scheme. This task creates it with the first piece: option rows. Most extensions in the list are options (`--sse`, `--cache-dir`, `--npipe`...).

## Acceptance criteria

- [x] `Curl.Extension.Abstractions.UnitLibrary` and `.UnitTests` exist in `/Extensions/Abstractions/` in `Curl.slnx`.
- [x] An extension option row names the option, its argument kind and its one-line help; a test registers one through the catalogue and parses it to a value the extension reads at transfer time.
- [x] Composition fails, naming both, when an extension option has the name of a curl option or of another extension option (unit test).
- [x] With `CURL_EXTENSIONS=0` an extension option is refused exactly as curl refuses an unknown option (exit 2, same stderr bytes); unit test.
- [x] Every existing `Curl.Cli.UnitTests` and `Curl.Console.UnitTests` test passes unchanged.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decisions in ADR-1006. `Curl.Cli` may not reference an extension project, so it has its own
  `CommandLineExtensionOption`; the host maps `ExtensionOption` to it. Extension options are looked up only
  after curl's table misses, so curl's options always win, and with extensions off the host passes none, so
  the refusal is the ordinary unknown-option path (exit 2, same bytes) with no special case.
- Touches widened (no other Doing task names them): `CommandLineOptions.cs`, `CommandLineGlobalState.cs` and the
  new `CommandLineExtensionOption.cs` carry the parsed values; Composition gains `ExtensionOptionCatalogue.cs`
  (a new shared list file, so `.gitattributes` and `Tasks/SharedListFiles.txt` each gain one line),
  `ExtensionCommandLineOptions.cs`, a reference to Abstractions in its csproj, and its CLAUDE.md.
- Criterion 2 is met across three tests: the catalogue path in `ExtensionCommandLineOptionsTests`, the parse in
  `CommandLineExtensionOptionTests`, and the read in `ExtensionOptionValuesTests`. Wiring the Console's parse
  call (in `CurlCommandRunner.cs`, held by BL-4088) is filed as BL-4171; `CurlComposition.cs` needed no change.
- Measure-CodeQuality.ps1: Curl.Cli, Curl.Extension.Composition and Curl.Extension.Abstractions all 100% line
  and branch, 0 failing (the private `Parse` reached complexity 12; `CreateOptions` was extracted).
- Tests are platform-neutral (no paths read, no OS text), so CI on Linux and macOS is covered by the fast tests here.

Keep the contract minimal: later groundwork tasks add transfer, connection, resolver and certificate hooks in their own subfolders of the Abstractions project.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a1784791 6733a932 33e4531e 38c80764 51538dbb e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. Extension options parse where curl has none, are checked at composition and are refused as unknown with extensions off; Console wiring filed as BL-4171.
