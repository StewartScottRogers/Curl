---
id: BL-4093
title: Add --extensions and --ai-help extensions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4092]
touches: [Curl.Extension.Abstractions.UnitLibrary/Description, Curl.Extension.Abstractions.UnitTests/Description, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CurlHelpTable.cs, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-29
completed:
---
# BL-4093 — Add --extensions and --ai-help extensions

## Goal

`curl --extensions` lists every compiled-in extension with its schemes and options, and `curl --ai-help extensions` documents each one in Markdown, both built from the extensions' own descriptions.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". ADR-0999 keeps `curl -V` unchanged, so extensions need their own listing. Every extension task after this one supplies a description and is listed automatically.

## Acceptance criteria

- [ ] An extension supplies a description (name, summary, schemes, options, specification link) through `Curl.Extension.Abstractions`; a test extension's description appears in both outputs.
- [ ] `curl --extensions` prints one line per extension; with `CURL_EXTENSIONS=0` it prints that extensions are off and exits 0.
- [ ] `curl --ai-help` lists `extensions` as a category, and `curl --ai-help extensions` prints each extension's section.
- [ ] `curl -V` output is byte-for-byte unchanged (existing tests pass).
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

The option `--extensions` is Curl's own: record in an ADR that it is not a curl option, so the gap office ignores it.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: bb166f31 34fde2bb 99a0caca 8b541e9f e35aff77 3d836d5e.
