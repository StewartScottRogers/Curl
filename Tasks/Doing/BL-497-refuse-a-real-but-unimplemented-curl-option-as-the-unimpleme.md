---
id: BL-497
title: Refuse a real but unimplemented curl option as the unimplemented-option ADR decides
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-496]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-497 — Refuse a real but unimplemented curl option as the unimplemented-option ADR decides

## Goal

Every curl 8.21.0 long option that is in `CurlOptionAliasTable` but has no row in `CommandLineOptionTable` is refused exactly as the ADR written by BL-496 decides, and a name that is in neither table is still refused as `is unknown`, exit 2.

## Context

- Conformance audit 2026-09-28, row 33. The decision is BL-496's ADR; read it first.
- Code: `Curl.Cli.UnitLibrary/CommandLineParser.cs` (`ParseNegatedLong`, `ParseUnlistedLong`, `ParseShortBundle`), `CommandLineRefusal.cs`, `CurlOptionAliasTable.cs`, and `ConfigFileApplier.cs` for `-K` lines (`found an unknown config option`).
- Short letters count too: `-4`, `-6`, `-1`, `-N`, `-Z`, `-:`, `-a`, `-B`, `-n` are real letters until their tasks land.
- The set of unimplemented names shrinks as later tasks add rows, so tests must derive it from the two tables, not list it.

## Acceptance criteria

- [ ] A `Curl.Cli.UnitTests` test enumerates every alias-table name without an option-table row (long form, and the short letter where it has one) and asserts the refusal the ADR pins, including the exit code.
- [ ] `--bogus`, `-9` and `--no-bogus` are refused as `is unknown`, exit 2, unchanged.
- [ ] A `-K` config line naming an unimplemented option is handled as the ADR states; a test pins it.
- [ ] A `Curl.Console.UnitTests` test pins the standard-error bytes and exit code for one unimplemented option end to end, with and without `-s`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
