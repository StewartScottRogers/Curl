---
id: BL-4093
title: Add --extensions and --ai-help extensions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4092]
touches: [Curl.Extension.Abstractions.UnitLibrary/Description, Curl.Extension.Abstractions.UnitTests/Description, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CurlHelpTable.cs, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary/CurlAiHelpText.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineGlobalState.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionListing.cs, Curl.Extension.Composition.UnitLibrary/CLAUDE.md, Curl.Extension.Composition.UnitTests/ExtensionListingTests.cs, Curl.Mcp.UnitLibrary/McpOptionPathTable.cs, .gitattributes, Tasks/SharedListFiles.txt]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4093 — Add --extensions and --ai-help extensions

## Goal

`curl --extensions` lists every compiled-in extension with its schemes and options, and `curl --ai-help extensions` documents each one in Markdown, both built from the extensions' own descriptions.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". ADR-0999 keeps `curl -V` unchanged, so extensions need their own listing. Every extension task after this one supplies a description and is listed automatically.

## Acceptance criteria

- [x] An extension supplies a description (name, summary, schemes, options, specification link) through `Curl.Extension.Abstractions`; a test extension's description appears in both outputs.
- [x] `curl --extensions` prints one line per extension; with `CURL_EXTENSIONS=0` it prints that extensions are off and exits 0.
- [x] `curl --ai-help` lists `extensions` as a category, and `curl --ai-help extensions` prints each extension's section.
- [x] `curl -V` output is byte-for-byte unchanged (existing tests pass).
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

The option `--extensions` is Curl's own: record in an ADR that it is not a curl option, so the gap office ignores it.

- Plan (ADR-1008): `ExtensionDescription` in `Curl.Extension.Abstractions.Description`; `ExtensionDescriptionCatalogue` (shared list, merge=union) and `ExtensionListing` in Composition build both outputs; the console asks `ExtensionListing`, so `Curl.Cli` still references no extension project. The test extension's description is `ExtensionListingTests`' (the catalogue is empty until an extension lands).
- Decision: `--extensions` is left out of `--help` (unlike `--ai-help`'s row) so curl's pinned help pages stay byte for byte; it is in `--ai-help`'s Curl category instead, and the index lists `extensions` beside the categories.
- Touches widened (no Doing task on work/dark-factory held them): the Cli files the flag needs, Composition (where the catalogue must live), `McpOptionPathTable.cs` (its classification test lists every option) and the shared-list registrations.
- Measure-CodeQuality (Extension.Abstractions, Extension.Composition, Cli, Console): all four at 100% line; one member outside a gate, `CurlComposition.CreateTransferDispatch` branch 83.33%, on a line from f7b52c9e0 (BL-4092), not this task's, and already filed as BL-4083. The run's tests (every project reaching Console) passed; platform-neutral (newlines from Environment.NewLine).
## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: bb166f31 34fde2bb 99a0caca 8b541e9f e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. curl --extensions and --ai-help extensions list every extension from its own description
