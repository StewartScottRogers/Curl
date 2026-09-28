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
completed: 2026-09-28
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

- [x] A `Curl.Cli.UnitTests` test enumerates every alias-table name without an option-table row (long form, and the short letter where it has one) and asserts the refusal the ADR pins, including the exit code.
- [x] `--bogus`, `-9` and `--no-bogus` are refused as `is unknown`, exit 2, unchanged.
- [x] A `-K` config line naming an unimplemented option is handled as the ADR states; a test pins it.
- [x] A `Curl.Console.UnitTests` test pins the standard-error bytes and exit code for one unimplemented option end to end, with and without `-s`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Implemented ADR-0137 class 3 in `CommandLineParser`: `RefuseUnlistedName` (long and `--expand-<name>`), `RefuseUnlistedNegation` (`--no-<name>`: not reversible when the alias table says `NotAccepted`, else not supported) and `RefuseUnlistedLetter` (short letter, spelled as the whole bundle). `-K` lines go through the same `ParseLong`, and `ConfigFileOptionRefused` already carried a non-unknown reason to `curl: option -K: the installed libcurl version does not support this`, so config files needed no change.
- Made `CurlOptionAliasTable`, `CurlOptionAlias` and `CurlOptionNoPrefix` public (plus new `TryFindName`, `IsLetter`) so the tests can derive the unimplemented set from the two public tables; the alternative, an `InternalsVisibleTo` just for this, would be the only one in `Curl.Cli.UnitLibrary`.
- Measured the Windows system curl 8.21.0 on 2026-09-28: `-s --http3 x` and `-sS --http3 x` still print the refusal and exit 2, so `-s` hides neither class-3 nor class-4 refusals; the console test pins that with and without `-s`.
- All tests derive their names from the tables (`CommandLineUnimplementedOptionTests`, `CurlCommandRunnerUnimplementedOptionTests`); none lists an unimplemented option.
- BL-779 (filed by BL-496) asked for the same parser change; it is covered here and moved to Deferred as a duplicate.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green in all 24 test projects (Cli 2507 passed, Console 1283 passed); `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. A real curl option without a CommandLineOptionTable row is refused with 'the installed libcurl version does not support this', exit 2, on the command line, as a bundle letter, --no-/--expand- and in -K files; typos keep 'is unknown'
